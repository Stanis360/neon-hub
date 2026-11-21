namespace NeonMonitor.Services.Fps;

/// <summary>Ein einzelnes Bild (Frame), wie PresentMon es meldet.</summary>
/// <param name="ProcessId">Prozess, der das Bild ausgegeben hat.</param>
/// <param name="Application">Exe-Name, z. B. „cod.exe“.</param>
/// <param name="TimeSeconds">Zeitpunkt seit Aufnahmestart (PresentMon-Zeitbasis).</param>
/// <param name="FrameTimeMs">Abstand zum vorherigen Bild in ms.</param>
/// <param name="InputLatencyMs">Eingabe (Maus/Tastatur) bis Bild auf dem Monitor – nur bei Bildern mit Eingabe.</param>
/// <param name="ClickLatencyMs">Mausklick bis Bild auf dem Monitor – nur bei Bildern mit Klick.</param>
/// <param name="DisplayDelayMs">Vom Present-Aufruf bis zur Anzeige.</param>
public readonly record struct FrameSample(int ProcessId, string Application, double TimeSeconds, double FrameTimeMs,
    double? InputLatencyMs = null, double? ClickLatencyMs = null, double? DisplayDelayMs = null);

/// <summary>Kennzahlen der aktuell erfassten Anwendung.</summary>
public sealed record FpsStats(
    string Application,
    double Fps,
    double FrameTimeAvgMs,
    double FrameTimeMaxMs,
    double? OnePercentLow,
    double[] RecentFrameTimes,
    double? InputLatencyMs,
    double? ClickLatencyMs,
    double? DisplayDelayMs,
    double StuttersPerMinute,
    double[] Histogram);

/// <summary>
/// Sammelt die Frames aller Anwendungen, wählt die „Spiel“-Anwendung aus und berechnet
/// FPS, Frametimes und 1 % Low. Threadsicher: PresentMon schreibt von einem Lese-Thread,
/// der Mess-Thread liest.
/// </summary>
public sealed class FrameTracker
{
    /// <summary>So lange werden Frames für das 1 % Low aufbewahrt.</summary>
    private const double KeepSeconds = 30;
    /// <summary>Ohne neues Bild seit so vielen ms gilt eine Anwendung als inaktiv.</summary>
    private const long StaleAfterMs = 2000;
    /// <summary>Wie viele Einzel-Frametimes ans Diagramm gehen.</summary>
    public const int RecentCount = 300;

    /// <summary>
    /// Programme, die zwar Bilder ausgeben, aber nie „das Spiel“ sind (Browser, Overlays, Windows-Shell …).
    /// Sie können trotzdem manuell ausgewählt werden.
    /// </summary>
    private static readonly HashSet<string> IgnoredApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm.exe", "NeonMonitor.exe", "explorer.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe",
        "SearchHost.exe", "TextInputHost.exe", "ApplicationFrameHost.exe", "LockApp.exe", "SystemSettings.exe",
        "chrome.exe", "msedge.exe", "msedgewebview2.exe", "firefox.exe", "opera.exe", "brave.exe",
        "Discord.exe", "steamwebhelper.exe", "steam.exe", "EpicGamesLauncher.exe", "Battle.net.exe",
        "RadeonSoftware.exe", "AMDRSServ.exe", "AMDRSSrcExt.exe", "Overwolf.exe", "obs64.exe", "Spotify.exe",
        "Teams.exe", "ms-teams.exe", "Code.exe", "WindowsTerminal.exe",
        // Desktop-/Electron-Apps
        "claude.exe", "ChatGPT.exe", "slack.exe", "WhatsApp.exe", "Telegram.exe", "Signal.exe", "Notion.exe",
        "Figma.exe", "Cursor.exe", "Obsidian.exe", "vivaldi.exe", "operagx.exe", "launcher.exe",
        // Launcher
        "EADesktop.exe", "UbisoftConnect.exe", "upc.exe", "GalaxyClient.exe", "RiotClientUx.exe",
        "RiotClientServices.exe", "EpicWebHelper.exe", "XboxPcApp.exe", "GameBar.exe", "GameBarFTServer.exe",
        // Windows & Werkzeuge
        "Widgets.exe", "PhoneExperienceHost.exe", "SnippingTool.exe", "ScreenClippingHost.exe", "Taskmgr.exe",
        "mstsc.exe", "msrdc.exe", "CrossDeviceResume.exe",
        // Videoplayer, Hintergründe, Overlays
        "vlc.exe", "mpc-hc64.exe", "PotPlayerMini64.exe", "wallpaper32.exe", "wallpaper64.exe",
        "webwallpaper32.exe", "Lively.exe", "Rainmeter.exe", "MSIAfterburner.exe", "RTSS.exe", "HWiNFO64.exe",
    };

    /// <summary>Vom Nutzer zusätzlich ausgeschlossene Programme (wird als Ganzes ersetzt → threadsicher).</summary>
    private static volatile HashSet<string> _userIgnored = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Eigene Ausschlussliste setzen (aus den Einstellungen).</summary>
    public static void SetUserIgnored(IEnumerable<string> apps) =>
        _userIgnored = new HashSet<string>(apps, StringComparer.OrdinalIgnoreCase);

    /// <summary>Ist die Anwendung ein Browser/Overlay/Systemprogramm (also kein Spiel)?</summary>
    public static bool IsIgnored(string application) =>
        IgnoredApps.Contains(application) || _userIgnored.Contains(application);

    private sealed class ProcessFrames(string application)
    {
        public string Application { get; } = application;
        public Queue<(double Time, double Ms)> Frames { get; } = new();
        /// <summary>Latenzwerte (Zeit, Eingabe→Bild, Klick→Bild, bis Anzeige) der letzten Sekunden.</summary>
        public Queue<(double Time, double? Input, double? Click, double? Display)> Latency { get; } = new();
        /// <summary>Zeitpunkte erkannter Ruckler (letzte 60 s).</summary>
        public Queue<double> Stutters { get; } = new();
        /// <summary>Die letzten Frametimes für den gleitenden Median der Ruckler-Erkennung.</summary>
        public Queue<float> MedianWindow { get; } = new();
        public double FirstTime { get; set; } = double.NaN;
        public long LastSeenTick { get; set; }
    }

    /// <summary>Zeitraum für die Latenz-Mittelwerte (Eingaben kommen nicht in jedem Bild vor).</summary>
    private const double LatencySeconds = 5;

    private readonly object _sync = new();
    private readonly Dictionary<int, ProcessFrames> _processes = new();
    private int _stickyPid; // zuletzt gewählte Anwendung (bleibt gewählt, wenn man auf einen anderen Monitor klickt)

    public void Add(FrameSample f)
    {
        if (f.FrameTimeMs <= 0 || !double.IsFinite(f.FrameTimeMs)) return;
        lock (_sync)
        {
            if (!_processes.TryGetValue(f.ProcessId, out var p))
                _processes[f.ProcessId] = p = new ProcessFrames(f.Application);

            p.Frames.Enqueue((f.TimeSeconds, f.FrameTimeMs));
            p.LastSeenTick = Environment.TickCount64;
            if (double.IsNaN(p.FirstTime)) p.FirstTime = f.TimeSeconds;
            while (p.Frames.Count > 0 && p.Frames.Peek().Time < f.TimeSeconds - KeepSeconds) p.Frames.Dequeue();

            // Latenz
            if (f.InputLatencyMs is not null || f.ClickLatencyMs is not null || f.DisplayDelayMs is not null)
                p.Latency.Enqueue((f.TimeSeconds, f.InputLatencyMs, f.ClickLatencyMs, f.DisplayDelayMs));
            while (p.Latency.Count > 0 && p.Latency.Peek().Time < f.TimeSeconds - LatencySeconds) p.Latency.Dequeue();

            // Ruckler: Vergleich mit dem Median der vorherigen Bilder
            if (p.MedianWindow.Count >= 10 && FrameAnalysis.IsStutter(f.FrameTimeMs, FrameAnalysis.Median(p.MedianWindow)))
                p.Stutters.Enqueue(f.TimeSeconds);
            p.MedianWindow.Enqueue((float)f.FrameTimeMs);
            if (p.MedianWindow.Count > FrameAnalysis.MedianWindow) p.MedianWindow.Dequeue();
            while (p.Stutters.Count > 0 && p.Stutters.Peek() < f.TimeSeconds - 60) p.Stutters.Dequeue();
        }
    }

    /// <summary>Alle Anwendungen, die gerade Bilder ausgeben.</summary>
    public string[] ActiveApplications()
    {
        lock (_sync)
        {
            RemoveStale();
            return _processes.Values.Select(p => p.Application).Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <summary>
    /// Wählt die zu messende Anwendung und berechnet ihre Kennzahlen.
    /// Reihenfolge: manuell gewählte → Vordergrundfenster → zuletzt gewählte → die mit den meisten Bildern.
    /// </summary>
    /// <param name="pinnedApplication">Vom Nutzer festgelegte Anwendung oder null für automatisch.</param>
    /// <param name="foregroundPid">Prozess des aktiven Fensters.</param>
    public FpsStats? GetStats(string? pinnedApplication, int foregroundPid)
    {
        lock (_sync)
        {
            RemoveStale();
            var pid = SelectProcess(pinnedApplication, foregroundPid);
            if (pid == 0 || !_processes.TryGetValue(pid, out var p) || p.Frames.Count < 2) return null;

            var frames = p.Frames.ToArray();
            var latest = frames[^1].Time;

            // letzte Sekunde: FPS = 1000 / mittlere Frametime (genauer als Bilder zählen)
            var lastSecond = frames.Where(x => x.Time > latest - 1.0).Select(x => x.Ms).ToArray();
            if (lastSecond.Length == 0) return null;
            var avg = lastSecond.Average();

            // 1 % Low: 99. Perzentil der Frametimes über das ganze Fenster (mind. 100 Bilder)
            double? low = null;
            if (frames.Length >= 100)
            {
                var sorted = frames.Select(x => x.Ms).OrderBy(x => x).ToArray();
                var p99 = sorted[(int)Math.Min(sorted.Length - 1, Math.Ceiling(sorted.Length * 0.99) - 1)];
                low = 1000.0 / p99;
            }

            var recent = frames.Skip(Math.Max(0, frames.Length - RecentCount)).Select(x => x.Ms).ToArray();

            // Latenz: Mittel der letzten Sekunden (nur Bilder, in denen es einen Wert gab)
            static double? Avg(IEnumerable<double?> v)
            {
                var list = v.Where(x => x is not null).Select(x => x!.Value).ToList();
                return list.Count > 0 ? list.Average() : null;
            }
            var input = Avg(p.Latency.Select(l => l.Input));
            var click = Avg(p.Latency.Select(l => l.Click));
            var display = Avg(p.Latency.Where(l => l.Time > latest - 1).Select(l => l.Display));

            // Ruckler pro Minute – in der ersten Minute hochgerechnet
            var observed = Math.Clamp(latest - p.FirstTime, 1, 60);
            var stuttersPerMin = p.Stutters.Count * 60.0 / observed;

            return new FpsStats(p.Application, 1000.0 / avg, avg, lastSecond.Max(), low, recent,
                input, click, display, stuttersPerMin, FrameAnalysis.Histogram(frames.Select(x => x.Ms)));
        }
    }

    private int SelectProcess(string? pinned, int foregroundPid)
    {
        if (!string.IsNullOrEmpty(pinned))
        {
            var match = _processes.Where(kv => kv.Value.Application.Equals(pinned, StringComparison.OrdinalIgnoreCase))
                                  .OrderByDescending(kv => kv.Value.Frames.Count).Select(kv => kv.Key).FirstOrDefault();
            return match;
        }

        bool IsCandidate(int pid) => _processes.TryGetValue(pid, out var p) && !IsIgnored(p.Application);

        if (IsCandidate(foregroundPid)) return _stickyPid = foregroundPid;
        if (IsCandidate(_stickyPid)) return _stickyPid;

        // Sonst: die Anwendung mit den meisten Bildern
        var best = _processes.Where(kv => !IsIgnored(kv.Value.Application))
                             .OrderByDescending(kv => kv.Value.Frames.Count)
                             .Select(kv => kv.Key).FirstOrDefault();
        return _stickyPid = best;
    }

    private void RemoveStale()
    {
        var now = Environment.TickCount64;
        foreach (var pid in _processes.Where(kv => now - kv.Value.LastSeenTick > StaleAfterMs).Select(kv => kv.Key).ToList())
            _processes.Remove(pid);
    }
}

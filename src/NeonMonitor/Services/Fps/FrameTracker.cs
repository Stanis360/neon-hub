namespace NeonMonitor.Services.Fps;

/// <summary>Ein einzelnes Bild (Frame), wie PresentMon es meldet.</summary>
/// <param name="ProcessId">Prozess, der das Bild ausgegeben hat.</param>
/// <param name="Application">Exe-Name, z. B. „cod.exe“.</param>
/// <param name="TimeSeconds">Zeitpunkt seit Aufnahmestart (PresentMon-Zeitbasis).</param>
/// <param name="FrameTimeMs">Abstand zum vorherigen Bild in ms.</param>
public readonly record struct FrameSample(int ProcessId, string Application, double TimeSeconds, double FrameTimeMs);

/// <summary>Kennzahlen der aktuell erfassten Anwendung.</summary>
public sealed record FpsStats(
    string Application,
    double Fps,
    double FrameTimeAvgMs,
    double FrameTimeMaxMs,
    double? OnePercentLow,
    double[] RecentFrameTimes);

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
    };

    private sealed class ProcessFrames(string application)
    {
        public string Application { get; } = application;
        public Queue<(double Time, double Ms)> Frames { get; } = new();
        public long LastSeenTick { get; set; }
    }

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
            while (p.Frames.Count > 0 && p.Frames.Peek().Time < f.TimeSeconds - KeepSeconds) p.Frames.Dequeue();
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
            return new FpsStats(p.Application, 1000.0 / avg, avg, lastSecond.Max(), low, recent);
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

        bool IsCandidate(int pid) => _processes.TryGetValue(pid, out var p) && !IgnoredApps.Contains(p.Application);

        if (IsCandidate(foregroundPid)) return _stickyPid = foregroundPid;
        if (IsCandidate(_stickyPid)) return _stickyPid;

        // Sonst: die Anwendung mit den meisten Bildern
        var best = _processes.Where(kv => !IgnoredApps.Contains(kv.Value.Application))
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

using System.Runtime.InteropServices;
using System.Text;
using NeonMonitor.Models;
using NeonMonitor.Services.Sensors;

namespace NeonMonitor.Services.Fps;

/// <summary>
/// Sensorquelle für FPS und Frametimes. Nutzt <see cref="PresentMonRunner"/> (liegt als
/// <c>Tools\PresentMon.exe</c> neben NeonMonitor) und <see cref="FrameTracker"/>.
/// </summary>
public sealed class FpsSource : ISensorSource
{
    private readonly FrameTracker _tracker = new();
    private PresentMonRunner? _runner;
    private volatile string? _pinnedApplication;

    public string Name => "FPS (PresentMon)";
    public bool IsAvailable { get; private set; }
    public string Status => _runner is null ? "PresentMon fehlt" : _runner.Status;

    /// <summary>Pfad der mitgelieferten PresentMon.exe.</summary>
    public static string ExePath => Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon.exe");

    /// <summary>
    /// Fest gewählte Anwendung (Exe-Name) oder null = automatisch
    /// (Vordergrundfenster bzw. die Anwendung mit den meisten Bildern).
    /// Darf vom UI-Thread gesetzt werden.
    /// </summary>
    public string? PinnedApplication
    {
        get => _pinnedApplication;
        set => _pinnedApplication = value;
    }

    public void Initialize()
    {
        if (!File.Exists(ExePath))
            throw new FileNotFoundException("PresentMon.exe nicht gefunden – bitte Installieren.bat erneut ausführen.", ExePath);

        _runner = new PresentMonRunner(ExePath, _tracker.Add);
        _runner.Start();
        IsAvailable = true;
    }

    public void Fill(HardwareSnapshot s)
    {
        s.FpsCandidates = _tracker.ActiveApplications();

        var stats = _tracker.GetStats(_pinnedApplication, ForegroundProcessId());
        if (stats is null) return;

        s.FpsApplication ??= stats.Application;
        s.Fps ??= stats.Fps;
        s.FrameTimeMs ??= stats.FrameTimeAvgMs;
        s.FrameTimeMaxMs ??= stats.FrameTimeMaxMs;
        s.Fps1PercentLow ??= stats.OnePercentLow;
        s.RecentFrameTimes = stats.RecentFrameTimes;
    }

    public void AppendReport(StringBuilder r)
    {
        r.AppendLine($"=== {Name} ===");
        r.AppendLine($"Status: {Status}");
        r.AppendLine($"Programm: {ExePath} ({(File.Exists(ExePath) ? "vorhanden" : "FEHLT")})");
        r.AppendLine($"Aktive Anwendungen: {string.Join(", ", _tracker.ActiveApplications())}");
    }

    private static int ForegroundProcessId()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public void Dispose()
    {
        _runner?.Dispose();
        _runner = null;
        IsAvailable = false;
    }
}

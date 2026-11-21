using System.Diagnostics;
using System.Text;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Fps;

namespace NeonMonitor.Services.Recording;

/// <summary>Momentaufnahme des Aufnahmezustands für die Oberfläche.</summary>
public sealed record RecordingStatus(bool IsRecording, TimeSpan Elapsed, int Samples, int Frames, string? Application, string? File);

/// <summary>
/// Zeichnet Sitzungen auf:
/// <list type="bullet">
/// <item><c>Spiel_Datum_Uhrzeit.csv</c> – eine Zeile pro Messung (alle Sensoren + FPS).</item>
/// <item><c>…-frames.csv</c> – eine Zeile pro Bild (exakte Frametimes → genaue 1 % / 0,1 % Lows).</item>
/// <item><c>….json</c> – Zusammenfassung für die Vergleichsseite.</item>
/// </list>
/// Messwerte kommen vom Mess-Thread, Frames vom PresentMon-Lese-Thread; ein Lock schützt beide Schreibwege.
/// </summary>
public sealed class RecordingService : IDisposable
{
    private readonly HardwareMonitorService _monitor;
    private readonly Func<double> _pricePerKwh;
    private readonly object _sync = new();

    private StreamWriter? _csv;
    private StreamWriter? _frames;
    private SessionSummary? _session;
    private readonly Stopwatch _clock = new();
    private readonly List<float> _frameTimes = new();
    private double? _firstFrameTime;
    private int _samples;
    private string? _latestApplication;   // zuletzt erkannte Anwendung (auch ohne Aufnahme)
    private string? _latestCpu, _latestGpu;

    // Laufende Summen/Maxima für die Zusammenfassung
    private readonly Accumulator _cpuLoad = new(), _gpuLoad = new(), _cpuPower = new(), _gpuPower = new(),
                                 _cpuTemp = new(), _gpuTemp = new(), _hotspot = new(), _ramLoad = new(), _fps = new(),
                                 _totalPower = new(), _cpuFan = new(), _caseFan = new(), _memTemp = new(),
                                 _input = new(), _click = new(), _ping = new(), _lossInet = new(), _lossRouter = new();
    private int _gpuBound, _cpuBound, _limited, _classified;
    private string? _connection;
    private double _energyWh;
    private TimeSpan _lastSampleAt;

    /// <param name="monitor">Messdienst (liefert Snapshots und Frames).</param>
    /// <param name="pricePerKwh">Liefert den aktuellen Strompreis in €/kWh.</param>
    public RecordingService(HardwareMonitorService monitor, Func<double> pricePerKwh)
    {
        _monitor = monitor;
        _pricePerKwh = pricePerKwh;
        monitor.SnapshotReady += OnSnapshot;
        monitor.Fps.FrameReceived += OnFrame;
    }

    /// <summary>Aufnahme gestartet/gestoppt (Aufruf auf dem Thread, der Start/Stop ausgelöst hat).</summary>
    public event EventHandler? StateChanged;

    public bool IsRecording
    {
        get { lock (_sync) return _session is not null; }
    }

    /// <summary>Zuletzt per FPS-Messung erkannte Anwendung (für den automatischen Spielnamen).</summary>
    public string? LatestApplication
    {
        get { lock (_sync) return _latestApplication; }
    }

    public RecordingStatus GetStatus()
    {
        lock (_sync)
        {
            return new RecordingStatus(_session is not null, _clock.Elapsed, _samples, _frameTimes.Count,
                                       _session?.Application, _session?.CsvFile);
        }
    }

    /// <summary>Startet eine neue Aufnahme.</summary>
    /// <param name="game">Spielname für Datei und Anzeige.</param>
    public void Start(string game)
    {
        lock (_sync)
        {
            if (_session is not null) return;
            var start = DateTime.Now;
            var baseName = SessionStore.BaseName(game, start);

            _session = new SessionSummary
            {
                Game = string.IsNullOrWhiteSpace(game) ? "Unbekannt" : game.Trim(),
                Application = _latestApplication,
                Start = start,
                CpuName = _latestCpu,
                GpuName = _latestGpu,
                CsvFile = baseName + ".csv",
                FramesFile = baseName + "-frames.csv",
                JsonPath = Path.Combine(SessionStore.Directory, baseName + ".json"),
            };

            // UTF-8 mit BOM, damit Excel Umlaute (°C, Lüfter) richtig anzeigt
            var utf8Bom = new UTF8Encoding(true);
            _csv = new StreamWriter(Path.Combine(SessionStore.Directory, _session.CsvFile), false, utf8Bom);
            _csv.WriteLine("sep=;");
            _csv.WriteLine(string.Join(SessionStore.Separator, RecordingColumns.All));
            _frames = new StreamWriter(Path.Combine(SessionStore.Directory, _session.FramesFile), false, utf8Bom);
            _frames.WriteLine("sep=;");
            _frames.WriteLine("Sekunden;Frametime ms");

            _frameTimes.Clear();
            _firstFrameTime = null;
            _samples = 0;
            foreach (var a in new[] { _cpuLoad, _gpuLoad, _cpuPower, _gpuPower, _cpuTemp, _gpuTemp, _hotspot, _ramLoad, _fps,
                                      _totalPower, _cpuFan, _caseFan, _memTemp, _input, _click, _ping, _lossInet, _lossRouter }) a.Reset();
            _gpuBound = _cpuBound = _limited = _classified = 0;
            _connection = null;
            _energyWh = 0;
            _lastSampleAt = TimeSpan.Zero;
            _clock.Restart();
            Log.Info($"Aufnahme gestartet: {_session.CsvFile}");
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Beendet die Aufnahme und speichert die Zusammenfassung. Gibt sie zurück (oder null).</summary>
    public SessionSummary? Stop()
    {
        SessionSummary? result;
        lock (_sync)
        {
            if (_session is null) return null;
            result = _session;
            _clock.Stop();

            result.End = DateTime.Now;
            result.DurationSeconds = _clock.Elapsed.TotalSeconds;
            FillFrameStats(result);
            result.MaxCpuTemp = _cpuTemp.Max;
            result.MaxGpuTemp = _gpuTemp.Max;
            result.MaxGpuHotspot = _hotspot.Max;
            result.AvgCpuLoad = _cpuLoad.Average;
            result.AvgGpuLoad = _gpuLoad.Average;
            result.AvgCpuPower = _cpuPower.Average;
            result.AvgGpuPower = _gpuPower.Average;
            result.MaxRamLoad = _ramLoad.Max;
            result.MaxGpuMemTemp = _memTemp.Max;
            result.AvgInputLatencyMs = _input.Average;
            result.AvgClickLatencyMs = _click.Average;
            result.AvgPingInternetMs = _ping.Average;
            result.AvgLossInternetPct = _lossInet.Average;
            result.MaxLossInternetPct = _lossInet.Max;
            result.AvgLossRouterPct = _lossRouter.Average;
            result.ConnectionType = _connection;
            if (_classified > 0)
            {
                result.GpuBoundPct = _gpuBound * 100.0 / _classified;
                result.CpuBoundPct = _cpuBound * 100.0 / _classified;
                result.LimitPct = _limited * 100.0 / _classified;
            }
            result.AvgCpuFan = _cpuFan.Average;
            result.AvgCaseFan = _caseFan.Average;
            result.AvgTotalPower = _totalPower.Average;
            if (_totalPower.Average is not null)
            {
                result.EnergyWh = _energyWh;
                result.CostEuro = _energyWh / 1000.0 * _pricePerKwh();
                if (result.AvgFps is { } fps && _totalPower.Average > 0) result.FpsPerWatt = fps / _totalPower.Average;
            }

            _csv?.Dispose();
            _frames?.Dispose();
            _csv = _frames = null;

            // Ohne einzige Bildmessung ist die Frames-Datei leer → weglassen
            if (_frameTimes.Count == 0)
            {
                File.Delete(Path.Combine(SessionStore.Directory, result.FramesFile));
                result.FramesFile = "";
            }
            SessionStore.SaveSummary(result);
            _session = null;
            Log.Info($"Aufnahme beendet: {result.CsvFile}, {result.FrameCount} Frames, Ø {result.AvgFps:0.0} FPS");
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <summary>FPS-Kennzahlen aus allen Einzelbildern (genauer als aus den Sekundenwerten).</summary>
    private void FillFrameStats(SessionSummary s)
    {
        s.FrameCount = _frameTimes.Count;
        if (_frameTimes.Count >= 2)
        {
            var avg = _frameTimes.Average(f => (double)f);
            s.AvgFrameTimeMs = avg;
            s.AvgFps = 1000.0 / avg;
            var sorted = _frameTimes.Select(f => (double)f).OrderBy(f => f).ToArray();
            double Percentile(double p) => sorted[(int)Math.Min(sorted.Length - 1, Math.Ceiling(sorted.Length * p) - 1)];
            if (sorted.Length >= 100) s.Low1 = 1000.0 / Percentile(0.99);
            if (sorted.Length >= 1000) s.Low01 = 1000.0 / Percentile(0.999);
        }
        else if (_fps.Average is { } fps)
        {
            s.AvgFps = fps; // Fallback: nur Sekundenwerte vorhanden
        }

        if (_frameTimes.Count >= 100)
        {
            s.StutterCount = FrameAnalysis.CountStutters(_frameTimes);
            var minutes = _frameTimes.Sum(f => (double)f) / 60_000.0;
            if (minutes > 0) s.StuttersPerMin = s.StutterCount / minutes;
            s.FrameHistogram = FrameAnalysis.Histogram(_frameTimes.Select(f => (double)f));
        }
    }

    // ---------------- Datenquellen ----------------

    private void OnSnapshot(object? sender, HardwareSnapshot s)
    {
        lock (_sync)
        {
            if (s.FpsApplication is not null) _latestApplication = s.FpsApplication;
            _latestCpu = s.CpuName ?? _latestCpu;
            _latestGpu = s.GpuName ?? _latestGpu;
            if (_session is null || _csv is null) return;

            _session.Application ??= s.FpsApplication;
            _session.CpuName ??= s.CpuName;
            _session.GpuName ??= s.GpuName;

            var f = SessionStore.Format;
            var row = new[]
            {
                s.Timestamp.ToString("HH:mm:ss.fff"),
                f(_clock.Elapsed.TotalSeconds, "0.000"),
                s.FpsApplication ?? "",
                f(s.Fps, "0.0"), f(s.Fps1PercentLow, "0.0"), f(s.FrameTimeMs, "0.00"), f(s.FrameTimeMaxMs, "0.00"),
                f(s.CpuLoad, "0.0"), f(s.CpuTemp, "0.0"), f(s.CpuPower, "0.0"), f(s.CpuClockAvg, "0"),
                f(s.GpuLoad, "0.0"), f(s.GpuTemp, "0.0"), f(s.GpuHotspot, "0.0"), f(s.GpuClock, "0"), f(s.GpuMemClock, "0"),
                f(s.GpuVramUsedMb, "0"), f(s.GpuPower, "0.0"), f(s.GpuFan, "0"),
                f(s.RamUsedGb, "0.00"), f(s.RamLoad, "0.0"),
                f(TotalPower(s), "0.0"), f(s.CpuFanRpm, "0"), f(s.CaseFanRpm, "0"),
                f(s.NetDownMbit, "0.00"), f(s.NetUpMbit, "0.00"), f(s.DiskReadMBs, "0.0"), f(s.DiskWriteMBs, "0.0"),
                f(s.GpuMemTemp, "0.0"),
                f(s.CpuCoreMaxLoad, "0.0"), s.Bottleneck ?? "", f(s.InputLatencyMs, "0.0"), f(s.ClickLatencyMs, "0.0"),
                f(s.StuttersPerMin, "0.0"), f(s.PingRouterMs, "0.0"), f(s.PingInternetMs, "0.0"),
                f(s.LossRouterPct, "0.0"), f(s.LossInternetPct, "0.0"), f(s.JitterMs, "0.0"),
            };
            _csv.WriteLine(string.Join(SessionStore.Separator, row));
            _csv.Flush();
            _frames?.Flush();
            _samples++;

            _cpuLoad.Add(s.CpuLoad); _gpuLoad.Add(s.GpuLoad);
            _cpuPower.Add(s.CpuPower); _gpuPower.Add(s.GpuPower);
            _cpuTemp.Add(s.CpuTemp); _gpuTemp.Add(s.GpuTemp); _hotspot.Add(s.GpuHotspot);
            _ramLoad.Add(s.RamLoad); _fps.Add(s.Fps);
            _cpuFan.Add(s.CpuFanRpm); _caseFan.Add(s.CaseFanRpm); _memTemp.Add(s.GpuMemTemp);
            _input.Add(s.InputLatencyMs); _click.Add(s.ClickLatencyMs);
            _ping.Add(s.PingInternetMs); _lossInet.Add(s.LossInternetPct); _lossRouter.Add(s.LossRouterPct);
            _connection ??= s.ConnectionType;
            if (s.Bottleneck is { } b)
            {
                _classified++;
                if (b == BottleneckAnalyzer.Gpu) _gpuBound++;
                else if (b == BottleneckAnalyzer.Cpu) _cpuBound++;
                else if (b == BottleneckAnalyzer.Limit) _limited++;
            }

            // Energie: Leistung × Zeit seit der letzten Messung
            var now = _clock.Elapsed;
            if (TotalPower(s) is { } watt)
            {
                _totalPower.Add(watt);
                _energyWh += watt * (now - _lastSampleAt).TotalHours;
            }
            _lastSampleAt = now;
        }
    }

    /// <summary>CPU-Package + GPU in Watt (null, wenn beides fehlt).</summary>
    public static double? TotalPower(HardwareSnapshot s) =>
        s.CpuPower is null && s.GpuPower is null ? null : (s.CpuPower ?? 0) + (s.GpuPower ?? 0);

    private void OnFrame(FrameSample frame)
    {
        lock (_sync)
        {
            if (_session is null || _frames is null) return;
            // Nur Bilder der gemessenen Anwendung (nicht Browser, Discord …)
            var app = _session.Application ?? _latestApplication;
            if (app is null || !frame.Application.Equals(app, StringComparison.OrdinalIgnoreCase)) return;

            _firstFrameTime ??= frame.TimeSeconds;
            _frameTimes.Add((float)frame.FrameTimeMs);
            _frames.Write(SessionStore.Format(frame.TimeSeconds - _firstFrameTime.Value, "0.0000"));
            _frames.Write(SessionStore.Separator);
            _frames.WriteLine(SessionStore.Format(frame.FrameTimeMs, "0.000"));
        }
    }

    public void Dispose()
    {
        _monitor.SnapshotReady -= OnSnapshot;
        _monitor.Fps.FrameReceived -= OnFrame;
        if (IsRecording) Stop(); // laufende Aufnahme beim Beenden sauber abschließen
    }

    /// <summary>Durchschnitt und Maximum ohne alle Werte zu speichern.</summary>
    private sealed class Accumulator
    {
        private double _sum;
        private int _count;
        public double? Max { get; private set; }
        public double? Average => _count > 0 ? _sum / _count : null;

        public void Add(double? v)
        {
            if (v is not { } d || !double.IsFinite(d)) return;
            _sum += d;
            _count++;
            if (Max is null || d > Max) Max = d;
        }

        public void Reset()
        {
            _sum = 0;
            _count = 0;
            Max = null;
        }
    }
}

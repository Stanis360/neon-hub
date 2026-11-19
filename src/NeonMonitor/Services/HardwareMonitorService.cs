using System.Diagnostics;
using System.Text;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Sensors;

namespace NeonMonitor.Services;

/// <summary>
/// Herzstück der Messung: Ein eigener Hintergrund-Thread fragt in festem Intervall alle
/// Sensorquellen ab und meldet jeden fertigen <see cref="HardwareSnapshot"/> per Event.
/// <para>
/// Die UI abonniert <see cref="SnapshotReady"/> (und muss selbst auf den UI-Thread wechseln).
/// Ab Schritt 4 hängt sich hier auch die CSV-Aufzeichnung an.
/// </para>
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly List<ISensorSource> _sources;
    private readonly object _sync = new(); // schützt die Quellen (nicht threadsicher)
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile int _intervalMs;

    /// <summary>Neue Messung verfügbar (wird auf dem Mess-Thread ausgelöst!).</summary>
    public event EventHandler<HardwareSnapshot>? SnapshotReady;

    /// <summary>Alle Quellen sind initialisiert (wird auf dem Mess-Thread ausgelöst!).</summary>
    public event EventHandler? Initialized;

    public HardwareMonitorService(int intervalMs)
    {
        IntervalMs = intervalMs;
        // Reihenfolge = Priorität. Spätere Quellen füllen nur noch fehlende Werte.
        _sources = new List<ISensorSource>
        {
            new LibreHardwareSource(),
            new WindowsCounterSource(),
            new DriveSource(),
        };
    }

    public IReadOnlyList<ISensorSource> Sources => _sources;

    public bool IsInitialized { get; private set; }

    /// <summary>Abfrageintervall in ms (250–10 000).</summary>
    public int IntervalMs
    {
        get => _intervalMs;
        set => _intervalMs = Math.Clamp(value, 250, 10_000);
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Factory.StartNew(() => RunAsync(_cts.Token), _cts.Token,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            foreach (var source in _sources)
            {
                try
                {
                    source.Initialize();
                    Log.Info($"Quelle '{source.Name}': {source.Status}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Quelle '{source.Name}' konnte nicht starten", ex);
                }
            }
        }
        IsInitialized = true;
        Initialized?.Invoke(this, EventArgs.Empty);

        while (!ct.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();

            var snapshot = Sample();
            try { SnapshotReady?.Invoke(this, snapshot); }
            catch (Exception ex) { Log.Error("Fehler im SnapshotReady-Handler", ex); }

            // Intervall einhalten, unabhängig davon, wie lange die Messung gedauert hat
            var wait = TimeSpan.FromMilliseconds(_intervalMs) - Stopwatch.GetElapsedTime(started);
            if (wait < TimeSpan.FromMilliseconds(50)) wait = TimeSpan.FromMilliseconds(50);
            try { await Task.Delay(wait, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private HardwareSnapshot Sample()
    {
        var snapshot = new HardwareSnapshot { Timestamp = DateTime.Now };
        lock (_sync)
        {
            foreach (var source in _sources)
            {
                if (!source.IsAvailable) continue;
                try { source.Fill(snapshot); }
                catch (Exception ex) { Log.Error($"Quelle '{source.Name}' Messfehler", ex); }
            }
        }
        return snapshot;
    }

    /// <summary>
    /// Erstellt einen Textbericht mit ALLEN Rohsensoren aller Quellen.
    /// Damit lässt sich prüfen, welche Werte die eigene Hardware wirklich liefert.
    /// </summary>
    public string CreateSensorReport()
    {
        var r = new StringBuilder();
        r.AppendLine("NeonMonitor – Sensorbericht");
        r.AppendLine($"Erstellt:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        r.AppendLine($"Windows:       {Environment.OSVersion.VersionString}");
        r.AppendLine($"Admin:         {(SystemInfo.IsAdministrator() ? "ja" : "NEIN")}");
        r.AppendLine($"PawnIO:        {(SystemInfo.IsPawnIoInstalled() ? "installiert" : "FEHLT")}");
        r.AppendLine($".NET:          {Environment.Version}");
        r.AppendLine();

        lock (_sync)
        {
            foreach (var source in _sources)
            {
                try { source.AppendReport(r); }
                catch (Exception ex) { r.AppendLine($"Fehler in {source.Name}: {ex.Message}"); }
                r.AppendLine();
            }
        }

        r.AppendLine("=== Zuordnung in NeonMonitor (aktueller Snapshot) ===");
        var s = Sample();
        foreach (var p in typeof(HardwareSnapshot).GetProperties().Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string)))
            r.AppendLine($"  {p.Name,-18} = {p.GetValue(s) ?? "— (nicht verfügbar)"}");
        return r.ToString();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(3)); } catch { /* beim Beenden egal */ }
        lock (_sync)
        {
            foreach (var source in _sources) source.Dispose();
        }
        _cts?.Dispose();
    }
}

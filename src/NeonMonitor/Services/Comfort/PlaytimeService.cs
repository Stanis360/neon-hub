using System.Text.Json;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Fps;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.Services.Comfort;

/// <summary>Spielzeit eines Spiels über alle Tage.</summary>
public sealed class PlaytimeEntry
{
    public string Game { get; set; } = "";
    public double Seconds { get; set; }
    public DateTime FirstPlayed { get; set; }
    public DateTime LastPlayed { get; set; }
    /// <summary>Summe FPS × Sekunden (für den zeitgewichteten Durchschnitt).</summary>
    public double FpsSeconds { get; set; }
    public double? AvgFps => Seconds > 0 && FpsSeconds > 0 ? FpsSeconds / Seconds : null;
}

/// <summary>
/// Zählt automatisch die Spielzeit pro Spiel – immer, wenn NeonMonitor ein Spiel mit FPS erkennt
/// (auch ohne Aufnahme). Gespeichert in <c>%AppData%\NeonMonitor\spielzeit.json</c>.
/// </summary>
public sealed class PlaytimeService
{
    private static readonly string FilePath = Path.Combine(AppPaths.AppDataDir, "spielzeit.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<string, PlaytimeEntry> _entries;
    private DateTime _lastTick = DateTime.MinValue;
    private DateTime _lastSave = DateTime.Now;

    public PlaytimeService()
    {
        _entries = Load().ToDictionary(e => e.Game, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Daten haben sich geändert (UI-Thread).</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<PlaytimeEntry> Entries => _entries.Values.OrderByDescending(e => e.Seconds).ToList();

    /// <summary>Mit jedem Snapshot aufrufen (UI-Thread).</summary>
    public void Track(HardwareSnapshot s)
    {
        var now = DateTime.Now;
        var dt = _lastTick == DateTime.MinValue ? 0 : (now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt is <= 0 or > 10) return; // nach Pausen/Standby nicht hochzählen

        // Spiel = erkannte Anwendung, kein ausgeschlossenes Programm, spürbare GPU-Last
        if (s.FpsApplication is not { } app || s.Fps is not > 1 || FrameTracker.IsIgnored(app)) return;
        if (s.GpuLoad is < 15) return;

        var game = GameNames.FromApplication(app);
        if (!_entries.TryGetValue(game, out var e))
            _entries[game] = e = new PlaytimeEntry { Game = game, FirstPlayed = now };
        e.Seconds += dt;
        e.FpsSeconds += s.Fps.Value * dt;
        e.LastPlayed = now;

        if ((now - _lastSave).TotalSeconds >= 60) Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        _lastSave = DateTime.Now;
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(_entries.Values.ToList(), JsonOptions)); }
        catch (Exception ex) { Log.Warn($"Spielzeit speichern: {ex.Message}"); }
    }

    public void Reset()
    {
        _entries.Clear();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static List<PlaytimeEntry> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<PlaytimeEntry>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) { Log.Warn($"Spielzeit laden: {ex.Message}"); }
        return new();
    }
}

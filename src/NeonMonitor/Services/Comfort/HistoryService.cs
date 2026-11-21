using System.Text.Json;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Fps;

namespace NeonMonitor.Services.Comfort;

/// <summary>Zustand einer SSD an einem Tag.</summary>
public sealed class DriveDay
{
    public string Name { get; set; } = "";
    public double? LifePct { get; set; }
    public double? WrittenGb { get; set; }
    public double? PowerOnHours { get; set; }
    public double? MaxTemp { get; set; }
}

/// <summary>Tageswerte für den Langzeitverlauf.</summary>
public sealed class DayRecord
{
    public DateTime Date { get; set; }
    public double? MaxCpuTemp { get; set; }
    public double? MaxGpuTemp { get; set; }
    public double? MaxHotspot { get; set; }
    public double? MaxGpuMemTemp { get; set; }
    public double? MaxCpuFan { get; set; }
    /// <summary>Spielzeit des Tages in Sekunden.</summary>
    public double GameSeconds { get; set; }
    public double CpuTempGamingSum { get; set; }
    public double GpuHotspotGamingSum { get; set; }
    public int GamingSamples { get; set; }
    public List<DriveDay> Drives { get; set; } = new();

    /// <summary>Ø CPU-Temperatur während gespielt wurde – bester Wert, um Kühlung über Wochen zu vergleichen.</summary>
    public double? AvgCpuTempGaming => GamingSamples > 0 ? CpuTempGamingSum / GamingSamples : null;
    public double? AvgHotspotGaming => GamingSamples > 0 ? GpuHotspotGamingSum / GamingSamples : null;
}

/// <summary>
/// Sammelt Tageswerte (Höchsttemperaturen, Ø Temperatur beim Spielen, Spielzeit, SSD-Zustand)
/// in <c>%AppData%\NeonMonitor\verlauf.json</c>. Läuft immer mit, auch ohne Aufnahme. Hält 400 Tage.
/// </summary>
public sealed class HistoryService
{
    private static readonly string FilePath = Path.Combine(AppPaths.AppDataDir, "verlauf.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly List<DayRecord> _days;
    private DateTime _lastTick = DateTime.MinValue;
    private DateTime _lastSave = DateTime.Now;

    public HistoryService()
    {
        _days = Load();
    }

    /// <summary>Alle Tage, ältester zuerst.</summary>
    public IReadOnlyList<DayRecord> Days => _days;

    /// <summary>Daten wurden geändert (höchstens einmal pro Minute ausgelöst).</summary>
    public event EventHandler? Changed;
    private DateTime _lastChanged = DateTime.MinValue;

    /// <summary>Mit jedem Snapshot aufrufen (UI-Thread).</summary>
    public void Track(HardwareSnapshot s)
    {
        var now = DateTime.Now;
        var dt = _lastTick == DateTime.MinValue ? 0 : (now - _lastTick).TotalSeconds;
        _lastTick = now;

        var day = Today(now);
        day.MaxCpuTemp = Max(day.MaxCpuTemp, s.CpuTemp);
        day.MaxGpuTemp = Max(day.MaxGpuTemp, s.GpuTemp);
        day.MaxHotspot = Max(day.MaxHotspot, s.GpuHotspot);
        day.MaxGpuMemTemp = Max(day.MaxGpuMemTemp, s.GpuMemTemp);
        day.MaxCpuFan = Max(day.MaxCpuFan, s.CpuFanRpm);

        var gaming = s.FpsApplication is { } app && s.Fps > 1 && !FrameTracker.IsIgnored(app) && s.GpuLoad >= 15;
        if (gaming && dt is > 0 and <= 10)
        {
            day.GameSeconds += dt;
            if (s.CpuTemp is { } c && s.GpuHotspot is { } h)
            {
                day.CpuTempGamingSum += c;
                day.GpuHotspotGamingSum += h;
                day.GamingSamples++;
            }
        }

        foreach (var d in s.DriveHealth)
        {
            var dd = day.Drives.FirstOrDefault(x => x.Name == d.Name);
            if (dd is null) day.Drives.Add(dd = new DriveDay { Name = d.Name });
            dd.LifePct = d.LifePct ?? dd.LifePct;
            dd.WrittenGb = d.WrittenGb ?? dd.WrittenGb;
            dd.PowerOnHours = d.PowerOnHours ?? dd.PowerOnHours;
            dd.MaxTemp = Max(dd.MaxTemp, d.TempC);
        }

        if ((now - _lastSave).TotalMinutes >= 5) Save();
        if ((now - _lastChanged).TotalSeconds >= 60)
        {
            _lastChanged = now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private DayRecord Today(DateTime now)
    {
        var date = now.Date;
        if (_days.Count > 0 && _days[^1].Date == date) return _days[^1];
        var d = new DayRecord { Date = date };
        _days.Add(d);
        while (_days.Count > 400) _days.RemoveAt(0);
        return d;
    }

    private static double? Max(double? a, double? b) =>
        b is not { } v || !double.IsFinite(v) ? a : a is null ? v : Math.Max(a.Value, v);

    public void Save()
    {
        _lastSave = DateTime.Now;
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(_days, JsonOptions)); }
        catch (Exception ex) { Log.Warn($"Verlauf speichern: {ex.Message}"); }
    }

    private static List<DayRecord> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return (JsonSerializer.Deserialize<List<DayRecord>>(File.ReadAllText(FilePath)) ?? new()).OrderBy(d => d.Date).ToList();
        }
        catch (Exception ex) { Log.Warn($"Verlauf laden: {ex.Message}"); }
        return new();
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NeonMonitor.Controls;
using NeonMonitor.Services.Comfort;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.ViewModels;

/// <summary>SSD-Zeile im Langzeitverlauf.</summary>
public sealed record DriveHealthRow(string Name, string LifeText, double LifeFraction, string WrittenText, string HoursText, string RecentText);

/// <summary>Zeile der Spielzeit-Statistik.</summary>
public sealed record PlaytimeRow(string Game, string TimeText, string LastPlayedText, string FpsText, double Fraction);

/// <summary>Reiter „Statistik“: Spielzeit pro Spiel, Gesamtwerte über alle Aufnahmen.</summary>
public sealed class StatsViewModel : ObservableObject
{
    private readonly PlaytimeService _playtime;
    private readonly HistoryService _history;
    private DateTime _lastRefresh = DateTime.MinValue;
    private string _totalText = "", _sessionsText = "";
    private int _rangeDays = 30;
    private IReadOnlyList<ChartLine> _tempLines = [];
    private DateTime? _chartStart;
    private string _trendText = "";

    /// <summary>Farben der Verlaufslinien.</summary>
    private static readonly Color CpuMax = Themes.ThemeManager.ColorOf(Themes.ThemeManager.Current.Accent),
                                  CpuAvg = Themes.ThemeManager.ColorOf(Themes.ThemeManager.Current.Accent2),
                                  Hotspot = Themes.ThemeManager.ColorOf(Themes.ThemeManager.Current.Secondary),
                                  GpuMax = Color.FromRgb(0x5A, 0xA9, 0xE6);

    public StatsViewModel(PlaytimeService playtime, HistoryService history)
    {
        _playtime = playtime;
        _history = history;
        _history.Changed += (_, _) => RefreshHistory();
        _playtime.Changed += (_, _) =>
        {
            if ((DateTime.Now - _lastRefresh).TotalSeconds >= 5) Refresh();
        };
        ResetCommand = new RelayCommand(() =>
        {
            if (MessageBox.Show("Gesamte Spielzeit-Statistik zurücksetzen?", "Statistik",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                _playtime.Reset();
        });
        RefreshCommand = new RelayCommand(() => { Refresh(); RefreshHistory(); });
        Refresh();
        RefreshHistory();
    }

    // ---------------- Langzeitverlauf ----------------

    public ObservableCollection<DriveHealthRow> Drives { get; } = new();
    public IReadOnlyList<ChartLine> TempLines { get => _tempLines; private set => SetProperty(ref _tempLines, value); }
    public DateTime? ChartStart { get => _chartStart; private set => SetProperty(ref _chartStart, value); }
    public double ChartDays => _rangeDays;
    public string TrendText { get => _trendText; private set => SetProperty(ref _trendText, value); }

    public bool Range7 { get => _rangeDays == 7; set { if (value) SetRange(7); } }
    public bool Range30 { get => _rangeDays == 30; set { if (value) SetRange(30); } }
    public bool Range90 { get => _rangeDays == 90; set { if (value) SetRange(90); } }

    private void SetRange(int days)
    {
        _rangeDays = days;
        foreach (var n in new[] { nameof(Range7), nameof(Range30), nameof(Range90), nameof(ChartDays) }) OnPropertyChanged(n);
        RefreshHistory();
    }

    public void RefreshHistory()
    {
        var today = DateTime.Today;
        var start = today.AddDays(-_rangeDays);
        var days = _history.Days.Where(d => d.Date >= start).ToList();
        ChartStart = start;

        ChartLine Line(Func<DayRecord, double?> f, Color c) => new(
            days.Select(d => (d.Date - start).TotalDays).ToArray(),
            days.Select(d => f(d) ?? double.NaN).ToArray(), c);
        TempLines =
        [
            Line(d => d.MaxCpuTemp, CpuMax),
            Line(d => d.AvgCpuTempGaming, CpuAvg),
            Line(d => d.MaxHotspot, Hotspot),
            Line(d => d.MaxGpuTemp, GpuMax),
        ];

        // Trend: Ø CPU-Temperatur beim Spielen, letzte 7 Tage gegenüber den 7 Tagen davor
        double? Avg(IEnumerable<DayRecord> ds)
        {
            var list = ds.Where(d => d.GamingSamples > 0).ToList();
            var n = list.Sum(d => d.GamingSamples);
            return n > 0 ? list.Sum(d => d.CpuTempGamingSum) / n : null;
        }
        var recent = Avg(_history.Days.Where(d => d.Date > today.AddDays(-7)));
        var before = Avg(_history.Days.Where(d => d.Date <= today.AddDays(-7) && d.Date > today.AddDays(-14)));
        TrendText = (recent, before) switch
        {
            ({ } r, { } b) => $"Ø CPU-Temperatur beim Spielen: letzte 7 Tage {r:0} °C, Woche davor {b:0} °C ({(r - b >= 0 ? "+" : "")}{r - b:0.0} °C).",
            ({ } r, null) => $"Ø CPU-Temperatur beim Spielen (letzte 7 Tage): {r:0} °C. Vergleich mit der Vorwoche folgt, sobald Daten da sind.",
            _ => "Noch keine Spieldaten – der Verlauf füllt sich automatisch, sobald gespielt wird.",
        };

        // SSD-Zustand: aktueller Stand + geschrieben in den letzten 30 Tagen
        Drives.Clear();
        var last = _history.Days.LastOrDefault();
        if (last is null) return;
        foreach (var d in last.Drives)
        {
            var old = _history.Days.Where(x => x.Date >= today.AddDays(-30))
                                   .SelectMany(x => x.Drives).FirstOrDefault(x => x.Name == d.Name && x.WrittenGb is not null);
            var recentGb = d.WrittenGb - old?.WrittenGb;
            Drives.Add(new DriveHealthRow(
                d.Name,
                d.LifePct is { } l ? $"{l:0} % Lebensdauer übrig" : "Lebensdauer unbekannt",
                (d.LifePct ?? 0) / 100,
                d.WrittenGb is { } w ? $"{w / 1000:0.0} TB geschrieben" : "",
                d.PowerOnHours is { } hrs ? $"{hrs:N0} Betriebsstunden" : "",
                recentGb is { } r && r > 0 ? $"{r:0} GB in den letzten 30 Tagen" : ""));
        }
    }

    public ObservableCollection<PlaytimeRow> Rows { get; } = new();
    public ICommand ResetCommand { get; }
    public ICommand RefreshCommand { get; }
    public string TotalText { get => _totalText; private set => SetProperty(ref _totalText, value); }
    public string SessionsText { get => _sessionsText; private set => SetProperty(ref _sessionsText, value); }
    public bool HasRows => Rows.Count > 0;

    public void Refresh()
    {
        _lastRefresh = DateTime.Now;
        var entries = _playtime.Entries;
        var max = entries.Select(e => e.Seconds).DefaultIfEmpty(1).Max();
        Rows.Clear();
        foreach (var e in entries)
        {
            Rows.Add(new PlaytimeRow(
                e.Game,
                FormatHours(e.Seconds),
                $"zuletzt {e.LastPlayed:dd.MM.yyyy HH:mm}",
                e.AvgFps is { } f ? $"Ø {f:0} FPS" : "",
                max > 0 ? e.Seconds / max : 0));
        }
        TotalText = $"{FormatHours(entries.Sum(e => e.Seconds))} gespielt · {entries.Count} Spiele";

        try
        {
            var sessions = SessionStore.List();
            var energy = sessions.Sum(s => s.EnergyWh ?? 0);
            var cost = sessions.Sum(s => s.CostEuro ?? 0);
            SessionsText = $"{sessions.Count} Aufnahmen · {SessionItemViewModel.FormatDuration(sessions.Sum(s => s.DurationSeconds))} aufgezeichnet"
                           + (energy > 0 ? $" · {energy / 1000:0.00} kWh CPU+GPU · {cost:0.00} €" : "");
        }
        catch
        {
            SessionsText = "";
        }
        OnPropertyChanged(nameof(HasRows));
    }

    public static string FormatHours(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : $"{t.Minutes} min";
    }
}

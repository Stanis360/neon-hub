using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NeonMonitor.Services.Comfort;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.ViewModels;

/// <summary>Zeile der Spielzeit-Statistik.</summary>
public sealed record PlaytimeRow(string Game, string TimeText, string LastPlayedText, string FpsText, double Fraction);

/// <summary>Reiter „Statistik“: Spielzeit pro Spiel, Gesamtwerte über alle Aufnahmen.</summary>
public sealed class StatsViewModel : ObservableObject
{
    private readonly PlaytimeService _playtime;
    private DateTime _lastRefresh = DateTime.MinValue;
    private string _totalText = "", _sessionsText = "";

    public StatsViewModel(PlaytimeService playtime)
    {
        _playtime = playtime;
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
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
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

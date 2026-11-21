using System.Windows;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services.Comfort;

namespace NeonMonitor.ViewModels;

/// <summary>Reiter „Einstellungen“: Autostart, Infobereich, automatische Aufnahme, Warnungen, Strompreis.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _s;
    private bool _autostart;

    public SettingsViewModel(AppSettings settings, UpdateViewModel update, WebViewModel web, Action requestRestart)
    {
        _s = settings;
        Themes = NeonMonitor.Themes.ThemeCatalog.All
            .Select(t => new ThemeChoiceViewModel(t, t.Id == settings.Theme, SelectTheme)).ToList();
        RestartCommand = new RelayCommand(requestRestart);
        Update = update;
        Web = web;
        _autostart = AutostartService.IsEnabled();

        PriceUp = new RelayCommand(() => Price += 0.01);
        PriceDown = new RelayCommand(() => Price -= 0.01);
        CpuUp = new RelayCommand(() => AlertCpu += 5);
        CpuDown = new RelayCommand(() => AlertCpu -= 5);
        GpuUp = new RelayCommand(() => AlertGpu += 5);
        GpuDown = new RelayCommand(() => AlertGpu -= 5);
        HotUp = new RelayCommand(() => AlertHot += 5);
        HotDown = new RelayCommand(() => AlertHot -= 5);
        OpenDocumentsCommand = new RelayCommand(() => Shell.Open(AppPaths.DocumentsDir));
        OpenAppDataCommand = new RelayCommand(() => Shell.Open(AppPaths.AppDataDir));
    }

    /// <summary>Update-Bereich (gleiches Objekt wie das Banner im Hauptfenster).</summary>
    public UpdateViewModel Update { get; }

    /// <summary>Handy-Ansicht.</summary>
    public WebViewModel Web { get; }

    // ---------------- Design ----------------

    /// <summary>Alle Designs mit Vorschaufarben.</summary>
    public IReadOnlyList<ThemeChoiceViewModel> Themes { get; }
    public ICommand RestartCommand { get; }

    /// <summary>Wurde ein anderes Design gewählt als das geladene? → Neustart nötig.</summary>
    public bool ThemeChangePending => _s.Theme != NeonMonitor.Themes.ThemeManager.Current.Id;

    // ---------------- Hintergrund-Effekt ----------------

    /// <summary>Tatsächlich angezeigter Effekt (für den Hintergrund im Hauptfenster); wirkt sofort.</summary>
    public string ActiveEffect => NeonMonitor.Themes.BackgroundEffects.Resolve(_s.BackgroundEffect, NeonMonitor.Themes.ThemeManager.Current);

    public bool EffectDesign { get => IsEffect(NeonMonitor.Themes.BackgroundEffects.FromTheme); set { if (value) SetEffect(NeonMonitor.Themes.BackgroundEffects.FromTheme); } }
    public bool EffectGrid { get => IsEffect(NeonMonitor.Themes.BackgroundEffects.Grid); set { if (value) SetEffect(NeonMonitor.Themes.BackgroundEffects.Grid); } }
    public bool EffectMeteors { get => IsEffect(NeonMonitor.Themes.BackgroundEffects.Meteors); set { if (value) SetEffect(NeonMonitor.Themes.BackgroundEffects.Meteors); } }
    public bool EffectLightning { get => IsEffect(NeonMonitor.Themes.BackgroundEffects.Lightning); set { if (value) SetEffect(NeonMonitor.Themes.BackgroundEffects.Lightning); } }
    public bool EffectAura { get => IsEffect(NeonMonitor.Themes.BackgroundEffects.Aura); set { if (value) SetEffect(NeonMonitor.Themes.BackgroundEffects.Aura); } }
    public bool EffectNone { get => IsEffect(NeonMonitor.Themes.BackgroundEffects.None); set { if (value) SetEffect(NeonMonitor.Themes.BackgroundEffects.None); } }

    private bool IsEffect(string id) => id == NeonMonitor.Themes.BackgroundEffects.FromTheme
        ? !NeonMonitor.Themes.BackgroundEffects.IsExplicit(_s.BackgroundEffect)
        : _s.BackgroundEffect == id;

    private void SetEffect(string id)
    {
        _s.BackgroundEffect = id;
        Save();
        foreach (var n in new[] { nameof(EffectDesign), nameof(EffectGrid), nameof(EffectMeteors), nameof(EffectLightning),
                                  nameof(EffectAura), nameof(EffectNone), nameof(ActiveEffect) })
            OnPropertyChanged(n);
    }

    private void SelectTheme(ThemeChoiceViewModel choice)
    {
        _s.Theme = choice.Id;
        _s.Save();
        foreach (var t in Themes) t.IsSelected = t == choice;
        OnPropertyChanged(nameof(ThemeChangePending));
    }

    /// <summary>Mit Windows starten (Aufgabenplanung, ohne UAC-Abfrage, minimiert im Infobereich).</summary>
    public bool Autostart
    {
        get => _autostart;
        set
        {
            if (value == _autostart) return;
            var ok = value ? AutostartService.Enable(out var err) : AutostartService.Disable(out err);
            if (!ok)
            {
                MessageBox.Show($"Autostart konnte nicht {(value ? "eingerichtet" : "entfernt")} werden:\n{err}",
                    "Autostart", MessageBoxButton.OK, MessageBoxImage.Warning);
                OnPropertyChanged();
                return;
            }
            _autostart = value;
            OnPropertyChanged();
        }
    }

    public bool CloseToTray { get => _s.CloseToTray; set => Set(() => _s.CloseToTray = value); }
    public bool MinimizeToTray { get => _s.MinimizeToTray; set => Set(() => _s.MinimizeToTray = value); }
    public bool AutoRecord { get => _s.AutoRecord; set => Set(() => _s.AutoRecord = value); }
    public bool AlertsEnabled { get => _s.AlertsEnabled; set => Set(() => _s.AlertsEnabled = value); }

    // Benchmark-Dauer als RadioButtons
    public bool Bench30 { get => _s.BenchmarkSeconds == 30; set { if (value) SetBench(30); } }
    public bool Bench60 { get => _s.BenchmarkSeconds == 60; set { if (value) SetBench(60); } }
    public bool Bench120 { get => _s.BenchmarkSeconds == 120; set { if (value) SetBench(120); } }
    public bool Bench300 { get => _s.BenchmarkSeconds == 300; set { if (value) SetBench(300); } }

    public double Price
    {
        get => _s.PricePerKwh;
        set { _s.PricePerKwh = Math.Round(Math.Clamp(value, 0, 2), 2); Save(); OnPropertyChanged(); OnPropertyChanged(nameof(PriceText)); }
    }
    public string PriceText => $"{Price:0.00} € / kWh";

    public double AlertCpu { get => _s.AlertCpuTemp; set { _s.AlertCpuTemp = Math.Clamp(value, 60, 100); Save(); OnPropertyChanged(); OnPropertyChanged(nameof(AlertCpuText)); } }
    public double AlertGpu { get => _s.AlertGpuTemp; set { _s.AlertGpuTemp = Math.Clamp(value, 60, 100); Save(); OnPropertyChanged(); OnPropertyChanged(nameof(AlertGpuText)); } }
    public double AlertHot { get => _s.AlertHotspot; set { _s.AlertHotspot = Math.Clamp(value, 70, 115); Save(); OnPropertyChanged(); OnPropertyChanged(nameof(AlertHotText)); } }
    public string AlertCpuText => $"{AlertCpu:0} °C";
    public string AlertGpuText => $"{AlertGpu:0} °C";
    public string AlertHotText => $"{AlertHot:0} °C";

    public ICommand PriceUp { get; }
    public ICommand PriceDown { get; }
    public ICommand CpuUp { get; }
    public ICommand CpuDown { get; }
    public ICommand GpuUp { get; }
    public ICommand GpuDown { get; }
    public ICommand HotUp { get; }
    public ICommand HotDown { get; }
    public ICommand OpenDocumentsCommand { get; }
    public ICommand OpenAppDataCommand { get; }

    public string VersionText => $"NeonMonitor {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3)}";

    private void SetBench(int seconds)
    {
        _s.BenchmarkSeconds = seconds;
        Save();
        foreach (var n in new[] { nameof(Bench30), nameof(Bench60), nameof(Bench120), nameof(Bench300) }) OnPropertyChanged(n);
    }

    private void Set(Action apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        apply();
        Save();
        OnPropertyChanged(name);
    }

    private void Save() => _s.Save();
}

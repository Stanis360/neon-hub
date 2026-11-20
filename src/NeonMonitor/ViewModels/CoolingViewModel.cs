using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Sensors;

namespace NeonMonitor.ViewModels;

/// <summary>Ein Lüfteranschluss mit eigenem Namen und Rolle (CPU / Gehäuse).</summary>
public sealed class FanViewModel : ObservableObject
{
    private readonly CoolingViewModel _owner;
    private string _name;
    private double _rpm, _maxSeen = 1500;

    public FanViewModel(string id, string defaultName, CoolingViewModel owner)
    {
        Id = id;
        DefaultName = defaultName;
        _owner = owner;
        _name = owner.CustomName(id) ?? defaultName;
        SetCpuCommand = new RelayCommand(() => owner.SetRole(this, cpu: true));
        SetCaseCommand = new RelayCommand(() => owner.SetRole(this, cpu: false));
    }

    public string Id { get; }
    /// <summary>Name laut Sensor, z. B. „Fan #2“.</summary>
    public string DefaultName { get; }
    public ICommand SetCpuCommand { get; }
    public ICommand SetCaseCommand { get; }

    /// <summary>Anzeigename – frei änderbar (leer = Standardname).</summary>
    public string Name
    {
        get => _name;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? DefaultName : value.Trim();
            if (SetProperty(ref _name, v)) _owner.RenameFan(Id, v == DefaultName ? null : v);
        }
    }

    public double Rpm => _rpm;
    public string RpmText => _rpm < 1 ? "steht / nicht belegt" : $"{_rpm:0} U/min";
    public double Fraction => _rpm / _maxSeen;
    public bool IsActive => _rpm > 0;
    public bool IsCpu => _owner.CpuFanId == Id;
    public bool IsCase => _owner.CaseFanId == Id;
    public string RoleText => IsCpu ? "CPU-Lüfter" : IsCase ? "Gehäuselüfter" : "";

    public void Update(double rpm)
    {
        _rpm = rpm;
        _maxSeen = Math.Max(_maxSeen, rpm);
        OnPropertyChanged(nameof(Rpm));
        OnPropertyChanged(nameof(RpmText));
        OnPropertyChanged(nameof(Fraction));
        OnPropertyChanged(nameof(IsActive));
    }

    public void RefreshRole()
    {
        OnPropertyChanged(nameof(IsCpu));
        OnPropertyChanged(nameof(IsCase));
        OnPropertyChanged(nameof(RoleText));
    }
}

/// <summary>Eine zusätzliche Temperatur (Mainboard, SSD, RAM).</summary>
public sealed class TempItemViewModel(string group, string name) : ObservableObject
{
    private double _value;
    public string Group { get; } = group;
    public string Name { get; } = name;
    public string Key => Group + "|" + Name;
    public string ValueText => $"{_value:0} °C";
    public double Fraction => _value / 100.0;
    public bool IsHot => Group == "SSD" ? _value >= 70 : _value >= 80;

    public void Update(double v)
    {
        if (Math.Abs(v - _value) < 0.05) return;
        _value = v;
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(Fraction));
        OnPropertyChanged(nameof(IsHot));
    }
}

/// <summary>Ein steuerbarer Lüfterkanal mit Modus (BIOS / fest / Kurve).</summary>
public sealed class FanChannelViewModel : ObservableObject
{
    private readonly CoolingViewModel _owner;
    private readonly FanCurveConfig _config;
    private string _title;
    private string _currentText = "";

    public FanChannelViewModel(string id, string title, FanCurveConfig config, CoolingViewModel owner)
    {
        Id = id;
        _title = title;
        _config = config;
        _owner = owner;
        MoreCommand = new RelayCommand(() => FixedPercent += 5);
        LessCommand = new RelayCommand(() => FixedPercent -= 5);
    }

    public string Id { get; }
    public FanCurveConfig Config => _config;
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string CurrentText { get => _currentText; private set => SetProperty(ref _currentText, value); }
    public ICommand MoreCommand { get; }
    public ICommand LessCommand { get; }

    // --- Modus (für RadioButtons als Chips) ---
    public bool IsBios { get => _config.Mode == "bios"; set { if (value) SetMode("bios"); } }
    public bool IsFixed { get => _config.Mode == "fixed"; set { if (value) SetMode("fixed"); } }
    public bool IsCurve { get => _config.Mode == "curve"; set { if (value) SetMode("curve"); } }

    public bool IsQuiet { get => _config.Preset == "quiet"; set { if (value) SetPreset("quiet"); } }
    public bool IsBalanced { get => _config.Preset == "balanced"; set { if (value) SetPreset("balanced"); } }
    public bool IsPerformance { get => _config.Preset == "performance"; set { if (value) SetPreset("performance"); } }

    public bool IsSourceCpu { get => _config.Source == "cpu"; set { if (value) SetSource("cpu"); } }
    public bool IsSourceMax { get => _config.Source == "max"; set { if (value) SetSource("max"); } }

    public double FixedPercent
    {
        get => _config.FixedPercent;
        set
        {
            _config.FixedPercent = Math.Clamp(Math.Round(value), FanControlEngine.HardMinimumPercent, 100);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FixedText));
            _owner.SaveCurves();
        }
    }

    public string FixedText => $"{FixedPercent:0} %";

    /// <summary>Kurve als Text, z. B. „40° → 30 % · 60° → 45 % …“.</summary>
    public string CurveText => string.Join("  ·  ",
        FanControlEngine.Presets[_config.Preset].Select(p => $"{p.Temp:0}° → {p.Percent:0} %"));

    public void Update(FanControlReading? reading)
    {
        CurrentText = reading?.Percent is { } p
            ? $"aktuell {p:0} %" + (reading.ControlledByUs ? " · geregelt von NeonMonitor" : " · Mainboard regelt")
            : "";
    }

    private void SetMode(string mode)
    {
        _config.Mode = mode;
        foreach (var n in new[] { nameof(IsBios), nameof(IsFixed), nameof(IsCurve) }) OnPropertyChanged(n);
        _owner.SaveCurves();
    }

    private void SetPreset(string preset)
    {
        _config.Preset = preset;
        foreach (var n in new[] { nameof(IsQuiet), nameof(IsBalanced), nameof(IsPerformance), nameof(CurveText) }) OnPropertyChanged(n);
        _owner.SaveCurves();
    }

    private void SetSource(string source)
    {
        _config.Source = source;
        OnPropertyChanged(nameof(IsSourceCpu));
        OnPropertyChanged(nameof(IsSourceMax));
        _owner.SaveCurves();
    }
}

/// <summary>Reiter „Kühlung“: Lüfter, Temperaturen, Lüftersteuerung.</summary>
public sealed partial class CoolingViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly FanControlEngine _engine;
    private bool _hasBoard;

    public CoolingViewModel(AppSettings settings, FanControlEngine engine)
    {
        _settings = settings;
        _engine = engine;
    }

    public ObservableCollection<FanViewModel> Fans { get; } = new();
    public ObservableCollection<TempItemViewModel> Temps { get; } = new();
    public ObservableCollection<FanChannelViewModel> Channels { get; } = new();

    /// <summary>Die wichtigsten Temperaturen für das Live-Dashboard (max. 4).</summary>
    public ObservableCollection<TempItemViewModel> TopTemps { get; } = new();

    public string? CpuFanId => _settings.CpuFanId ?? AutoCpuFanId;
    public string? CaseFanId => _settings.CaseFanId ?? AutoCaseFanId;
    private string? AutoCpuFanId { get; set; }
    private string? AutoCaseFanId { get; set; }

    public bool HasBoard { get => _hasBoard; private set => SetProperty(ref _hasBoard, value); }

    public string NoBoardHint => _settings.SafeMode
        ? "Sicherer Modus aktiv – Mainboard-Lüfter brauchen den PawnIO-Treiber und sind deshalb aus."
        : "Keine Mainboard-Sensoren gefunden. Prüfe, ob PawnIO installiert ist und NeonMonitor als Administrator läuft (Sensorbericht).";

    /// <summary>Hauptschalter der Lüftersteuerung (mit Sicherheitsabfrage beim Einschalten).</summary>
    public bool ControlEnabled
    {
        get => _settings.FanControlEnabled;
        set
        {
            if (value == _settings.FanControlEnabled) return;
            if (value)
            {
                var ok = MessageBox.Show(
                    "NeonMonitor übernimmt die Regelung der Mainboard-Lüfter.\n\n" +
                    "Sicherheitsnetz: nie unter 20 %, Vollgas ab 90 °C CPU oder 105 °C Hotspot, " +
                    "beim Beenden regelt wieder das Mainboard.\n\n" +
                    "Wichtig: Stürzt NeonMonitor ab, kann ein Lüfter auf der zuletzt gesetzten Drehzahl bleiben, " +
                    "bis du NeonMonitor neu startest oder den PC neu startest.\n\nFortfahren?",
                    "Lüftersteuerung einschalten", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ok != MessageBoxResult.Yes)
                {
                    OnPropertyChanged(); // Schalter zurückspringen lassen
                    return;
                }
            }
            _settings.FanControlEnabled = value;
            _engine.Enabled = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string? CustomName(string id) => _settings.FanNames.TryGetValue(id, out var n) ? n : null;

    public void RenameFan(string id, string? name)
    {
        if (name is null) _settings.FanNames.Remove(id);
        else _settings.FanNames[id] = name;
        _settings.Save();
        foreach (var c in Channels) c.Title = ChannelTitle(c.Id, c.Title);
    }

    public void SetRole(FanViewModel fan, bool cpu)
    {
        if (cpu)
        {
            _settings.CpuFanId = fan.Id;
            if (_settings.CaseFanId == fan.Id) _settings.CaseFanId = null;
        }
        else
        {
            _settings.CaseFanId = fan.Id;
            if (_settings.CpuFanId == fan.Id) _settings.CpuFanId = null;
        }
        _settings.Save();
        foreach (var f in Fans) f.RefreshRole();
    }

    public void SaveCurves()
    {
        _settings.FanCurves = Channels.Select(c => c.Config.Clone()).ToList();
        _settings.Save();
        _engine.SetConfigs(_settings.FanCurves);
    }

    /// <summary>Neuen Snapshot übernehmen (UI-Thread).</summary>
    public void Apply(HardwareSnapshot s)
    {
        HasBoard = s.Fans.Count > 0 || s.FanControls.Count > 0;

        foreach (var f in s.Fans)
        {
            var vm = Fans.FirstOrDefault(x => x.Id == f.Id);
            if (vm is null) Fans.Add(vm = new FanViewModel(f.Id, f.Name, this));
            vm.Update(f.Rpm);
        }

        // automatische Zuordnung wie in der Sensorquelle (für die Anzeige)
        var active = Fans.Where(f => f.IsActive).ToList();
        var autoCpu = active.FirstOrDefault(f => f.DefaultName.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                      ?? active.FirstOrDefault(f => f.DefaultName == "Fan #2") ?? active.FirstOrDefault();
        var autoCase = active.FirstOrDefault(f => f.Id != (CpuFanId ?? autoCpu?.Id));
        if (AutoCpuFanId != autoCpu?.Id || AutoCaseFanId != autoCase?.Id)
        {
            AutoCpuFanId = autoCpu?.Id;
            AutoCaseFanId = autoCase?.Id;
            foreach (var f in Fans) f.RefreshRole();
        }

        foreach (var t in s.ExtraTemps)
        {
            var key = t.Group + "|" + t.Name;
            var vm = Temps.FirstOrDefault(x => x.Key == key);
            if (vm is null)
            {
                Temps.Add(vm = new TempItemViewModel(t.Group, t.Name));
                RebuildTopTemps();
            }
            vm.Update(t.Celsius);
        }

        foreach (var c in s.FanControls)
        {
            var vm = Channels.FirstOrDefault(x => x.Id == c.Id);
            if (vm is null)
            {
                var config = _settings.FanCurves.FirstOrDefault(x => x.ControlId == c.Id)?.Clone()
                             ?? new FanCurveConfig { ControlId = c.Id, Source = ChannelTitle(c.Id, c.Name).Contains("CPU") ? "cpu" : "max" };
                Channels.Add(vm = new FanChannelViewModel(c.Id, ChannelTitle(c.Id, c.Name), config, this));
            }
            vm.Update(c);
        }
    }

    /// <summary>„Fan Control #2“ ↔ Lüfter „Fan #2“: Titel = (eigener) Name des passenden Lüfters.</summary>
    private string ChannelTitle(string controlId, string fallback)
    {
        var number = NumberRegex().Match(controlId.Split('/').LastOrDefault() ?? "");
        var fan = Fans.FirstOrDefault(f => f.Id.Contains("/fan/") && number.Success
                                           && f.Id.Split('/').Last() == number.Value);
        return fan is null ? fallback : $"{fan.Name}  ({fan.DefaultName})";
    }

    private void RebuildTopTemps()
    {
        TopTemps.Clear();
        int Priority(TempItemViewModel t) => t.Group switch { "SSD" => 0, "RAM" => 1, _ => 2 };
        foreach (var t in Temps.OrderBy(Priority).Take(4)) TopTemps.Add(t);
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumberRegex();
}

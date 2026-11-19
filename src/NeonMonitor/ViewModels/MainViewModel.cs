using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services;

namespace NeonMonitor.ViewModels;

/// <summary>
/// ViewModel des Hauptfensters: übernimmt die Snapshots des <see cref="HardwareMonitorService"/>
/// auf den UI-Thread und verteilt sie auf die einzelnen <see cref="MetricViewModel"/>s.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private static readonly int[] Intervals = [500, 1000, 2000];

    private readonly HardwareMonitorService _monitor;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;

    private string _cpuName = "CPU wird erkannt …";
    private string _gpuName = "GPU wird erkannt …";
    private string _ramTotalText = "";
    private string _vramTotalText = "";
    private string _gpuFanText = "";
    private string _sensorStatus = "Sensoren werden initialisiert …";
    private string _warnings = "";
    private bool _isInitializing = true;

    public MainViewModel(HardwareMonitorService monitor, AppSettings settings)
    {
        _monitor = monitor;
        _settings = settings;
        _dispatcher = Application.Current.Dispatcher;

        // Verlaufslänge in Punkten = gewünschte Sekunden / Intervall
        var points = HistoryPoints();

        // Warnschwellen: 9800X3D drosselt bei 95 °C, RX 9070 XT Hotspot-Limit ~110 °C
        // barMaximum = Vollausschlag der Balken (passend zu 9800X3D / RX 9070 XT, wächst bei Bedarf mit)
        CpuLoad = new("Last", "%", "0", points);
        CpuTemp = new("Temperatur", "°C", "0", points, warnAt: 80, criticalAt: 90, showPeak: true);
        CpuPower = new("Leistung", "W", "0", points, barMaximum: 150);
        CpuClock = new("Takt Ø alle Kerne", "MHz", "0", points, barMaximum: 5500);
        CpuClockMax = new("Takt schnellster Kern", "MHz", "0", points, barMaximum: 5500);

        GpuLoad = new("Last", "%", "0", points);
        GpuTemp = new("Temperatur", "°C", "0", points, warnAt: 80, criticalAt: 90, showPeak: true);
        GpuHotspot = new("Hotspot", "°C", "0", points, warnAt: 95, criticalAt: 105, barMaximum: 110, showPeak: true);
        GpuClock = new("GPU-Takt", "MHz", "0", points, barMaximum: 3200);
        GpuMemClock = new("Speichertakt", "MHz", "0", points, barMaximum: 2600);
        GpuVram = new("VRAM", "GB", "0.0", points, barMaximum: 16);
        GpuPower = new("Leistung", "W", "0", points, barMaximum: 340);

        RamUsed = new("Belegt", "GB", "0.0", points, barMaximum: 32);
        RamLoad = new("Auslastung", "%", "0", points, warnAt: 85, criticalAt: 95);

        Fps = new FpsViewModel(monitor.Fps, settings, points);

        IsAdmin = SystemInfo.IsAdministrator();
        IsPawnIoInstalled = SystemInfo.IsPawnIoInstalled();
        UpdateWarnings();

        CreateReportCommand = new RelayCommand(CreateReport, () => !IsInitializing);
        CycleIntervalCommand = new RelayCommand(CycleInterval);
        ResetPeaksCommand = new RelayCommand(ResetPeaks);
        OpenDataFolderCommand = new RelayCommand(() => OpenPath(AppPaths.DocumentsDir));

        _monitor.Initialized += (_, _) => _dispatcher.BeginInvoke(OnMonitorInitialized);
        _monitor.SnapshotReady += (_, s) => _dispatcher.BeginInvoke(() => Apply(s));
    }

    // ---------------- Messwerte ----------------

    public MetricViewModel CpuLoad { get; }
    public MetricViewModel CpuTemp { get; }
    public MetricViewModel CpuPower { get; }
    public MetricViewModel CpuClock { get; }
    public MetricViewModel CpuClockMax { get; }

    public MetricViewModel GpuLoad { get; }
    public MetricViewModel GpuTemp { get; }
    public MetricViewModel GpuHotspot { get; }
    public MetricViewModel GpuClock { get; }
    public MetricViewModel GpuMemClock { get; }
    public MetricViewModel GpuVram { get; }
    public MetricViewModel GpuPower { get; }

    public MetricViewModel RamUsed { get; }
    public MetricViewModel RamLoad { get; }

    /// <summary>FPS-Seite und FPS-Zeile im Dashboard.</summary>
    public FpsViewModel Fps { get; }

    public ObservableCollection<DriveViewModel> Drives { get; } = new();

    /// <summary>Takt pro CPU-Kern (beim 9800X3D: 8 Einträge).</summary>
    public ObservableCollection<CoreClockViewModel> CoreClocks { get; } = new();

    public string CpuName { get => _cpuName; private set => SetProperty(ref _cpuName, value); }
    public string GpuName { get => _gpuName; private set => SetProperty(ref _gpuName, value); }
    public string RamTotalText { get => _ramTotalText; private set => SetProperty(ref _ramTotalText, value); }
    public string VramTotalText { get => _vramTotalText; private set => SetProperty(ref _vramTotalText, value); }
    public string GpuFanText { get => _gpuFanText; private set => SetProperty(ref _gpuFanText, value); }

    // ---------------- Status & Einstellungen ----------------

    public bool IsAdmin { get; }
    public bool IsPawnIoInstalled { get; }

    public bool IsInitializing
    {
        get => _isInitializing;
        private set => SetProperty(ref _isInitializing, value);
    }

    public string SensorStatus { get => _sensorStatus; private set => SetProperty(ref _sensorStatus, value); }

    /// <summary>Hinweise für das Warnbanner (leer = Banner unsichtbar).</summary>
    public string Warnings
    {
        get => _warnings;
        private set
        {
            if (SetProperty(ref _warnings, value)) OnPropertyChanged(nameof(HasWarnings));
        }
    }

    public bool HasWarnings => Warnings.Length > 0;

    public bool BackgroundAnimation
    {
        get => _settings.BackgroundAnimation;
        set
        {
            if (_settings.BackgroundAnimation == value) return;
            _settings.BackgroundAnimation = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string IntervalText => $"Intervall: {_settings.PollIntervalMs / 1000.0:0.#} s";

    public ICommand CreateReportCommand { get; }
    public ICommand CycleIntervalCommand { get; }
    public ICommand ResetPeaksCommand { get; }
    public ICommand OpenDataFolderCommand { get; }

    // ---------------- Logik ----------------

    private int HistoryPoints() => Math.Max(10, _settings.HistorySeconds * 1000 / _settings.PollIntervalMs);

    private void OnMonitorInitialized()
    {
        IsInitializing = false;
        SensorStatus = string.Join("   ·   ", _monitor.Sources.Select(s => $"{s.Name}: {(s.IsAvailable ? s.Status : "nicht verfügbar")}"));
        UpdateWarnings();
        CommandManager.InvalidateRequerySuggested();
    }

    private void Apply(HardwareSnapshot s)
    {
        if (s.CpuName is not null) CpuName = s.CpuName;
        if (s.GpuName is not null) GpuName = s.GpuName;

        CpuLoad.Update(s.CpuLoad);
        CpuTemp.Update(s.CpuTemp);
        CpuPower.Update(s.CpuPower);
        CpuClock.Update(s.CpuClockAvg);
        CpuClockMax.Update(s.CpuClockMax);

        GpuLoad.Update(s.GpuLoad);
        GpuTemp.Update(s.GpuTemp);
        GpuHotspot.Update(s.GpuHotspot);
        GpuClock.Update(s.GpuClock);
        GpuMemClock.Update(s.GpuMemClock);
        GpuVram.Update(s.GpuVramUsedMb / 1024.0);
        if (s.GpuVramTotalMb is { } vramTotal) GpuVram.BarMaximum = Math.Round(vramTotal / 1024.0);
        GpuPower.Update(s.GpuPower);
        VramTotalText = s.GpuVramTotalMb is { } vt ? $"von {vt / 1024.0:0} GB" : "";
        GpuFanText = s.GpuFan switch
        {
            null => "",
            < 1 => "Lüfter steht (Zero-RPM)",
            var fan => $"Lüfter {fan:0} U/min",
        };

        RamUsed.Update(s.RamUsedGb);
        RamLoad.Update(s.RamLoad);
        RamTotalText = s.RamTotalGb is { } rt ? $"von {rt:0.0} GB" : "";
        if (s.RamTotalGb is { } ramTotal) RamUsed.BarMaximum = Math.Round(ramTotal, 1);

        Fps.Apply(s);
        UpdateCoreClocks(s.CpuCoreClocks);
        UpdateDrives(s.Drives);
    }

    /// <summary>Kernliste aktualisieren; wird beim ersten Snapshot einmalig aufgebaut.</summary>
    private void UpdateCoreClocks(IReadOnlyList<CoreClock> cores)
    {
        if (cores.Count != CoreClocks.Count)
        {
            CoreClocks.Clear();
            foreach (var c in cores) CoreClocks.Add(new CoreClockViewModel(c.Name, 5500));
        }
        for (var i = 0; i < cores.Count; i++) CoreClocks[i].Update(cores[i].Mhz);
    }

    /// <summary>Laufwerksliste aktualisieren, ohne die Collection neu aufzubauen (kein Flackern).</summary>
    private void UpdateDrives(IReadOnlyList<DriveUsage> drives)
    {
        foreach (var d in drives)
        {
            var vm = Drives.FirstOrDefault(x => x.Name == d.Name);
            if (vm is null)
            {
                vm = new DriveViewModel(d.Name, d.Label);
                Drives.Add(vm);
            }
            vm.Update(d);
        }
        for (var i = Drives.Count - 1; i >= 0; i--)
            if (drives.All(d => d.Name != Drives[i].Name)) Drives.RemoveAt(i);
    }

    private void UpdateWarnings()
    {
        var w = new List<string>();
        if (!IsAdmin)
            w.Add("Keine Adminrechte – CPU-Temperatur/-Leistung fehlen. Programm als Administrator starten.");
        if (!IsPawnIoInstalled && !_settings.SafeMode)
            w.Add("PawnIO-Treiber fehlt – CPU-Temperatur, -Takt und -Leistung sind nicht verfügbar. Installation: winget install namazso.PawnIO");
        if (!IsInitializing && !_monitor.Fps.IsAvailable)
            w.Add("PresentMon fehlt – FPS sind nicht verfügbar. Bitte Installieren.bat erneut ausführen.");
        if (_settings.SafeMode)
            w.Add("Sicherer Modus aktiv: ohne PawnIO-Treiber, daher keine CPU-Temperatur/-Leistung/-Takte (umschalten im Reiter FPS).");
        if (!IsInitializing && _monitor.Sources.FirstOrDefault()?.IsAvailable == false)
            w.Add("LibreHardwareMonitor konnte nicht starten – es laufen nur die Windows-Fallbacks. Details im Log.");
        Warnings = string.Join(Environment.NewLine, w);
    }

    private void CycleInterval()
    {
        var idx = Array.IndexOf(Intervals, _settings.PollIntervalMs);
        _settings.PollIntervalMs = Intervals[(idx + 1) % Intervals.Length];
        _settings.Save();
        _monitor.IntervalMs = _settings.PollIntervalMs;

        var points = HistoryPoints();
        foreach (var m in AllMetrics()) m.History.Resize(points);
        Fps.Resize(points);
        OnPropertyChanged(nameof(IntervalText));
    }

    private void ResetPeaks()
    {
        foreach (var m in AllMetrics()) m.ResetPeak();
        Fps.ResetPeaks();
    }

    private IEnumerable<MetricViewModel> AllMetrics() =>
    [
        CpuLoad, CpuTemp, CpuPower, CpuClock, CpuClockMax,
        GpuLoad, GpuTemp, GpuHotspot, GpuClock, GpuMemClock, GpuVram, GpuPower,
        RamUsed, RamLoad,
    ];

    /// <summary>Schreibt den Sensorbericht nach Dokumente\NeonMonitor\Berichte und öffnet ihn.</summary>
    private async void CreateReport()
    {
        try
        {
            var text = await Task.Run(_monitor.CreateSensorReport);
            var file = Path.Combine(AppPaths.ReportsDir, $"sensorbericht-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            await File.WriteAllTextAsync(file, text);
            OpenPath(file);
        }
        catch (Exception ex)
        {
            Log.Error("Sensorbericht fehlgeschlagen", ex);
            MessageBox.Show($"Sensorbericht konnte nicht erstellt werden:\n{ex.Message}", "NeonMonitor",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn($"Öffnen von '{path}' fehlgeschlagen: {ex.Message}"); }
    }
}

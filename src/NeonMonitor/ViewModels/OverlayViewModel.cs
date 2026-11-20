using System.Collections.ObjectModel;
using System.Windows.Input;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.ViewModels;

/// <summary>Ecke eines Bildschirms, in die das Overlay gesetzt wird.</summary>
public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// Schnittstelle zum Overlay-Fenster. So bleibt das ViewModel frei von Fenster-Code
/// (umgesetzt von <c>Views.OverlayController</c>).
/// </summary>
public interface IOverlayHost
{
    void SetVisible(bool visible);
    void SetClickThrough(bool clickThrough);
    void MoveTo(MonitorInfo monitor, OverlayCorner corner);
}

/// <summary>Ein wählbarer Overlay-Eintrag (z. B. „GPU Temp“).</summary>
public sealed class OverlayItemViewModel : ObservableObject
{
    private bool _isEnabled;
    private readonly Action _changed;

    public OverlayItemViewModel(string key, string group, string label, MetricViewModel? metric, Action changed)
    {
        Key = key;
        Group = group;
        Label = label;
        Metric = metric;
        _changed = changed;
    }

    /// <summary>Schlüssel für die Einstellungen, z. B. „gputemp“.</summary>
    public string Key { get; }
    /// <summary>Gruppe für die Auswahlseite: FPS, CPU, GPU, RAM, Sonstiges.</summary>
    public string Group { get; }
    /// <summary>Kurzbeschriftung im Overlay.</summary>
    public string Label { get; }
    /// <summary>Messwert (null bei Sondereinträgen wie Spielname/Diagramm).</summary>
    public MetricViewModel? Metric { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value)) _changed();
        }
    }

    /// <summary>Setzt den Zustand, ohne die Änderungsbenachrichtigung auszulösen (beim Laden).</summary>
    public void Init(bool enabled) => _isEnabled = enabled;
}

/// <summary>Eine Gruppe von Einträgen auf der Auswahlseite.</summary>
public sealed record OverlayGroup(string Title, IReadOnlyList<OverlayItemViewModel> Items);

/// <summary>Auswahlknopf „Monitor 2“ für die Positionierung.</summary>
public sealed record MonitorChoice(MonitorInfo Monitor, ICommand PlaceTopLeft, ICommand PlaceTopRight,
                                   ICommand PlaceBottomLeft, ICommand PlaceBottomRight);

/// <summary>
/// Overlay: welche Werte, wie groß, wie durchsichtig, wo. Alle Änderungen werden sofort gespeichert.
/// </summary>
public sealed class OverlayViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private IOverlayHost? _host;

    public OverlayViewModel(MainViewModel main, AppSettings settings)
    {
        _settings = settings;
        Main = main;

        var all = new List<OverlayItemViewModel>
        {
            Item("game", "FPS", "Spiel", null),
            Item("fps", "FPS", "FPS", main.Fps.Fps),
            Item("low1", "FPS", "1% Low", main.Fps.Low1),
            Item("ft", "FPS", "Frametime", main.Fps.FrameTime),
            Item("ftmax", "FPS", "FT max", main.Fps.FrameTimeMax),
            Item("chart", "FPS", "Frametime-Diagramm", null),

            Item("cpuload", "CPU", "CPU", main.CpuLoad),
            Item("cputemp", "CPU", "CPU Temp", main.CpuTemp),
            Item("cpupower", "CPU", "CPU Watt", main.CpuPower),
            Item("cpuclock", "CPU", "CPU Takt", main.CpuClock),

            Item("gpuload", "GPU", "GPU", main.GpuLoad),
            Item("gputemp", "GPU", "GPU Temp", main.GpuTemp),
            Item("gpuhot", "GPU", "Hotspot", main.GpuHotspot),
            Item("gpumemtemp", "GPU", "VRAM Temp", main.GpuMemTemp),
            Item("gpupower", "GPU", "GPU Watt", main.GpuPower),
            Item("gpuclock", "GPU", "GPU Takt", main.GpuClock),
            Item("gpuvram", "GPU", "VRAM", main.GpuVram),

            Item("ramload", "RAM", "RAM", main.RamLoad),
            Item("ramused", "RAM", "RAM belegt", main.RamUsed),

            Item("fpsw", "FPS", "FPS/Watt", main.FpsPerWatt),

            Item("cpufan", "Kühlung", "CPU-Lüfter", main.CpuFan),
            Item("casefan", "Kühlung", "Gehäuse", main.CaseFan),

            Item("totalpower", "Sonstiges", "CPU+GPU W", main.TotalPower),
            Item("netdown", "Sonstiges", "Netz ↓", main.NetDown),
            Item("netup", "Sonstiges", "Netz ↑", main.NetUp),
            Item("clock", "Sonstiges", "Uhrzeit", null),
        };
        foreach (var i in all) i.Init(settings.OverlayItems.Contains(i.Key));
        AllItems = all;

        Groups = all.GroupBy(i => i.Group).Select(g => new OverlayGroup(g.Key, g.ToList())).ToList();
        RebuildVisible();

        ToggleCommand = new RelayCommand(() => IsVisible = !IsVisible);
        BiggerCommand = new RelayCommand(() => Scale += 0.1);
        SmallerCommand = new RelayCommand(() => Scale -= 0.1);
        MoreOpaqueCommand = new RelayCommand(() => BackgroundOpacity += 0.1);
        LessOpaqueCommand = new RelayCommand(() => BackgroundOpacity -= 0.1);
        RefreshMonitorsCommand = new RelayCommand(RefreshMonitors);
        RefreshMonitors();
    }

    /// <summary>Zugriff auf alle Messwerte (für das Overlay-Fenster).</summary>
    public MainViewModel Main { get; }

    public IReadOnlyList<OverlayItemViewModel> AllItems { get; }
    public IReadOnlyList<OverlayGroup> Groups { get; }

    /// <summary>Die eingeschalteten Messwert-Zeilen in Anzeigereihenfolge.</summary>
    public ObservableCollection<OverlayItemViewModel> VisibleMetrics { get; } = new();

    public ObservableCollection<MonitorChoice> Monitors { get; } = new();

    public bool ShowGame => IsOn("game");
    public bool ShowChart => IsOn("chart");
    public bool ShowClock => IsOn("clock");

    public ICommand ToggleCommand { get; }
    public ICommand BiggerCommand { get; }
    public ICommand SmallerCommand { get; }
    public ICommand MoreOpaqueCommand { get; }
    public ICommand LessOpaqueCommand { get; }
    public ICommand RefreshMonitorsCommand { get; }

    /// <summary>Wird vom Overlay-Controller nach dem Start gesetzt.</summary>
    public void AttachHost(IOverlayHost host)
    {
        _host = host;
        _host.SetClickThrough(ClickThrough);
        if (IsVisible) _host.SetVisible(true);
    }

    public bool IsVisible
    {
        get => _settings.OverlayVisible;
        set
        {
            if (_settings.OverlayVisible == value) return;
            _settings.OverlayVisible = value;
            _settings.Save();
            _host?.SetVisible(value);
            OnPropertyChanged();
        }
    }

    public bool ClickThrough
    {
        get => _settings.OverlayClickThrough;
        set
        {
            if (_settings.OverlayClickThrough == value) return;
            _settings.OverlayClickThrough = value;
            _settings.Save();
            _host?.SetClickThrough(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Vergrößerung 0,6 – 2,0.</summary>
    public double Scale
    {
        get => _settings.OverlayScale;
        set
        {
            value = Math.Round(Math.Clamp(value, 0.6, 2.0), 1);
            if (Math.Abs(_settings.OverlayScale - value) < 0.001) return;
            _settings.OverlayScale = value;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScaleText));
        }
    }

    public string ScaleText => $"{Scale * 100:0} %";

    /// <summary>Hintergrund-Deckkraft 0 – 1.</summary>
    public double BackgroundOpacity
    {
        get => _settings.OverlayBackgroundOpacity;
        set
        {
            value = Math.Round(Math.Clamp(value, 0, 1), 1);
            if (Math.Abs(_settings.OverlayBackgroundOpacity - value) < 0.001) return;
            _settings.OverlayBackgroundOpacity = value;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(OpacityText));
        }
    }

    public string OpacityText => $"{BackgroundOpacity * 100:0} %";

    /// <summary>Aktuelle Uhrzeit für das Overlay (wird mit jedem Messwert aktualisiert).</summary>
    public string ClockText { get; private set; } = "";

    public void Tick()
    {
        if (!ShowClock) return;
        ClockText = DateTime.Now.ToString("HH:mm");
        OnPropertyChanged(nameof(ClockText));
    }

    /// <summary>Position merken (vom Fenster nach dem Verschieben aufgerufen).</summary>
    public void SavePosition(int x, int y)
    {
        _settings.OverlayX = x;
        _settings.OverlayY = y;
        _settings.Save();
    }

    public (int X, int Y)? SavedPosition =>
        _settings.OverlayX is { } x && _settings.OverlayY is { } y ? (x, y) : null;

    private void RefreshMonitors()
    {
        Monitors.Clear();
        foreach (var m in NativeWindows.GetMonitors())
        {
            ICommand Place(OverlayCorner c) => new RelayCommand(() =>
            {
                if (!IsVisible) IsVisible = true;
                _host?.MoveTo(m, c);
            });
            Monitors.Add(new MonitorChoice(m, Place(OverlayCorner.TopLeft), Place(OverlayCorner.TopRight),
                                           Place(OverlayCorner.BottomLeft), Place(OverlayCorner.BottomRight)));
        }
    }

    private OverlayItemViewModel Item(string key, string group, string label, MetricViewModel? metric) =>
        new(key, group, label, metric, OnItemsChanged);

    private bool IsOn(string key) => AllItems?.FirstOrDefault(i => i.Key == key)?.IsEnabled == true;

    private void OnItemsChanged()
    {
        _settings.OverlayItems = AllItems.Where(i => i.IsEnabled).Select(i => i.Key).ToList();
        _settings.Save();
        RebuildVisible();
    }

    private void RebuildVisible()
    {
        VisibleMetrics.Clear();
        foreach (var i in AllItems.Where(i => i.IsEnabled && i.Metric is not null)) VisibleMetrics.Add(i);
        OnPropertyChanged(nameof(ShowGame));
        OnPropertyChanged(nameof(ShowChart));
        OnPropertyChanged(nameof(ShowClock));
    }
}

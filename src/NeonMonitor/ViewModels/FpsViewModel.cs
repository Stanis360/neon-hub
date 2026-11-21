using System.Collections.ObjectModel;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Fps;

namespace NeonMonitor.ViewModels;

/// <summary>Auswahlknopf für eine Anwendung („Automatisch“, „cod.exe“ …).</summary>
public sealed class FpsChoiceViewModel(string title, string? application, ICommand select, ICommand? ignore = null) : ObservableObject
{
    /// <summary>„Nicht als Spiel werten“ (null bei „Automatisch“).</summary>
    public ICommand? IgnoreCommand { get; } = ignore;
    public bool CanIgnore => IgnoreCommand is not null;
    private bool _isSelected;

    public string Title { get; } = title;
    /// <summary>Exe-Name oder null für „Automatisch“.</summary>
    public string? Application { get; } = application;
    public ICommand SelectCommand { get; } = select;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>ViewModel der FPS-Seite (und der FPS-Zeile im Live-Dashboard).</summary>
public sealed class FpsViewModel : ObservableObject
{
    private readonly FpsSource _source;
    private readonly AppSettings _settings;
    private string _application = "";
    private string _summary = "";
    private string _status = "PresentMon startet …";

    public FpsViewModel(FpsSource source, AppSettings settings, int historyPoints)
    {
        _source = source;
        _settings = settings;

        Fps = new MetricViewModel("FPS", "", "0", historyPoints, barMaximum: 240);
        Low1 = new MetricViewModel("1 % Low", "FPS", "0", historyPoints, barMaximum: 240);
        FrameTime = new MetricViewModel("Frametime Ø", "ms", "0.0", historyPoints, barMaximum: 33.3);
        FrameTimeMax = new MetricViewModel("Frametime max", "ms", "0.0", historyPoints, barMaximum: 50,
                                           warnAt: 33.4, criticalAt: 50);

        // Latenz: unter ~30 ms sehr gut, über 50 ms spürbar träge (Shooter)
        InputLatency = new MetricViewModel("Eingabe → Bild", "ms", "0.0", historyPoints, warnAt: 40, criticalAt: 60, barMaximum: 60);
        ClickLatency = new MetricViewModel("Klick → Bild", "ms", "0.0", historyPoints, warnAt: 40, criticalAt: 60, barMaximum: 60);
        DisplayDelay = new MetricViewModel("Bild → Anzeige", "ms", "0.0", historyPoints, barMaximum: 20);
        Stutters = new MetricViewModel("Ruckler / Minute", "", "0.0", historyPoints, warnAt: 3, criticalAt: 10, barMaximum: 10);

        // Verbindung
        PingRouter = new MetricViewModel("Ping Router", "ms", "0", historyPoints, warnAt: 10, criticalAt: 30, barMaximum: 20);
        PingInternet = new MetricViewModel("Ping Internet", "ms", "0", historyPoints, warnAt: 60, criticalAt: 100, barMaximum: 100);
        LossRouter = new MetricViewModel("Verlust Router", "%", "0.0", historyPoints, warnAt: 1, criticalAt: 3, barMaximum: 10);
        LossInternet = new MetricViewModel("Verlust Internet", "%", "0.0", historyPoints, warnAt: 1, criticalAt: 3, barMaximum: 10);
        Jitter = new MetricViewModel("Jitter", "ms", "0.0", historyPoints, warnAt: 10, criticalAt: 30, barMaximum: 30);

        SelectCommand = new ParamCommand(app =>
        {
            _source.PinnedApplication = app;
            foreach (var c in Choices) c.IsSelected = c.Application == app;
        });
        Choices.Add(new FpsChoiceViewModel("Automatisch", null, SelectCommand) { IsSelected = true });

        IgnoreCommand = new ParamCommand(app =>
        {
            if (app is null || _settings.FpsIgnoredApps.Contains(app, StringComparer.OrdinalIgnoreCase)) return;
            _settings.FpsIgnoredApps.Add(app);
            ApplyIgnored();
            if (string.Equals(_source.PinnedApplication, app, StringComparison.OrdinalIgnoreCase)) SelectCommand.Execute(null);
        });
        UnignoreCommand = new ParamCommand(app =>
        {
            _settings.FpsIgnoredApps.RemoveAll(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase));
            ApplyIgnored();
        });
        foreach (var a in _settings.FpsIgnoredApps) Ignored.Add(a);
    }

    public MetricViewModel InputLatency { get; }
    public MetricViewModel ClickLatency { get; }
    public MetricViewModel DisplayDelay { get; }
    public MetricViewModel Stutters { get; }
    public MetricViewModel PingRouter { get; }
    public MetricViewModel PingInternet { get; }
    public MetricViewModel LossRouter { get; }
    public MetricViewModel LossInternet { get; }
    public MetricViewModel Jitter { get; }

    /// <summary>Frametime-Verteilung der letzten 30 s (Anteile je Klasse).</summary>
    public double[] Histogram { get => _histogram; private set => SetProperty(ref _histogram, value); }
    private double[] _histogram = [];

    // ---- Engpass (über 5 Messungen geglättet, damit die Anzeige nicht flackert) ----
    private readonly Queue<string?> _bottlenecks = new();
    private string? _bottleneck;
    public string? Bottleneck
    {
        get => _bottleneck;
        private set
        {
            if (!SetProperty(ref _bottleneck, value)) return;
            OnPropertyChanged(nameof(BottleneckText));
            OnPropertyChanged(nameof(BottleneckTip));
            OnPropertyChanged(nameof(BottleneckShort));
        }
    }
    public string BottleneckText => Services.Fps.BottleneckAnalyzer.Describe(_bottleneck);
    public string BottleneckTip => Services.Fps.BottleneckAnalyzer.Tip(_bottleneck);
    /// <summary>Kurzform fürs Overlay, z. B. „GPU-Limit“.</summary>
    public string BottleneckShort => _bottleneck switch
    {
        null => "—",
        "Limit" => "FPS-Grenze",
        "Ausgeglichen" => "ausgeglichen",
        var b => $"{b}-Limit",
    };

    private string _connectionText = "";
    /// <summary>z. B. „WLAN · Signal 78 %“ oder „LAN“.</summary>
    public string ConnectionText { get => _connectionText; private set => SetProperty(ref _connectionText, value); }

    private string _connectionVerdict = "";
    /// <summary>Einschätzung, wo Paketverlust entsteht.</summary>
    public string ConnectionVerdict { get => _connectionVerdict; private set => SetProperty(ref _connectionVerdict, value); }

    public MetricViewModel Fps { get; }
    public MetricViewModel Low1 { get; }
    public MetricViewModel FrameTime { get; }
    public MetricViewModel FrameTimeMax { get; }

    /// <summary>Einzel-Frametimes der letzten ~300 Bilder.</summary>
    public ChartSeries FrameTimes { get; } = new(FrameTracker.RecentCount);

    /// <summary>„Automatisch“ + alle Anwendungen, die gerade Bilder ausgeben.</summary>
    public ObservableCollection<FpsChoiceViewModel> Choices { get; } = new();

    public ParamCommand SelectCommand { get; }
    public ParamCommand IgnoreCommand { get; }
    public ParamCommand UnignoreCommand { get; }

    /// <summary>Selbst ausgeschlossene Programme.</summary>
    public ObservableCollection<string> Ignored { get; } = new();
    public bool HasIgnored => Ignored.Count > 0;

    private void ApplyIgnored()
    {
        _settings.Save();
        FrameTracker.SetUserIgnored(_settings.FpsIgnoredApps);
        Ignored.Clear();
        foreach (var a in _settings.FpsIgnoredApps) Ignored.Add(a);
        OnPropertyChanged(nameof(HasIgnored));
    }

    /// <summary>Erfasste Anwendung, z. B. „cod.exe“.</summary>
    public string Application { get => _application; private set => SetProperty(ref _application, value); }

    /// <summary>Kurzform für das Live-Dashboard, z. B. „144 FPS · cod.exe“.</summary>
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Sicherer Modus (ohne PawnIO) – wird beim nächsten Programmstart wirksam.</summary>
    public bool SafeMode
    {
        get => _settings.SafeMode;
        set
        {
            if (_settings.SafeMode == value) return;
            _settings.SafeMode = value;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SafeModeHint));
        }
    }

    public string SafeModeHint => "Änderung wirkt nach einem Neustart von NeonMonitor.";

    private IEnumerable<MetricViewModel> All =>
        [Fps, Low1, FrameTime, FrameTimeMax, InputLatency, ClickLatency, DisplayDelay, Stutters,
         PingRouter, PingInternet, LossRouter, LossInternet, Jitter];

    public void Resize(int points)
    {
        foreach (var m in All) m.History.Resize(points);
    }

    public void ResetPeaks()
    {
        foreach (var m in All) m.ResetPeak();
    }

    /// <summary>Neuen Snapshot übernehmen (UI-Thread).</summary>
    public void Apply(HardwareSnapshot s)
    {
        Fps.Update(s.Fps);
        Low1.Update(s.Fps1PercentLow);
        FrameTime.Update(s.FrameTimeMs);
        FrameTimeMax.Update(s.FrameTimeMaxMs);
        FrameTimes.ReplaceAll(s.RecentFrameTimes);
        InputLatency.Update(s.InputLatencyMs);
        ClickLatency.Update(s.ClickLatencyMs);
        DisplayDelay.Update(s.DisplayDelayMs);
        Stutters.Update(s.StuttersPerMin);
        if (s.FrameHistogram.Length > 0 || s.Fps is null) Histogram = s.FrameHistogram;

        _bottlenecks.Enqueue(s.Bottleneck);
        while (_bottlenecks.Count > 5) _bottlenecks.Dequeue();
        Bottleneck = _bottlenecks.Where(b => b is not null).GroupBy(b => b)
                                 .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

        PingRouter.Update(s.PingRouterMs);
        PingInternet.Update(s.PingInternetMs);
        LossRouter.Update(s.LossRouterPct);
        LossInternet.Update(s.LossInternetPct);
        Jitter.Update(s.JitterMs);
        ConnectionText = s.ConnectionType switch
        {
            null => "",
            "WLAN" => s.WifiSignalPct is { } sig ? $"WLAN · Signal {sig:0} %" : "WLAN",
            var c => c,
        };
        ConnectionVerdict = (s.LossRouterPct, s.LossInternetPct) switch
        {
            ( >= 1, _) => "Verlust schon im Heimnetz (PC ↔ Router)" + (s.ConnectionType == "WLAN" ? " – typisch für WLAN. Ein LAN-Kabel behebt das meist." : " – Kabel/Router prüfen."),
            (_, >= 1) => "Heimnetz sauber, Verlust ab dem Router → Internetanschluss oder Anbieter.",
            (not null, not null) => "Heimnetz und Internet ohne Verlust. Zeigt das Spiel trotzdem Paketverlust, liegt es eher am Spielserver.",
            _ => "Messung läuft …",
        };

        Application = s.FpsApplication ?? "";
        Summary = s.Fps is { } fps ? $"{fps:0} FPS · {Services.Recording.GameNames.FromApplication(s.FpsApplication)}" + (s.Bottleneck is null ? "" : $" · {BottleneckShort}") : "";
        Status = !_source.IsAvailable
            ? "PresentMon fehlt – bitte Installieren.bat erneut ausführen."
            : s.Fps is null
                ? $"PresentMon {_source.Status} – warte auf ein Spiel bzw. eine 3D-Anwendung …"
                : $"PresentMon {_source.Status} · Messung über Windows-ETW, ohne Eingriff ins Spiel";

        UpdateChoices(s.FpsCandidates);
    }

    private void UpdateChoices(string[] apps)
    {
        // Gewählte Anwendung bleibt in der Liste, auch wenn sie gerade keine Bilder liefert
        var pinned = _source.PinnedApplication;
        var wanted = apps.Concat(pinned is null ? [] : [pinned]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        for (var i = Choices.Count - 1; i >= 1; i--)
            if (!wanted.Contains(Choices[i].Application!, StringComparer.OrdinalIgnoreCase)) Choices.RemoveAt(i);
        foreach (var app in wanted)
            if (Choices.All(c => !string.Equals(c.Application, app, StringComparison.OrdinalIgnoreCase)))
                Choices.Add(new FpsChoiceViewModel(app, app, SelectCommand, IgnoreCommand) { IsSelected = app == pinned });
    }
}

/// <summary>ICommand mit Parameter (hier: Exe-Name oder null).</summary>
public sealed class ParamCommand(Action<string?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter as string);
}

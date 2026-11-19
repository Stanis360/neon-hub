using System.Collections.ObjectModel;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Fps;

namespace NeonMonitor.ViewModels;

/// <summary>Auswahlknopf für eine Anwendung („Automatisch“, „cod.exe“ …).</summary>
public sealed class FpsChoiceViewModel(string title, string? application, ICommand select) : ObservableObject
{
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

        SelectCommand = new ParamCommand(app =>
        {
            _source.PinnedApplication = app;
            foreach (var c in Choices) c.IsSelected = c.Application == app;
        });
        Choices.Add(new FpsChoiceViewModel("Automatisch", null, SelectCommand) { IsSelected = true });
    }

    public MetricViewModel Fps { get; }
    public MetricViewModel Low1 { get; }
    public MetricViewModel FrameTime { get; }
    public MetricViewModel FrameTimeMax { get; }

    /// <summary>Einzel-Frametimes der letzten ~300 Bilder.</summary>
    public ChartSeries FrameTimes { get; } = new(FrameTracker.RecentCount);

    /// <summary>„Automatisch“ + alle Anwendungen, die gerade Bilder ausgeben.</summary>
    public ObservableCollection<FpsChoiceViewModel> Choices { get; } = new();

    public ParamCommand SelectCommand { get; }

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

    public void Resize(int points)
    {
        foreach (var m in new[] { Fps, Low1, FrameTime, FrameTimeMax }) m.History.Resize(points);
    }

    public void ResetPeaks()
    {
        foreach (var m in new[] { Fps, Low1, FrameTime, FrameTimeMax }) m.ResetPeak();
    }

    /// <summary>Neuen Snapshot übernehmen (UI-Thread).</summary>
    public void Apply(HardwareSnapshot s)
    {
        Fps.Update(s.Fps);
        Low1.Update(s.Fps1PercentLow);
        FrameTime.Update(s.FrameTimeMs);
        FrameTimeMax.Update(s.FrameTimeMaxMs);
        FrameTimes.ReplaceAll(s.RecentFrameTimes);

        Application = s.FpsApplication ?? "";
        Summary = s.Fps is { } fps ? $"{fps:0} FPS · {s.FpsApplication}" : "";
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
                Choices.Add(new FpsChoiceViewModel(app, app, SelectCommand) { IsSelected = app == pinned });
    }
}

/// <summary>ICommand mit Parameter (hier: Exe-Name oder null).</summary>
public sealed class ParamCommand(Action<string?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter as string);
}

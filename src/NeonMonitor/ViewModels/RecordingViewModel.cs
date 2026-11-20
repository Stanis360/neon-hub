using System.Windows;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Fps;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.ViewModels;

/// <summary>Reiter „Aufzeichnung“: Start/Stopp, Spielname, Status, Ergebnis der letzten Sitzung.</summary>
public sealed class RecordingViewModel : ObservableObject
{
    private readonly RecordingService _recorder;
    private readonly AppSettings _settings;
    private string _gameName = "";

    // Automatik
    private DateTime? _gameActiveSince;
    private DateTime _gameLastActive = DateTime.MinValue;
    private string? _activeGame;
    private bool _autoStarted;
    private string? _suppressedGame;   // nach manuellem Stopp nicht sofort wieder automatisch starten
    private DateTime? _benchmarkEnd;
    private bool _nameEditedByUser;
    private string _statusText = "Bereit. Start per Knopf oder Strg + Alt + R.";
    private string _elapsedText = "";
    private SessionSummary? _lastSession;

    public RecordingViewModel(RecordingService recorder, AppSettings settings)
    {
        _recorder = recorder;
        _settings = settings;
        ToggleCommand = new RelayCommand(Toggle);
        BenchmarkCommand = new RelayCommand(StartBenchmark, () => !IsRecording);
        OpenFolderCommand = new RelayCommand(() => Shell.Open(SessionStore.Directory));
        _recorder.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(ButtonText));
        });
    }

    /// <summary>Wird nach jeder beendeten Aufnahme ausgelöst (Vergleichsliste aktualisieren).</summary>
    public event Action<SessionSummary>? SessionSaved;

    public ICommand ToggleCommand { get; }
    public ICommand BenchmarkCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public bool IsRecording => _recorder.IsRecording;
    public bool IsBenchmark => _benchmarkEnd is not null;
    public string ButtonText => IsRecording ? (IsBenchmark ? "■  Benchmark abbrechen" : "■  Aufnahme stoppen") : "●  Aufnahme starten";
    public string BenchmarkText => $"⏱  Benchmark {_settings.BenchmarkSeconds} s";

    /// <summary>Hinweis, ob die automatische Aufnahme aktiv ist.</summary>
    public string AutoText => _settings.AutoRecord
        ? "Automatische Aufnahme ist an: startet 10 s nach Spielbeginn, stoppt 15 s nach Spielende."
        : "Tipp: In den Einstellungen lässt sich die automatische Aufnahme beim Spielstart einschalten.";

    /// <summary>
    /// Spielname für die Datei. Wird automatisch aus dem erkannten Spiel gefüllt,
    /// bis man selbst etwas einträgt.
    /// </summary>
    public string GameName
    {
        get => _gameName;
        set
        {
            if (SetProperty(ref _gameName, value)) _nameEditedByUser = true;
        }
    }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    /// <summary>Laufzeit „03:12“ für Overlay und Titelleiste.</summary>
    public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

    public SessionSummary? LastSession
    {
        get => _lastSession;
        private set
        {
            if (SetProperty(ref _lastSession, value)) OnPropertyChanged(nameof(HasLastSession));
        }
    }

    public bool HasLastSession => LastSession is not null;

    /// <summary>Feste Aufnahmedauer für faire Vergleiche (Dauer in den Einstellungen).</summary>
    public void StartBenchmark()
    {
        if (IsRecording) return;
        var game = string.IsNullOrWhiteSpace(GameName) ? GameNames.FromApplication(_recorder.LatestApplication) : GameName;
        try
        {
            _recorder.Start($"{game} Benchmark");
            _benchmarkEnd = DateTime.Now.AddSeconds(_settings.BenchmarkSeconds);
            _autoStarted = false;
        }
        catch (Exception ex)
        {
            Log.Error("Benchmark", ex);
            StatusText = $"Fehler: {ex.Message}";
        }
        RaiseState();
    }

    public void Toggle()
    {
        try
        {
            if (_recorder.IsRecording)
            {
                if (_autoStarted) _suppressedGame = _activeGame; // manuell gestoppt → nicht gleich neu starten
                StopInternal();
                return;
            }
            var name = string.IsNullOrWhiteSpace(GameName) ? GameNames.FromApplication(_recorder.LatestApplication) : GameName;
            _recorder.Start(name);
            _autoStarted = false;
        }
        catch (Exception ex)
        {
            Log.Error("Aufnahme", ex);
            StatusText = $"Fehler: {ex.Message}";
        }
        RaiseState();
    }

    private void StopInternal()
    {
        try
        {
            var summary = _recorder.Stop();
            _nameEditedByUser = false;
            if (summary is not null)
            {
                LastSession = summary;
                StatusText = $"Gespeichert: {summary.CsvFile}";
                SessionSaved?.Invoke(summary);
            }
            ElapsedText = "";
        }
        catch (Exception ex)
        {
            Log.Error("Aufnahme stoppen", ex);
            StatusText = $"Fehler: {ex.Message}";
        }
        _benchmarkEnd = null;
        _autoStarted = false;
        RaiseState();
    }

    private void RaiseState()
    {
        foreach (var n in new[] { nameof(IsRecording), nameof(IsBenchmark), nameof(ButtonText), nameof(BenchmarkText), nameof(AutoText) })
            OnPropertyChanged(n);
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Mit jedem Messwert aufgerufen (UI-Thread).</summary>
    public void Tick(HardwareSnapshot s)
    {
        TickAutomation(s);

        // Spielname automatisch vorschlagen, solange der Nutzer nichts eingetragen hat
        if (!_nameEditedByUser && !IsRecording && _recorder.LatestApplication is { } app)
        {
            _gameName = GameNames.FromApplication(app);
            OnPropertyChanged(nameof(GameName));
        }

        var st = _recorder.GetStatus();
        if (!st.IsRecording) return;
        ElapsedText = st.Elapsed.ToString(st.Elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
        var rest = _benchmarkEnd is { } end ? $" · Benchmark noch {Math.Max(0, (end - DateTime.Now).TotalSeconds):0} s" : "";
        var auto = _autoStarted ? " · automatisch gestartet" : "";
        StatusText = $"Aufnahme läuft · {ElapsedText} · {st.Samples:N0} Messungen · {st.Frames:N0} Bilder"
                     + (st.Application is null ? " · warte auf Spiel …" : $" · {st.Application}") + rest + auto;
    }

    /// <summary>Benchmark-Ende und automatische Aufnahme beim Spielstart/-ende.</summary>
    private void TickAutomation(HardwareSnapshot s)
    {
        var now = DateTime.Now;

        if (_benchmarkEnd is { } end && now >= end && IsRecording)
        {
            StopInternal();
            StatusText = "Benchmark fertig – " + StatusText;
        }

        // „Spiel läuft“ = erkannte Anwendung (kein Browser o. Ä.), flüssige FPS und spürbare GPU-Last
        var app = s.FpsApplication;
        var gameRunning = app is not null && s.Fps > 15 && !FrameTracker.IsIgnored(app) && (s.GpuLoad ?? 100) >= 20;
        if (gameRunning)
        {
            if (_activeGame != app) _gameActiveSince = now;
            _activeGame = app;
            _gameActiveSince ??= now;
            _gameLastActive = now;
        }
        else
        {
            _gameActiveSince = null;
            if ((now - _gameLastActive).TotalSeconds >= 15)
            {
                _activeGame = null;
                _suppressedGame = null; // Spiel beendet → nächstes Mal wieder automatisch
            }
        }

        if (!_settings.AutoRecord) return;

        if (!IsRecording && gameRunning && _activeGame != _suppressedGame
            && now - _gameActiveSince >= TimeSpan.FromSeconds(10))
        {
            try
            {
                _recorder.Start(GameNames.FromApplication(app));
                _autoStarted = true;
                RaiseState();
            }
            catch (Exception ex) { Log.Error("Automatische Aufnahme", ex); }
        }
        else if (IsRecording && _autoStarted && _benchmarkEnd is null && (now - _gameLastActive).TotalSeconds >= 15)
        {
            StopInternal();
            StatusText = "Automatisch gestoppt (Spiel beendet) – " + StatusText;
        }
    }
}

/// <summary>Öffnet Dateien/Ordner mit dem Standardprogramm.</summary>
public static class Shell
{
    public static void Open(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn($"Öffnen von '{path}' fehlgeschlagen: {ex.Message}"); }
    }
}

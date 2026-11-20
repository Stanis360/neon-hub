using System.Windows;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.ViewModels;

/// <summary>Reiter „Aufzeichnung“: Start/Stopp, Spielname, Status, Ergebnis der letzten Sitzung.</summary>
public sealed class RecordingViewModel : ObservableObject
{
    private readonly RecordingService _recorder;
    private string _gameName = "";
    private bool _nameEditedByUser;
    private string _statusText = "Bereit. Start per Knopf oder Strg + Alt + R.";
    private string _elapsedText = "";
    private SessionSummary? _lastSession;

    public RecordingViewModel(RecordingService recorder)
    {
        _recorder = recorder;
        ToggleCommand = new RelayCommand(Toggle);
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
    public ICommand OpenFolderCommand { get; }

    public bool IsRecording => _recorder.IsRecording;
    public string ButtonText => IsRecording ? "■  Aufnahme stoppen" : "●  Aufnahme starten";

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

    public void Toggle()
    {
        try
        {
            if (_recorder.IsRecording)
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
            else
            {
                var name = string.IsNullOrWhiteSpace(GameName) ? GameNames.FromApplication(_recorder.LatestApplication) : GameName;
                _recorder.Start(name);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Aufnahme", ex);
            StatusText = $"Fehler: {ex.Message}";
        }
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(ButtonText));
    }

    /// <summary>Mit jedem Messwert aufgerufen (UI-Thread).</summary>
    public void Tick()
    {
        // Spielname automatisch vorschlagen, solange der Nutzer nichts eingetragen hat
        if (!_nameEditedByUser && !IsRecording && _recorder.LatestApplication is { } app)
        {
            _gameName = GameNames.FromApplication(app);
            OnPropertyChanged(nameof(GameName));
        }

        var st = _recorder.GetStatus();
        if (!st.IsRecording) return;
        ElapsedText = st.Elapsed.ToString(st.Elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
        StatusText = $"Aufnahme läuft · {ElapsedText} · {st.Samples:N0} Messungen · {st.Frames:N0} Bilder"
                     + (st.Application is null ? " · warte auf Spiel …" : $" · {st.Application}");
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

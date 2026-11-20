using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services.Comfort;

namespace NeonMonitor.ViewModels;

/// <summary>Update-Hinweis (Banner im Hauptfenster) und Update-Bereich in den Einstellungen.</summary>
public sealed class UpdateViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly Action _requestExit;
    private readonly DispatcherTimer _timer;
    private ReleaseInfo? _available;
    private string _status = "";
    private bool _busy;

    /// <param name="requestExit">Beendet NeonMonitor sauber (wird nach dem Start der Installation aufgerufen).</param>
    public UpdateViewModel(AppSettings settings, Action requestExit)
    {
        _settings = settings;
        _requestExit = requestExit;
        InstallCommand = new RelayCommand(async () => await InstallAsync(), () => _available is not null && !_busy);
        LaterCommand = new RelayCommand(() =>
        {
            if (_available is not null) _settings.SkippedVersion = _available.Version.ToString();
            _settings.Save();
            SetAvailable(null);
        });
        CheckCommand = new RelayCommand(async () => await CheckAsync(manual: true), () => !_busy);
        OpenReleasesCommand = new RelayCommand(() => Shell.Open(_available?.PageUrl ?? UpdateService.ReleasesPage));

        // erste Prüfung kurz nach dem Start, danach alle 6 Stunden
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(6);
            if (_settings.CheckForUpdates) await CheckAsync(manual: false);
        };
        _timer.Start();
    }

    public ICommand InstallCommand { get; }
    public ICommand LaterCommand { get; }
    public ICommand CheckCommand { get; }
    public ICommand OpenReleasesCommand { get; }

    /// <summary>Kurzform für die Titelleiste, z. B. „v1.2.0“.</summary>
    public string VersionShort => $"v{UpdateService.CurrentVersion}";

    public string CurrentVersionText => $"Installiert: {UpdateService.CurrentVersion}" + (UpdateService.IsSelfContained ? " (portabel)" : "");

    public bool IsAvailable => _available is not null;
    public string BannerText => _available is null ? "" : $"Neue Version {_available.Version} ist verfügbar (installiert: {UpdateService.CurrentVersion}).";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool CheckForUpdates
    {
        get => _settings.CheckForUpdates;
        set
        {
            _settings.CheckForUpdates = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    private async Task CheckAsync(bool manual)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            if (manual) Status = "Suche nach Updates …";
            var latest = await UpdateService.GetLatestAsync();
            if (latest is null)
            {
                Status = manual ? "Noch kein Release auf GitHub gefunden (oder keine Verbindung)." : Status;
                return;
            }
            if (latest.Version <= UpdateService.CurrentVersion)
            {
                Status = $"Aktuell – neueste Version ist {latest.Version}.";
                SetAvailable(null);
                return;
            }
            // „Später“ gilt nur für genau diese Version und nur für die automatische Prüfung
            if (!manual && _settings.SkippedVersion == latest.Version.ToString()) return;
            Status = $"Version {latest.Version} verfügbar.";
            SetAvailable(latest);
        }
        catch (Exception ex)
        {
            Log.Warn($"Update-Prüfung: {ex.Message}");
            if (manual) Status = $"Update-Prüfung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InstallAsync()
    {
        if (_available is null) return;
        var answer = MessageBox.Show(
            $"NeonMonitor {_available.Version} jetzt installieren?\n\nNeonMonitor wird dafür kurz beendet und startet danach von selbst neu. " +
            "Eine laufende Aufnahme wird vorher gespeichert.",
            "Update", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        SetBusy(true);
        try
        {
            await UpdateService.StartInstallAsync(_available, new Progress<string>(s => Status = s));
            Status = "Installation gestartet – NeonMonitor wird beendet …";
            _requestExit();
        }
        catch (Exception ex)
        {
            Log.Error("Update", ex);
            Status = $"Update fehlgeschlagen: {ex.Message}";
            MessageBox.Show(Status + "\n\nDu kannst die neue Version auch manuell von GitHub laden.", "Update",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetAvailable(ReleaseInfo? r)
    {
        _available = r;
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(BannerText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CommandManager.InvalidateRequerySuggested();
    }
}

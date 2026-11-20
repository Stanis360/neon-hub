using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services;
using NeonMonitor.Services.Comfort;
using NeonMonitor.Services.Recording;
using NeonMonitor.ViewModels;
using NeonMonitor.Views;

namespace NeonMonitor;

/// <summary>
/// Einstiegspunkt: erzeugt die Dienste, verbindet sie mit dem Hauptfenster, Infobereich-Symbol und
/// Tastenkürzeln und räumt beim Beenden auf.
/// </summary>
public partial class App : Application
{
    private const string MutexName = @"Local\NeonMonitor.SingleInstance";
    private const string ShowEventName = @"Local\NeonMonitor.ShowWindow";

    private HardwareMonitorService? _monitor;
    private RecordingService? _recorder;
    private PlaytimeService? _playtime;
    private GlobalHotkeys? _hotkeys;
    private TrayIcon? _tray;
    private Mutex? _singleInstance;
    private EventWaitHandle? _showEvent;
    private AppSettings _settings = new();
    private MainWindow? _window;
    private bool _exiting;
    private bool _trayHintShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Nur eine Instanz: Läuft NeonMonitor schon, bitten wir sie, ihr Fenster zu zeigen, und beenden uns.
        _singleInstance = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { /* alte Version ohne Signal */ }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                if (_exiting) return;
                Dispatcher.BeginInvoke(ShowMainWindow);
            }
        }) { IsBackground = true, Name = "Einzelinstanz" }.Start();

        // Unbehandelte Fehler protokollieren statt kommentarlos abzustürzen
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unbehandelte Ausnahme", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unbeobachtete Task-Ausnahme", args.Exception);
            args.SetObserved();
        };

        Log.Info($"NeonMonitor startet (Admin={SystemInfo.IsAdministrator()}, PawnIO={SystemInfo.IsPawnIoInstalled()})");

        // Wir beenden selbst (Fenster kann im Infobereich versteckt sein)
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settings = AppSettings.Load();
        Services.Fps.FrameTracker.SetUserIgnored(_settings.FpsIgnoredApps);
        _monitor = new HardwareMonitorService(_settings);
        _recorder = new RecordingService(_monitor, () => _settings.PricePerKwh);
        _playtime = new PlaytimeService();
        var alerts = new AlertService(_settings);

        var vm = new MainViewModel(_monitor, _settings, _recorder, _playtime, alerts);
        _window = new MainWindow { DataContext = vm };
        MainWindow = _window;
        _window.Closing += OnWindowClosing;
        _window.StateChanged += (_, _) =>
        {
            if (_window.WindowState == WindowState.Minimized && _settings.MinimizeToTray) HideToTray();
        };

        // Infobereich-Symbol
        _tray = new TrayIcon(
            open: ShowMainWindow,
            toggleOverlay: () => vm.Overlay.ToggleCommand.Execute(null),
            toggleRecording: () => vm.Recording.ToggleCommand.Execute(null),
            benchmark: () => vm.Recording.StartBenchmark(),
            exit: ExitApp);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.TrayText)) _tray.SetText(vm.TrayText);
        };
        vm.Recording.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RecordingViewModel.IsRecording)) _tray.SetRecording(vm.Recording.IsRecording);
        };
        alerts.Alert += (title, text) => _tray.Notify(title, text, warning: true);

        // Beim Autostart (--minimized) nur im Infobereich starten
        var startHidden = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        var handle = new WindowInteropHelper(_window).EnsureHandle(); // Handle auch ohne sichtbares Fenster
        if (!startHidden) _window.Show();

        // Overlay (öffnet sich automatisch wieder, wenn es beim letzten Beenden sichtbar war)
        vm.Overlay.AttachHost(new OverlayController(vm.Overlay));

        // Systemweite Tastenkürzel – funktionieren auch mitten im Spiel
        _hotkeys = new GlobalHotkeys(handle);
        const uint ctrlAlt = GlobalHotkeys.ModControl | GlobalHotkeys.ModAlt;
        _hotkeys.Register(ctrlAlt, 0x4F /* O */, () => vm.Overlay.ToggleCommand.Execute(null));
        _hotkeys.Register(ctrlAlt, 0x52 /* R */, () => vm.Recording.ToggleCommand.Execute(null));
        _hotkeys.Register(ctrlAlt, 0x42 /* B */, () => vm.Recording.StartBenchmark());

        // Sensoren erst nach dem Anzeigen starten – LHM braucht beim Öffnen ein paar Sekunden.
        _monitor.Start();
    }

    /// <summary>Fenster aus dem Infobereich holen und nach vorne bringen.</summary>
    private void ShowMainWindow()
    {
        if (_window is null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;  // kurzer Trick, damit das Fenster wirklich nach vorne kommt
        _window.Topmost = false;
        _window.Focus();
    }

    private void HideToTray()
    {
        _window?.Hide();
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray?.Notify("NeonMonitor läuft weiter",
            "Das Programm ist jetzt im Infobereich (unten rechts). Doppelklick öffnet es wieder, Rechtsklick → Beenden.");
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting || !_settings.CloseToTray)
        {
            ExitApp();
            return;
        }
        e.Cancel = true;
        HideToTray();
    }

    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        _showEvent?.Set();          // Wartethread beenden
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _recorder?.Dispose();       // laufende Aufnahme sauber abschließen
        _playtime?.Save();
        _monitor?.Dispose();        // gibt Lüfter ans Mainboard zurück und den PawnIO-Treiber frei
        _showEvent?.Dispose();
        _singleInstance?.Dispose();
        Log.Info("NeonMonitor beendet");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("UI-Fehler", e.Exception);
        MessageBox.Show($"Unerwarteter Fehler:\n{e.Exception.Message}\n\nDetails: {AppPaths.LogFile}",
            "NeonMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

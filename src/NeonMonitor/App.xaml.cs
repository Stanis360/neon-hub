using System.Windows;
using System.Windows.Threading;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services;
using NeonMonitor.ViewModels;

namespace NeonMonitor;

/// <summary>
/// Einstiegspunkt: erzeugt die Dienste, verbindet sie mit dem Hauptfenster und räumt beim Beenden auf.
/// </summary>
public partial class App : Application
{
    private HardwareMonitorService? _monitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        var settings = AppSettings.Load();
        _monitor = new HardwareMonitorService(settings.PollIntervalMs);

        var window = new MainWindow { DataContext = new MainViewModel(_monitor, settings) };
        MainWindow = window;
        window.Show();

        // Sensoren erst nach dem Anzeigen starten – LHM braucht beim Öffnen ein paar Sekunden.
        _monitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose(); // wichtig: gibt den PawnIO-Treiber frei
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

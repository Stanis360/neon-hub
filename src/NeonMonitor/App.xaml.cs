using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Nur eine Instanz: Läuft NeonMonitor schon, holen wir dieses Fenster nach vorne und beenden uns.
        _singleInstance = new Mutex(true, @"Local\NeonMonitor.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            BringExistingInstanceToFront();
            Shutdown();
            return;
        }

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
        _singleInstance?.Dispose();
        Log.Info("NeonMonitor beendet");
        base.OnExit(e);
    }

    private static void BringExistingInstanceToFront()
    {
        var current = Environment.ProcessId;
        var other = Process.GetProcessesByName("NeonMonitor").FirstOrDefault(p => p.Id != current);
        if (other is null || other.MainWindowHandle == IntPtr.Zero) return;
        ShowWindow(other.MainWindowHandle, SwRestore);
        SetForegroundWindow(other.MainWindowHandle);
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("UI-Fehler", e.Exception);
        MessageBox.Show($"Unerwarteter Fehler:\n{e.Exception.Message}\n\nDetails: {AppPaths.LogFile}",
            "NeonMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

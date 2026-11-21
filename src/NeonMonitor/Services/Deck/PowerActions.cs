using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NeonMonitor.Services.Deck;

/// <summary>
/// Energie-Aktionen für das Deck: Herunterfahren/Neustart (mit Vorwarnzeit und Abbrechen),
/// Energiesparen, Ruhezustand, Sperren, Bildschirme aus.
/// </summary>
public static class PowerActions
{
    /// <summary>Vorwarnzeit für Herunterfahren/Neustart – in der Zeit kann man noch abbrechen.</summary>
    public const int DelaySeconds = 15;

    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        ["shutdown"] = "Herunterfahren",
        ["restart"] = "Neu starten",
        ["sleep"] = "Energiesparen (Standby)",
        ["hibernate"] = "Ruhezustand",
        ["lock"] = "Sperren",
        ["displayoff"] = "Bildschirme aus",
        ["abort"] = "Herunterfahren abbrechen",
    };

    /// <summary>Aktionen, die auf dem Handy vorher bestätigt werden müssen.</summary>
    public static bool NeedsConfirmation(string action) => action is "shutdown" or "restart" or "sleep" or "hibernate";

    /// <summary>Führt die Aktion aus und liefert die Rückmeldung fürs Handy.</summary>
    public static string Run(string action)
    {
        switch (action)
        {
            case "shutdown":
                Shutdown($"/s /t {DelaySeconds} /c \"NeonMonitor: Der PC fährt in {DelaySeconds} Sekunden herunter.\"");
                return $"Fährt in {DelaySeconds} s herunter – „Abbrechen“ stoppt es";
            case "restart":
                Shutdown($"/r /t {DelaySeconds} /c \"NeonMonitor: Der PC startet in {DelaySeconds} Sekunden neu.\"");
                return $"Startet in {DelaySeconds} s neu – „Abbrechen“ stoppt es";
            case "abort":
                Shutdown("/a");
                return "Herunterfahren abgebrochen";
            case "sleep":
                // kurz verzögert, damit die Antwort noch beim Handy ankommt
                Task.Delay(1500).ContinueWith(_ => SetSuspendState(false, false, false));
                return "PC geht in den Energiesparmodus";
            case "hibernate":
                Task.Delay(1500).ContinueWith(_ => SetSuspendState(true, false, false));
                return "PC geht in den Ruhezustand";
            case "lock":
                LockWorkStation();
                return "PC gesperrt";
            case "displayoff":
                PostMessage(new IntPtr(0xFFFF), 0x0112 /* WM_SYSCOMMAND */, new IntPtr(0xF170) /* SC_MONITORPOWER */, new IntPtr(2));
                return "Bildschirme aus";
            default:
                throw new InvalidOperationException("Unbekannte Energie-Aktion.");
        }
    }

    private static void Shutdown(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("shutdown.exe", args) { UseShellExecute = false, CreateNoWindow = true });
        p?.WaitForExit(5000);
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}

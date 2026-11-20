using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Comfort;

/// <summary>
/// Autostart über die Windows-Aufgabenplanung. Vorteil gegenüber dem Autostart-Ordner:
/// Die Aufgabe startet NeonMonitor mit Adminrechten <b>ohne</b> UAC-Abfrage bei jeder Anmeldung.
/// </summary>
public static class AutostartService
{
    private const string TaskName = "NeonMonitor";

    public static bool IsEnabled() => RunSchtasks($"/Query /TN \"{TaskName}\"", out _) == 0;

    /// <summary>Legt die Aufgabe an (startet minimiert im Infobereich, 10 s nach der Anmeldung).</summary>
    public static bool Enable(out string error)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Programmpfad unbekannt");
        var user = WindowsIdentity.GetCurrent().Name;
        string X(string v) => SecurityElement.Escape(v) ?? "";

        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>NeonMonitor bei der Anmeldung starten</Description></RegistrationInfo>
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled><UserId>{X(user)}</UserId><Delay>PT10S</Delay></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author"><UserId>{X(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec><Command>{X(exe)}</Command><Arguments>--minimized</Arguments><WorkingDirectory>{X(Path.GetDirectoryName(exe)!)}</WorkingDirectory></Exec>
              </Actions>
            </Task>
            """;
        var file = Path.Combine(Path.GetTempPath(), "NeonMonitor-autostart.xml");
        File.WriteAllText(file, xml, Encoding.Unicode);
        try
        {
            return RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F", out error) == 0;
        }
        finally
        {
            File.Delete(file);
        }
    }

    public static bool Disable(out string error) => RunSchtasks($"/Delete /TN \"{TaskName}\" /F", out error) == 0;

    private static int RunSchtasks(string args, out string output)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(10_000);
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            Log.Warn($"schtasks {args}: {ex.Message}");
            output = ex.Message;
            return -1;
        }
    }
}

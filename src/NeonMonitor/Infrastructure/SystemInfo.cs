using System.Security.Principal;
using Microsoft.Win32;

namespace NeonMonitor.Infrastructure;

/// <summary>Hilfsfunktionen für Umgebungsprüfungen (Admin, Treiber).</summary>
public static class SystemInfo
{
    /// <summary>Läuft der Prozess mit Administratorrechten?</summary>
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Ist der PawnIO-Treiber installiert? LibreHardwareMonitor ab 0.9.5 nutzt PawnIO
    /// statt des alten (von Windows Defender blockierten) WinRing0-Treibers.
    /// Ohne PawnIO fehlen CPU-Temperatur, -Takt und -Leistung.
    /// </summary>
    public static bool IsPawnIoInstalled()
    {
        const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";
        try
        {
            using var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = hklm64.OpenSubKey(key);
            return k is not null;
        }
        catch
        {
            return false;
        }
    }
}

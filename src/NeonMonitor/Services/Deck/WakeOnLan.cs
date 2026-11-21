using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace NeonMonitor.Services.Deck;

/// <summary>Netzwerkadapter mit den Angaben, die eine Wake-on-LAN-App braucht.</summary>
public sealed record WolAdapter(string Name, string Description, bool IsWifi, string Mac, string? Ip);

/// <summary>
/// Hilfen für Wake-on-LAN (PC per Netzwerk einschalten): Adapter und MAC-Adresse ermitteln und
/// Windows vorbereiten (Wecken per „Magic Packet“ erlauben, Schnellstart aus).
/// Die BIOS-Einstellung muss man selbst setzen.
/// </summary>
public static class WakeOnLan
{
    /// <summary>Aktive Adapter mit Gateway (also die, über die der PC im Heimnetz hängt).</summary>
    public static IReadOnlyList<WolAdapter> Adapters() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.GigabitEthernet
                        && !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                        && !n.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
                        && n.GetIPProperties().GatewayAddresses.Count > 0)
            .Select(n => new WolAdapter(
                n.Name, n.Description, n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                string.Join("-", n.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))),
                n.GetIPProperties().UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString()))
            .ToList();

    /// <summary>Ist der Windows-Schnellstart an? (Verhindert bei vielen Boards das Wecken aus „Aus“.)</summary>
    public static bool IsFastStartupOn()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
        return key?.GetValue("HiberbootEnabled") is int v && v == 1;
    }

    /// <summary>
    /// Windows vorbereiten: auf allen kabelgebundenen Adaptern „Wake on Magic Packet“ einschalten
    /// und den Schnellstart abschalten. Gibt die Ausgabe der Befehle zurück.
    /// </summary>
    public static string PrepareWindows()
    {
        const string script = """
            $ErrorActionPreference = 'Continue'
            $out = @()
            Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' -and $_.MediaType -ne 'Native 802.11' } | ForEach-Object {
                try { Set-NetAdapterPowerManagement -Name $_.Name -WakeOnMagicPacket Enabled -ErrorAction Stop; $out += "Magic Packet erlaubt: $($_.Name)" }
                catch { $out += "Magic Packet für $($_.Name) nicht änderbar: $($_.Exception.Message)" }
                Get-NetAdapterAdvancedProperty -Name $_.Name -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -match 'Magic Packet|Wake on Magic' } |
                    ForEach-Object { try { Set-NetAdapterAdvancedProperty -Name $_.Name -DisplayName $_.DisplayName -DisplayValue 'Enabled' -ErrorAction Stop } catch { } }
            }
            Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name HiberbootEnabled -Value 0
            $out += 'Schnellstart ausgeschaltet'
            $out -join "`n"
            """;
        var psi = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);
        using var p = Process.Start(psi)!;
        var text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(30_000);
        return text.Trim();
    }
}

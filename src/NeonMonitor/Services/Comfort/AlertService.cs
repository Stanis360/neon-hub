using NeonMonitor.Infrastructure;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Comfort;

/// <summary>
/// Prüft Grenzwerte und meldet Warnungen (als Windows-Benachrichtigung über das Infobereich-Symbol).
/// Dieselbe Warnung kommt frühestens nach 5 Minuten erneut.
/// </summary>
public sealed class AlertService(AppSettings settings)
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, DateTime> _lastAlert = new();
    private int _cpuFanStoppedTicks;

    /// <summary>(Titel, Text) – UI-Thread.</summary>
    public event Action<string, string>? Alert;

    public void Check(HardwareSnapshot s)
    {
        if (!settings.AlertsEnabled) return;

        if (s.CpuTemp >= settings.AlertCpuTemp)
            Raise("cpu", "CPU sehr heiß", $"CPU-Temperatur {s.CpuTemp:0} °C (Grenze {settings.AlertCpuTemp:0} °C).");
        if (s.GpuTemp >= settings.AlertGpuTemp)
            Raise("gpu", "Grafikkarte sehr heiß", $"GPU-Temperatur {s.GpuTemp:0} °C (Grenze {settings.AlertGpuTemp:0} °C).");
        if (s.GpuHotspot >= settings.AlertHotspot)
            Raise("hot", "GPU-Hotspot sehr heiß", $"Hotspot {s.GpuHotspot:0} °C (Grenze {settings.AlertHotspot:0} °C).");

        // Paketverlust (über die letzten 60 Pings) – mit Hinweis, wo er entsteht
        if (s.LossRouterPct >= 3)
            Raise("lossrouter", "Paketverlust im Heimnetz",
                $"{s.LossRouterPct:0} % Verlust schon zum Router" + (s.ConnectionType == "WLAN" ? $" (WLAN, Signal {s.WifiSignalPct:0} %). LAN-Kabel würde helfen." : "."));
        else if (s.LossInternetPct >= 3)
            Raise("lossinet", "Paketverlust im Internet",
                $"{s.LossInternetPct:0} % Verlust ins Internet, Heimnetz ist sauber → Anschluss/Anbieter.");

        // CPU-Lüfter steht, obwohl die CPU warm ist → wahrscheinlich Defekt oder Kabel ab
        _cpuFanStoppedTicks = s.CpuFanRpm is 0 && s.CpuTemp > 60 ? _cpuFanStoppedTicks + 1 : 0;
        if (_cpuFanStoppedTicks >= 5)
            Raise("cpufan", "CPU-Lüfter steht!", $"0 U/min bei {s.CpuTemp:0} °C CPU-Temperatur. Bitte Lüfter prüfen.");
    }

    private void Raise(string key, string title, string text)
    {
        var now = DateTime.Now;
        if (_lastAlert.TryGetValue(key, out var last) && now - last < Cooldown) return;
        _lastAlert[key] = now;
        Log.Warn($"Warnung: {title} – {text}");
        Alert?.Invoke(title, text);
    }
}

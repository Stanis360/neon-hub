namespace NeonMonitor.Models;

/// <summary>
/// Eine Messung aller Werte zu einem Zeitpunkt.
/// <para>
/// <c>null</c> bedeutet immer „Sensor nicht verfügbar“ – nie 0. So kann die Oberfläche
/// fehlende Werte als „—“ anzeigen und die spätere CSV-Aufzeichnung leere Zellen schreiben.
/// </para>
/// Die Sensorquellen füllen ein Snapshot-Objekt nacheinander; jede Quelle setzt nur Werte,
/// die noch <c>null</c> sind (so dienen spätere Quellen als Fallback).
/// </summary>
public sealed class HardwareSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    // ---------- CPU ----------
    public string? CpuName { get; set; }
    /// <summary>Gesamtlast in %.</summary>
    public double? CpuLoad { get; set; }
    /// <summary>Temperatur in °C (bei Ryzen: Tctl/Tdie).</summary>
    public double? CpuTemp { get; set; }
    /// <summary>Durchschnittlicher Kerntakt in MHz.</summary>
    public double? CpuClockAvg { get; set; }
    /// <summary>Höchster Kerntakt in MHz.</summary>
    public double? CpuClockMax { get; set; }
    /// <summary>Package-Leistung in Watt.</summary>
    public double? CpuPower { get; set; }
    /// <summary>Takt jedes einzelnen Kerns in MHz (Reihenfolge Kern 1 … n).</summary>
    public List<CoreClock> CpuCoreClocks { get; set; } = new();

    // ---------- GPU ----------
    public string? GpuName { get; set; }
    /// <summary>GPU-Last in %.</summary>
    public double? GpuLoad { get; set; }
    /// <summary>Kerntemperatur (Edge) in °C.</summary>
    public double? GpuTemp { get; set; }
    /// <summary>Hotspot-/Junction-Temperatur in °C.</summary>
    public double? GpuHotspot { get; set; }
    /// <summary>Kerntakt in MHz.</summary>
    public double? GpuClock { get; set; }
    /// <summary>Speichertakt in MHz.</summary>
    public double? GpuMemClock { get; set; }
    /// <summary>Belegter Grafikspeicher in MB.</summary>
    public double? GpuVramUsedMb { get; set; }
    /// <summary>Gesamter Grafikspeicher in MB.</summary>
    public double? GpuVramTotalMb { get; set; }
    /// <summary>Leistungsaufnahme in Watt.</summary>
    public double? GpuPower { get; set; }
    /// <summary>Lüfterdrehzahl in U/min.</summary>
    public double? GpuFan { get; set; }

    // ---------- RAM ----------
    public double? RamUsedGb { get; set; }
    public double? RamTotalGb { get; set; }
    /// <summary>RAM-Belegung in %.</summary>
    public double? RamLoad { get; set; }

    // ---------- Laufwerke ----------
    public List<DriveUsage> Drives { get; set; } = new();

    // ---------- FPS (über PresentMon / ETW) ----------
    /// <summary>Bilder pro Sekunde der erfassten Anwendung (Mittel der letzten Sekunde).</summary>
    public double? Fps { get; set; }
    /// <summary>Durchschnittliche Frametime der letzten Sekunde in ms.</summary>
    public double? FrameTimeMs { get; set; }
    /// <summary>Längste Frametime der letzten Sekunde in ms (Ruckler).</summary>
    public double? FrameTimeMaxMs { get; set; }
    /// <summary>1 % Low der letzten 30 Sekunden (= 1000 / 99. Perzentil der Frametimes).</summary>
    public double? Fps1PercentLow { get; set; }
    /// <summary>Name der erfassten Anwendung, z. B. „cod.exe“.</summary>
    public string? FpsApplication { get; set; }

    // Nur für die Anzeige (nicht für die CSV-Aufzeichnung):
    /// <summary>Die letzten Einzel-Frametimes (ms) für das Frametime-Diagramm.</summary>
    public double[] RecentFrameTimes { get; set; } = [];
    /// <summary>Alle Anwendungen, die gerade Bilder ausgeben (zur Auswahl).</summary>
    public string[] FpsCandidates { get; set; } = [];
}

/// <summary>Takt eines CPU-Kerns.</summary>
public sealed record CoreClock(string Name, double Mhz);

/// <summary>Belegung eines Laufwerks (Partition).</summary>
public sealed record DriveUsage(string Name, string Label, double UsedGb, double TotalGb)
{
    public double Percent => TotalGb > 0 ? UsedGb / TotalGb * 100.0 : 0;
}

namespace NeonMonitor.Services.Recording;

/// <summary>
/// Zusammenfassung einer Aufzeichnung. Wird als <c>.json</c> neben der CSV gespeichert,
/// damit die Vergleichsseite die Liste schnell laden kann, ohne alle CSVs zu lesen.
/// </summary>
public sealed class SessionSummary
{
    /// <summary>Anzeigename des Spiels (vom Nutzer änderbar).</summary>
    public string Game { get; set; } = "";
    /// <summary>Exe-Name der gemessenen Anwendung, z. B. „cod.exe“.</summary>
    public string? Application { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double DurationSeconds { get; set; }

    public string? CpuName { get; set; }
    public string? GpuName { get; set; }

    // ---- FPS (aus den Einzel-Frametimes berechnet) ----
    public int FrameCount { get; set; }
    public double? AvgFps { get; set; }
    /// <summary>1 % Low = 1000 / 99. Perzentil der Frametimes.</summary>
    public double? Low1 { get; set; }
    /// <summary>0,1 % Low = 1000 / 99,9. Perzentil der Frametimes.</summary>
    public double? Low01 { get; set; }
    public double? AvgFrameTimeMs { get; set; }

    // ---- Sensoren ----
    public double? MaxCpuTemp { get; set; }
    public double? MaxGpuTemp { get; set; }
    public double? MaxGpuHotspot { get; set; }
    public double? AvgCpuLoad { get; set; }
    public double? AvgGpuLoad { get; set; }
    public double? AvgCpuPower { get; set; }
    public double? AvgGpuPower { get; set; }
    public double? MaxRamLoad { get; set; }
    public double? AvgCpuFan { get; set; }
    public double? AvgCaseFan { get; set; }

    // ---- Energie (nur CPU-Package + Grafikkarte, ohne Rest des PCs) ----
    /// <summary>Ø Leistung CPU + GPU in Watt.</summary>
    public double? AvgTotalPower { get; set; }
    /// <summary>Verbrauchte Energie CPU + GPU in Wattstunden.</summary>
    public double? EnergyWh { get; set; }
    /// <summary>Kosten in € (Strompreis zum Zeitpunkt der Aufnahme).</summary>
    public double? CostEuro { get; set; }
    /// <summary>Ø FPS pro Watt (CPU + GPU).</summary>
    public double? FpsPerWatt { get; set; }

    /// <summary>Dateiname der Sensor-CSV (eine Zeile pro Messung).</summary>
    public string CsvFile { get; set; } = "";
    /// <summary>Dateiname der Frametime-CSV (eine Zeile pro Bild).</summary>
    public string FramesFile { get; set; } = "";

    /// <summary>Pfad der JSON-Datei selbst (wird beim Laden gesetzt, nicht gespeichert).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string JsonPath { get; set; } = "";
}

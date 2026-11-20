using System.Text.Json;

namespace NeonMonitor.Infrastructure;

/// <summary>Vom Nutzer änderbare Einstellungen; werden als JSON in %AppData% gespeichert.</summary>
public sealed class AppSettings
{
    /// <summary>Abfrageintervall der Sensoren in Millisekunden.</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>Hintergrundanimation an/aus (aus = weniger CPU/GPU-Last beim Spielen).</summary>
    public bool BackgroundAnimation { get; set; } = true;

    /// <summary>
    /// Sicherer Modus: verzichtet auf den PawnIO-Kernel-Treiber (keine CPU-Temperatur/-Leistung),
    /// falls ein Anti-Cheat sich daran stört. Wirkt nach Neustart.
    /// </summary>
    public bool SafeMode { get; set; }

    // ---------- Overlay ----------

    /// <summary>Overlay beim Start anzeigen (merkt sich den letzten Zustand).</summary>
    public bool OverlayVisible { get; set; }

    /// <summary>Angezeigte Werte (Schlüssel aus OverlayViewModel), in fester Reihenfolge.</summary>
    public List<string> OverlayItems { get; set; } =
        ["game", "fps", "low1", "cpuload", "cputemp", "gpuload", "gputemp", "gpuhot", "gpupower", "ramload", "chart"];

    /// <summary>Letzte Position (physische Pixel, linke obere Ecke) oder null = automatisch.</summary>
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }

    /// <summary>Vergrößerung (1.0 = 100 %).</summary>
    public double OverlayScale { get; set; } = 1.0;

    /// <summary>Deckkraft des Hintergrunds (0 = unsichtbar, 1 = voll).</summary>
    public double OverlayBackgroundOpacity { get; set; } = 0.75;

    /// <summary>Fixiert: Mausklicks gehen durch das Overlay hindurch, Verschieben nicht möglich.</summary>
    public bool OverlayClickThrough { get; set; }

    /// <summary>Wie viele Sekunden die Live-Diagramme zurückreichen.</summary>
    public int HistorySeconds { get; set; } = 120;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new();
        }
        catch (Exception ex)
        {
            Log.Warn($"Einstellungen konnten nicht gelesen werden, nutze Standardwerte: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Warn($"Einstellungen konnten nicht gespeichert werden: {ex.Message}");
        }
    }
}

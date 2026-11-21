using System.Text.Json;
using NeonMonitor.Services.Sensors;

namespace NeonMonitor.Infrastructure;

/// <summary>Vom Nutzer änderbare Einstellungen; werden als JSON in %AppData% gespeichert.</summary>
public sealed class AppSettings
{
    /// <summary>Abfrageintervall der Sensoren in Millisekunden.</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>Gewähltes Design (Kennung aus ThemeCatalog).</summary>
    public string Theme { get; set; } = "graphit";

    /// <summary>Hintergrundanimation an/aus (aus = weniger CPU/GPU-Last beim Spielen).</summary>
    public bool BackgroundAnimation { get; set; } = true;

    /// <summary>
    /// Sicherer Modus: verzichtet auf den PawnIO-Kernel-Treiber (keine CPU-Temperatur/-Leistung),
    /// falls ein Anti-Cheat sich daran stört. Wirkt nach Neustart.
    /// </summary>
    public bool SafeMode { get; set; }

    /// <summary>Eigene Programme, die bei der FPS-Messung nie als Spiel gelten (Exe-Namen).</summary>
    public List<string> FpsIgnoredApps { get; set; } = new();

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

    // ---------- Kühlung ----------

    /// <summary>Eigene Namen für Lüfteranschlüsse (LHM-Kennung → Name, z. B. „Gehäuselüfter (4×)“).</summary>
    public Dictionary<string, string> FanNames { get; set; } = new();
    /// <summary>Welcher Anschluss ist der CPU-Lüfter bzw. die Gehäuselüfter (LHM-Kennung, null = automatisch).</summary>
    public string? CpuFanId { get; set; }
    public string? CaseFanId { get; set; }
    /// <summary>Hauptschalter der Lüftersteuerung (aus = Mainboard/BIOS regelt).</summary>
    public bool FanControlEnabled { get; set; }
    public List<FanCurveConfig> FanCurves { get; set; } = new();

    // ---------- Programmverhalten ----------

    /// <summary>Schließen-Knopf versteckt das Fenster nur (Programm läuft im Infobereich weiter).</summary>
    public bool CloseToTray { get; set; } = true;
    /// <summary>Minimieren versteckt das Fenster im Infobereich.</summary>
    public bool MinimizeToTray { get; set; }

    // ---------- Aufnahme ----------

    /// <summary>Aufnahme automatisch starten, sobald ein Spiel läuft, und beim Beenden stoppen.</summary>
    public bool AutoRecord { get; set; }
    /// <summary>Dauer des Benchmark-Modus in Sekunden.</summary>
    public int BenchmarkSeconds { get; set; } = 60;
    /// <summary>Strompreis in €/kWh für die Kostenberechnung.</summary>
    public double PricePerKwh { get; set; } = 0.35;

    // ---------- Handy-Ansicht ----------

    /// <summary>Webseite im Heimnetz für Handy/Tablet einschalten.</summary>
    public bool WebEnabled { get; set; }
    public int WebPort { get; set; } = 8085;
    /// <summary>Zufälliger Schlüssel in der Adresse – ohne ihn gibt es keine Daten.</summary>
    public string? WebToken { get; set; }

    // ---------- Handy-Deck ----------

    /// <summary>Darf das Handy Deck-Knöpfe am PC auslösen? Standard: aus.</summary>
    public bool DeckEnabled { get; set; }
    /// <summary>Knöpfe des Decks in Anzeigereihenfolge.</summary>
    public List<NeonMonitor.Services.Deck.DeckButton> DeckButtons { get; set; } = NeonMonitor.Services.Deck.DeckButton.Defaults();

    // ---------- Updates ----------

    /// <summary>Automatisch auf GitHub nach neuen Versionen suchen.</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>Mit „Später“ übersprungene Version (wird dann nicht erneut gemeldet).</summary>
    public string? SkippedVersion { get; set; }

    // ---------- Warnungen ----------

    public bool AlertsEnabled { get; set; } = true;
    public double AlertCpuTemp { get; set; } = 90;
    public double AlertGpuTemp { get; set; } = 90;
    public double AlertHotspot { get; set; } = 105;

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

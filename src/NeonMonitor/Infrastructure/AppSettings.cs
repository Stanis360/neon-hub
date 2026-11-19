using System.Text.Json;

namespace NeonMonitor.Infrastructure;

/// <summary>Vom Nutzer änderbare Einstellungen; werden als JSON in %AppData% gespeichert.</summary>
public sealed class AppSettings
{
    /// <summary>Abfrageintervall der Sensoren in Millisekunden.</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>Hintergrundanimation an/aus (aus = weniger CPU/GPU-Last beim Spielen).</summary>
    public bool BackgroundAnimation { get; set; } = true;

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

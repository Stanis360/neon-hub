namespace NeonMonitor.Services.Recording;

/// <summary>Macht aus Exe-Namen lesbare Spielnamen für Dateinamen und Anzeige.</summary>
public static class GameNames
{
    /// <summary>Bekannte Spiele. Weitere Einträge einfach ergänzen (Exe-Name ohne „.exe“, klein).</summary>
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cod"] = "Call of Duty",
        ["cod24"] = "Call of Duty",
        ["cod25"] = "Call of Duty",
        ["cod26"] = "Call of Duty",
        ["FactoryGame-Win64-Shipping"] = "Satisfactory",
        ["FactoryGameSteam-Win64-Shipping"] = "Satisfactory",
        ["FactoryGameEGS-Win64-Shipping"] = "Satisfactory",
        ["FactoryGame"] = "Satisfactory",
        ["DSPGAME"] = "Dyson Sphere Program",
    };

    /// <summary>„FactoryGame-Win64-Shipping.exe“ → „Satisfactory“, sonst Exe-Name ohne Endung.</summary>
    public static string FromApplication(string? application)
    {
        if (string.IsNullOrWhiteSpace(application)) return "Unbekannt";
        var name = Path.GetFileNameWithoutExtension(application);
        if (Known.TryGetValue(name, out var known)) return known;
        // Unreal-Engine-Anhängsel entfernen: „MyGame-Win64-Shipping“ → „MyGame“
        foreach (var suffix in new[] { "-Win64-Shipping", "-WinGDK-Shipping", "_x64", "-Win64" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) name = name[..^suffix.Length];
        return name;
    }

    /// <summary>Entfernt Zeichen, die in Dateinamen nicht erlaubt sind.</summary>
    public static string ToFileName(string game)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(game.Trim().Select(c => invalid.Contains(c) || c == ';' ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Unbekannt" : clean;
    }
}

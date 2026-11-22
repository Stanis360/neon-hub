namespace NeonMonitor.Services.Deck;

/// <summary>Ein Knopf des Handy-Decks.</summary>
public sealed class DeckButton
{
    /// <summary>Kurze, zufällige Kennung (wird vom Handy beim Auslösen geschickt).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = "Neu";
    /// <summary>Emoji oder Zeichen als Symbol, z. B. „🎙“.</summary>
    public string Icon { get; set; } = "★";
    /// <summary>Hintergrundfarbe als Hex, z. B. „#3DDC97“.</summary>
    public string Color { get; set; } = "#4A5459";
    /// <summary>„hotkey“, „program“, „url“, „media“, „text“, „game“, „power“ oder „neon“.</summary>
    public string Type { get; set; } = "hotkey";
    /// <summary>Tastenkombination, Pfad, Adresse, Medienaktion, Text oder NeonMonitor-Aktion.</summary>
    public string Value { get; set; } = "";

    public DeckButton Clone() => (DeckButton)MemberwiseClone();

    /// <summary>Vorschlag für den Start – lässt sich im Reiter „Deck“ beliebig ändern.</summary>
    public static List<DeckButton> Defaults() =>
    [
        new() { Icon = "●", Label = "Aufnahme", Color = "#E5626F", Type = "neon", Value = "record" },
        .. LauncherDefaults().Take(4),  // Steam, Opera GX, Epic Games, Satisfactory
        PlannerButton(),
        .. LauncherDefaults().Skip(4),  // Call of Duty
        new() { Icon = "◧", Label = "Overlay", Color = "#3DDC97", Type = "neon", Value = "overlay" },
        new() { Icon = "🎙", Label = "Discord stumm", Color = "#5AA9E6", Type = "hotkey", Value = "Strg+Umschalt+M" },
        new() { Icon = "📸", Label = "Screenshot", Color = "#C77DFF", Type = "hotkey", Value = "Win+Umschalt+S" },
        SpotifyButton(),
        .. PowerDefaults(),
    ];

    public static DeckButton PlannerButton() =>
        new() { Icon = "🗺", Label = "Satisfactory-Planer", Color = "#E8B44C", Type = "url", Value = "https://satisfactory-calculator.com" };

    public static DeckButton SpotifyButton() =>
        new() { Icon = "🎵", Label = "Spotify", Color = "#3DDC97", Type = "game", Value = "spotify" };

    /// <summary>Launcher und Spiele (Installationsort wird beim Auslösen gesucht, siehe <see cref="AppLauncher"/>).</summary>
    public static List<DeckButton> LauncherDefaults() =>
    [
        new() { Icon = "🎮", Label = "Steam", Color = "#5AA9E6", Type = "game", Value = "steam" },
        new() { Icon = "🌐", Label = "Opera GX", Color = "#E5626F", Type = "game", Value = "operagx" },
        new() { Icon = "🛒", Label = "Epic Games", Color = "#4A5459", Type = "game", Value = "epic" },
        new() { Icon = "🏭", Label = "Satisfactory", Color = "#E8B44C", Type = "game", Value = "satisfactory" },
        new() { Icon = "🎯", Label = "Call of Duty", Color = "#3DDC97", Type = "game", Value = "cod" },
    ];

    /// <summary>
    /// Einmalige Umbauten gespeicherter Decks. Alle anderen Knöpfe bleiben unverändert; was schon da ist,
    /// wird nicht doppelt angelegt. Gibt true zurück, wenn sich etwas geändert hat.
    /// <list type="bullet">
    /// <item>Version 1: „Benchmark“ und „Adrenalin“ raus, an ihre Stelle Steam, Opera GX, Epic Games, Satisfactory, Call of Duty.</item>
    /// <item>Version 2: Play/Pause, Weiter, Stumm, Leiser, Lauter und Task-Manager raus; Satisfactory-Planer (hinter
    /// „Satisfactory“) und Spotify (an die Stelle der Medienknöpfe) rein.</item>
    /// </list>
    /// </summary>
    public static bool MigrateLayout(List<DeckButton> buttons, ref int version)
    {
        if (version >= 2) return false;

        if (version < 1)
        {
            var at = RemoveAll(buttons, b => b.Type == "neon" && b.Value == "benchmark",
                                        b => b.Type == "hotkey" && (b.Label == "Adrenalin" || b.Value == "Alt+R"));
            InsertMissing(buttons, at, LauncherDefaults());
        }

        var mediaAt = RemoveAll(buttons,
            b => b.Type == "media" && b.Value == "playpause", b => b.Type == "media" && b.Value == "next",
            b => b.Type == "media" && b.Value == "mute", b => b.Type == "media" && b.Value == "voldown",
            b => b.Type == "media" && b.Value == "volup",
            b => b.Type == "hotkey" && (b.Label == "Task-Manager" || b.Value == "Strg+Umschalt+Escape"));
        var satisfactory = buttons.FindIndex(b => b.Type == "game" && b.Value == "satisfactory");
        InsertMissing(buttons, satisfactory < 0 ? -1 : satisfactory + 1, [PlannerButton()]);
        // Medienknöpfe lagen hinter dem Planer → Position nach dessen Einfügen um eins verschieben
        if (mediaAt >= 0 && satisfactory >= 0 && satisfactory < mediaAt) mediaAt++;
        InsertMissing(buttons, mediaAt, [SpotifyButton()]);

        version = 2;
        return true;
    }

    /// <summary>Entfernt je den ersten Treffer und gibt die kleinste frei gewordene Position zurück (-1 = nichts entfernt).</summary>
    private static int RemoveAll(List<DeckButton> buttons, params Predicate<DeckButton>[] matches)
    {
        var at = -1;
        foreach (var match in matches)
        {
            var i = buttons.FindIndex(match);
            if (i < 0) continue;
            buttons.RemoveAt(i);
            at = at < 0 ? i : Math.Min(at, i);
        }
        return at;
    }

    /// <summary>Fügt fehlende Knöpfe ein (gleiche Art + Wert = schon vorhanden); ohne Position vor die Energie-Knöpfe.</summary>
    private static void InsertMissing(List<DeckButton> buttons, int at, List<DeckButton> wanted)
    {
        var missing = wanted.Where(w => !buttons.Any(b => b.Type == w.Type && b.Value == w.Value)).ToList();
        if (missing.Count == 0) return;
        if (at < 0 || at > buttons.Count)
        {
            var power = buttons.FindIndex(b => b.Type == "power");
            at = power < 0 ? buttons.Count : power;
        }
        buttons.InsertRange(at, missing);
    }

    /// <summary>
    /// Energie-Knöpfe für „+ Energie-Knöpfe“: Ausschalten und Abbrechen (stoppt die 15-s-Vorwarnung).
    /// Neustart, Standby usw. lassen sich bei Bedarf über die Aktionsart „System“ anlegen.
    /// </summary>
    public static List<DeckButton> PowerDefaults() =>
    [
        new() { Icon = "⏻", Label = "Ausschalten", Color = "#E5626F", Type = "power", Value = "shutdown" },
        new() { Icon = "✋", Label = "Abbrechen", Color = "#3DDC97", Type = "power", Value = "abort" },
    ];
}

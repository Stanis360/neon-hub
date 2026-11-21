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
        .. LauncherDefaults(),
        new() { Icon = "◧", Label = "Overlay", Color = "#3DDC97", Type = "neon", Value = "overlay" },
        new() { Icon = "🎙", Label = "Discord stumm", Color = "#5AA9E6", Type = "hotkey", Value = "Strg+Umschalt+M" },
        new() { Icon = "📸", Label = "Screenshot", Color = "#C77DFF", Type = "hotkey", Value = "Win+Umschalt+S" },
        new() { Icon = "⏯", Label = "Play/Pause", Color = "#4A5459", Type = "media", Value = "playpause" },
        new() { Icon = "⏭", Label = "Weiter", Color = "#4A5459", Type = "media", Value = "next" },
        new() { Icon = "🔇", Label = "Stumm", Color = "#4A5459", Type = "media", Value = "mute" },
        new() { Icon = "🔉", Label = "Leiser", Color = "#4A5459", Type = "media", Value = "voldown" },
        new() { Icon = "🔊", Label = "Lauter", Color = "#4A5459", Type = "media", Value = "volup" },
        new() { Icon = "📊", Label = "Task-Manager", Color = "#4A5459", Type = "hotkey", Value = "Strg+Umschalt+Escape" },
        .. PowerDefaults(),
    ];

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
    /// Einmaliger Umbau gespeicherter Decks (Version 1): „Benchmark“ und „Adrenalin“ raus, an ihre Stelle
    /// Steam, Opera GX, Epic Games, Satisfactory und Call of Duty. Alle anderen Knöpfe bleiben unverändert;
    /// bereits vorhandene Launcher-Knöpfe werden nicht doppelt angelegt. Gibt true zurück, wenn sich etwas geändert hat.
    /// </summary>
    public static bool MigrateLayout(List<DeckButton> buttons, ref int version)
    {
        if (version >= 1) return false;
        version = 1;

        var insertAt = -1;
        void RemoveFirst(Predicate<DeckButton> match)
        {
            var i = buttons.FindIndex(match);
            if (i < 0) return;
            buttons.RemoveAt(i);
            insertAt = insertAt < 0 ? i : Math.Min(insertAt, i);
        }
        RemoveFirst(b => b.Type == "neon" && b.Value == "benchmark");
        RemoveFirst(b => b.Type == "hotkey" && (b.Label == "Adrenalin" || b.Value == "Alt+R"));

        var missing = LauncherDefaults().Where(l => !buttons.Any(b => b.Type == "game" && b.Value == l.Value)).ToList();
        if (insertAt < 0)
        {
            // Nichts ersetzt: vor die Energie-Knöpfe, sonst ans Ende
            var power = buttons.FindIndex(b => b.Type == "power");
            insertAt = power < 0 ? buttons.Count : power;
        }
        buttons.InsertRange(insertAt, missing);
        return true;
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

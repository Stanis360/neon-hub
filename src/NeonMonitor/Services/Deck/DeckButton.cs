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
    /// <summary>„hotkey“, „program“, „url“, „media“, „text“, „power“ oder „neon“.</summary>
    public string Type { get; set; } = "hotkey";
    /// <summary>Tastenkombination, Pfad, Adresse, Medienaktion, Text oder NeonMonitor-Aktion.</summary>
    public string Value { get; set; } = "";

    public DeckButton Clone() => (DeckButton)MemberwiseClone();

    /// <summary>Vorschlag für den Start – lässt sich im Reiter „Deck“ beliebig ändern.</summary>
    public static List<DeckButton> Defaults() =>
    [
        new() { Icon = "●", Label = "Aufnahme", Color = "#E5626F", Type = "neon", Value = "record" },
        new() { Icon = "⏱", Label = "Benchmark", Color = "#E8B44C", Type = "neon", Value = "benchmark" },
        new() { Icon = "◧", Label = "Overlay", Color = "#3DDC97", Type = "neon", Value = "overlay" },
        new() { Icon = "🎙", Label = "Discord stumm", Color = "#5AA9E6", Type = "hotkey", Value = "Strg+Umschalt+M" },
        new() { Icon = "📸", Label = "Screenshot", Color = "#C77DFF", Type = "hotkey", Value = "Win+Umschalt+S" },
        new() { Icon = "🔴", Label = "Adrenalin", Color = "#E5626F", Type = "hotkey", Value = "Alt+R" },
        new() { Icon = "⏯", Label = "Play/Pause", Color = "#4A5459", Type = "media", Value = "playpause" },
        new() { Icon = "⏭", Label = "Weiter", Color = "#4A5459", Type = "media", Value = "next" },
        new() { Icon = "🔇", Label = "Stumm", Color = "#4A5459", Type = "media", Value = "mute" },
        new() { Icon = "🔉", Label = "Leiser", Color = "#4A5459", Type = "media", Value = "voldown" },
        new() { Icon = "🔊", Label = "Lauter", Color = "#4A5459", Type = "media", Value = "volup" },
        new() { Icon = "📊", Label = "Task-Manager", Color = "#4A5459", Type = "hotkey", Value = "Strg+Umschalt+Escape" },
        .. PowerDefaults(),
    ];

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

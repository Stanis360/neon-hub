using System.Globalization;

namespace NeonMonitor.Services.Deck;

/// <summary>
/// Tastenkombinationen als Text, z. B. „Strg+Umschalt+M“ ↔ virtuelle Tastencodes (Windows-VK).
/// Deutsche und englische Namen werden akzeptiert; unbekannte Tasten als Hex-Code („0xC0“).
/// Bewusst ohne WPF-Abhängigkeit (testbar und unabhängig von der Oberfläche).
/// </summary>
public static class Hotkey
{
    // Linke Varianten, damit die Scancodes eindeutig sind
    public const ushort VkCtrl = 0xA2, VkShift = 0xA0, VkAlt = 0xA4, VkWin = 0x5B;

    private static readonly Dictionary<string, ushort> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Strg"] = VkCtrl, ["Ctrl"] = VkCtrl, ["Control"] = VkCtrl,
        ["Umschalt"] = VkShift, ["Shift"] = VkShift,
        ["Alt"] = VkAlt,
        ["Win"] = VkWin, ["Windows"] = VkWin,
    };

    /// <summary>Anzeigename → VK. Der erste Name je Code wird beim Formatieren verwendet.</summary>
    private static readonly (string Name, ushort Vk)[] KeyTable = BuildTable();

    private static (string, ushort)[] BuildTable()
    {
        var t = new List<(string, ushort)>();
        for (var c = 'A'; c <= 'Z'; c++) t.Add((c.ToString(), c));
        for (var d = 0; d <= 9; d++) t.Add((d.ToString(), (ushort)('0' + d)));
        for (var f = 1; f <= 24; f++) t.Add(($"F{f}", (ushort)(0x6F + f)));
        for (var n = 0; n <= 9; n++) t.Add(($"Num{n}", (ushort)(0x60 + n)));
        t.AddRange(new (string, ushort)[]
        {
            ("Enter", 0x0D), ("Return", 0x0D), ("Escape", 0x1B), ("Esc", 0x1B), ("Space", 0x20), ("Leertaste", 0x20),
            ("Tab", 0x09), ("Back", 0x08), ("Backspace", 0x08), ("Delete", 0x2E), ("Entf", 0x2E), ("Insert", 0x2D), ("Einfg", 0x2D),
            ("Home", 0x24), ("Pos1", 0x24), ("End", 0x23), ("Ende", 0x23), ("PageUp", 0x21), ("Prior", 0x21), ("BildAuf", 0x21),
            ("PageDown", 0x22), ("Next", 0x22), ("BildAb", 0x22), ("Left", 0x25), ("Up", 0x26), ("Right", 0x27), ("Down", 0x28),
            ("PrintScreen", 0x2C), ("Snapshot", 0x2C), ("Druck", 0x2C), ("Pause", 0x13), ("Scroll", 0x91), ("CapsLock", 0x14),
            ("NumMultiply", 0x6A), ("Multiply", 0x6A), ("NumAdd", 0x6B), ("Add", 0x6B), ("NumSubtract", 0x6D), ("Subtract", 0x6D),
            ("NumDecimal", 0x6E), ("Decimal", 0x6E), ("NumDivide", 0x6F), ("Divide", 0x6F),
            ("Plus", 0xBB), ("OemPlus", 0xBB), ("Komma", 0xBC), ("OemComma", 0xBC), ("Minus", 0xBD), ("OemMinus", 0xBD),
            ("Punkt", 0xBE), ("OemPeriod", 0xBE),
        });
        return t.ToArray();
    }

    /// <summary>„Strg+Umschalt+M“ → (Zusatztasten, Haupttaste). Null bei ungültigem Text.</summary>
    public static (List<ushort> Modifiers, ushort Key)? Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var mods = new List<ushort>();
        foreach (var p in parts[..^1])
        {
            if (!ModifierNames.TryGetValue(p, out var vk)) return null;
            if (!mods.Contains(vk)) mods.Add(vk);
        }
        var key = KeyFromName(parts[^1]);
        return key is null ? null : (mods, key.Value);
    }

    /// <summary>Text für eine gedrückte Kombination (Aufnahme im Editor).</summary>
    public static string Format(bool ctrl, bool shift, bool alt, bool win, ushort vk)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("Strg");
        if (shift) parts.Add("Umschalt");
        if (alt) parts.Add("Alt");
        if (win) parts.Add("Win");
        parts.Add(NameOf(vk));
        return string.Join("+", parts);
    }

    /// <summary>Strg/Umschalt/Alt/Win selbst (links/rechts/allgemein).</summary>
    public static bool IsModifierVk(int vk) => vk is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;

    public static string NameOf(ushort vk)
    {
        foreach (var (name, code) in KeyTable)
            if (code == vk) return name;
        return $"0x{vk:X2}";
    }

    private static ushort? KeyFromName(string name)
    {
        foreach (var (n, code) in KeyTable)
            if (n.Equals(name, StringComparison.OrdinalIgnoreCase)) return code;
        if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && ushort.TryParse(name[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) && hex is > 0 and < 0xFF)
            return hex;
        return null;
    }
}

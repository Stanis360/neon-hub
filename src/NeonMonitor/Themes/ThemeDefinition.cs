namespace NeonMonitor.Themes;

/// <summary>
/// Ein Design: alle Farben (als Hex „#RRGGBB“ oder „#AARRGGBB“), Schriften und Formen.
/// Aus diesen Werten baut <see cref="ThemeManager"/> beim Start die WPF-Ressourcen; dieselben Werte
/// nutzen Hintergrund-Animation, Overlay und die Handy-Seite.
/// </summary>
public sealed record ThemeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    // ---- Grundfarben ----
    public required string Background { get; init; }
    public required string BackgroundTop { get; init; }
    public required string BackgroundBottom { get; init; }
    public required string Panel { get; init; }
    public required string PanelBorder { get; init; }
    public required string Track { get; init; }
    public required string Chrome { get; init; }
    public required string Divider { get; init; }
    /// <summary>Innere Flächen (Zeilen in Listen, Kacheln).</summary>
    public required string Inner { get; init; }
    public required string Selected { get; init; }
    public required string Button { get; init; }
    public required string ButtonHover { get; init; }
    public required string ButtonPressed { get; init; }
    public required string ScrollThumb { get; init; }
    public required string ScrollThumbHover { get; init; }

    // ---- Akzente & Text ----
    public required string Accent { get; init; }
    public required string Accent2 { get; init; }
    /// <summary>Zweite Diagrammlinie / innerer Ring (Temperaturen).</summary>
    public required string Secondary { get; init; }
    public required string Text { get; init; }
    public required string TextDim { get; init; }
    /// <summary>Textfarbe auf Akzent-Hintergrund (z. B. gewählter Chip).</summary>
    public required string OnAccent { get; init; }
    public string Warn { get; init; } = "#E8B44C";
    public string Critical { get; init; } = "#E5626F";

    // ---- Formen & Schrift ----
    public double CardRadius { get; init; } = 14;
    public double SmallRadius { get; init; } = 6;
    /// <summary>Schrift für Überschriften/Werte (Font-Familien-Liste, „./Fonts/#…“ = eingebettet).</summary>
    public string DisplayFont { get; init; } = "Bahnschrift, Segoe UI";
    public string UiFont { get; init; } = "Segoe UI Variable Text, Segoe UI";
    /// <summary>Schrift für große Zahlen (Ringe, FPS).</summary>
    public string BigFont { get; init; } = "Bahnschrift, Segoe UI";
    /// <summary>Schrift der Kartenüberschriften (null = wie <see cref="DisplayFont"/>).</summary>
    public string? TitleFont { get; init; }
    /// <summary>Vergrößerung der großen Zahlen (Pixelschriften wirken bei gleicher Größe kleiner).</summary>
    public double BigScale { get; init; } = 1.0;
    /// <summary>Text vor jeder Kartenüberschrift (z. B. „&gt; “ im Terminal-Design).</summary>
    public string TitlePrefix { get; init; } = "";
    /// <summary>Bildröhren-Look: feine Zeilen und abgedunkelte Ränder über dem ganzen Fenster.</summary>
    public bool Crt { get; init; }
    public bool TitleUppercase { get; init; }
    public bool TitleLeft { get; init; }
    /// <summary>Kartenüberschriften in Akzentfarbe (sonst Textfarbe).</summary>
    public bool TitleAccent { get; init; } = true;
    /// <summary>Balken/Ringe als Verlauf von Akzent zu <see cref="GradientEnd"/>.</summary>
    public bool Gradient { get; init; }
    /// <summary>Endfarbe des Verlaufs (null = Secondary).</summary>
    public string? GradientEnd { get; init; }

    // ---- Hintergrund-Animation ----
    public required string Glow1 { get; init; }
    public required string Glow2 { get; init; }
    /// <summary>Farbe der Rasterlinien inkl. Deckkraft.</summary>
    public string GridLine { get; init; } = "#0CFFFFFF";
    /// <summary>Hintergrund-Effekt, wenn unter Einstellungen „Wie Design“ gewählt ist (siehe <see cref="BackgroundEffects"/>).</summary>
    public string Effect { get; init; } = BackgroundEffects.Grid;
    /// <summary>Farbe von Meteoren/Blitzen (null = Akzentfarbe; grau/weiß = weiße Meteore).</summary>
    public string? EffectColor { get; init; }
}

/// <summary>Die wählbaren Hintergrund-Effekte (Kennungen wie in den Einstellungen gespeichert).</summary>
public static class BackgroundEffects
{
    /// <summary>Nur in den Einstellungen: den Effekt des Designs verwenden.</summary>
    public const string FromTheme = "design";
    public const string Grid = "grid";
    public const string Meteors = "meteor";
    public const string Lightning = "lightning";
    public const string Aura = "aura";
    public const string Aurora = "aurora";
    public const string Petals = "petals";
    public const string Ocean = "ocean";
    public const string Warp = "warp";
    public const string Rain = "rain";
    public const string Terminal = "terminal";
    public const string None = "none";

    /// <summary>Alle konkreten Effekte mit Anzeigenamen, in der Reihenfolge der Auswahl.</summary>
    public static IReadOnlyList<(string Id, string Name)> All { get; } =
    [
        (Grid, "Raster"), (Meteors, "Meteorschauer"), (Lightning, "Blitze"), (Aura, "Ki-Aura"), (Aurora, "Polarlicht"),
        (Petals, "Blütenregen"), (Ocean, "Tiefsee"), (Warp, "Sternenflug"), (Rain, "Neon-Regen"), (Terminal, "Datenstrom"),
        (None, "Nur Farbe"),
    ];

    /// <summary>Ist ein konkreter Effekt gewählt (nicht „Wie Design“ oder ein unbekannter Wert)?</summary>
    public static bool IsExplicit(string? setting) => setting != null && All.Any(e => e.Id == setting);

    /// <summary>Löst „Wie Design“ und unbekannte Werte zu einem konkreten Effekt auf.</summary>
    public static string Resolve(string? setting, ThemeDefinition theme) => IsExplicit(setting) ? setting! : theme.Effect;
}

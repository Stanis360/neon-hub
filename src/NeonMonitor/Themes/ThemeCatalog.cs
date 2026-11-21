namespace NeonMonitor.Themes;

/// <summary>Alle verfügbaren Designs (umschaltbar unter Einstellungen → Design).</summary>
public static class ThemeCatalog
{
    public static readonly ThemeDefinition Graphit = new()
    {
        Id = "graphit", Name = "Graphit & Mint", Description = "Dunkles Anthrazit, ruhiges Mintgrün (bisheriges Design)",
        Background = "#15181A", BackgroundTop = "#141719", BackgroundBottom = "#1C2023",
        Panel = "#EB23282B", PanelBorder = "#33404A50", Track = "#373E43", Chrome = "#F0191C1E", Divider = "#2A3236",
        Inner = "#1C2124", Selected = "#262C30", Button = "#2A3034", ButtonHover = "#353D42", ButtonPressed = "#3F484E",
        ScrollThumb = "#404950", ScrollThumbHover = "#5A656C",
        Accent = "#3DDC97", Accent2 = "#2BA59A", Secondary = "#C5D0CD", Text = "#E4EAE8", TextDim = "#8B969A", OnAccent = "#15181A",
        Glow1 = "#3DDC97", Glow2 = "#3DDC97",
    };

    public static readonly ThemeDefinition Midnight = new()
    {
        Id = "midnight", Name = "Midnight Blue", Description = "Nachtblau, Cyan-Lila-Verläufe, gläserne Karten",
        Background = "#0B1020", BackgroundTop = "#0B1020", BackgroundBottom = "#121A33",
        Panel = "#D1161F3A", PanelBorder = "#247896FF", Track = "#25304F", Chrome = "#F00A0F1E", Divider = "#1E2848",
        Inner = "#141C36", Selected = "#1A2445", Button = "#1A2445", ButtonHover = "#223058", ButtonPressed = "#2A3A6A",
        ScrollThumb = "#2A3760", ScrollThumbHover = "#3D4D85",
        Accent = "#4CC9F0", Accent2 = "#7C9CF5", Secondary = "#B794F6", Text = "#E6EBFF", TextDim = "#8A94B8", OnAccent = "#0B1020",
        CardRadius = 16, SmallRadius = 8, Gradient = true,
        DisplayFont = "Segoe UI Variable Display, Segoe UI", BigFont = "Segoe UI Variable Display Semibold, Segoe UI Semibold, Segoe UI",
        Glow1 = "#4CC9F0", Glow2 = "#B794F6",
    };

    public static readonly ThemeDefinition Carbon = new()
    {
        Id = "carbon", Name = "Carbon & Orange", Description = "Fast schwarz, kantig, orange Akzente, technische Schrift",
        Background = "#0D0D0E", BackgroundTop = "#0D0D0E", BackgroundBottom = "#121214",
        Panel = "#F218181B", PanelBorder = "#26262B", Track = "#2A2A30", Chrome = "#F00A0A0B", Divider = "#232328",
        Inner = "#141416", Selected = "#1F1F23", Button = "#1F1F23", ButtonHover = "#2A2A30", ButtonPressed = "#34343B",
        ScrollThumb = "#34343B", ScrollThumbHover = "#4A4A52",
        Accent = "#FF8A3D", Accent2 = "#FFB27A", Secondary = "#F4F4F5", Text = "#F4F4F5", TextDim = "#8E8E99", OnAccent = "#0D0D0E",
        CardRadius = 4, SmallRadius = 2, TitleUppercase = true, TitleLeft = true,
        DisplayFont = "./Fonts/#Rajdhani, ./Fonts/#Rajdhani Medium, Bahnschrift SemiCondensed, Bahnschrift", BigFont = "./Fonts/#Rajdhani, ./Fonts/#Rajdhani SemiBold, Bahnschrift",
        Glow1 = "#FF8A3D", Glow2 = "#FF8A3D", GridLine = "#08FFFFFF",
    };

    public static readonly ThemeDefinition Rubin = new()
    {
        Id = "rubin", Name = "Rubin", Description = "Schwarz-Anthrazit mit Rot, im Stil von AMD Adrenalin",
        Background = "#101012", BackgroundTop = "#101012", BackgroundBottom = "#17171A",
        Panel = "#F21D1D21", PanelBorder = "#2B2B31", Track = "#33333A", Chrome = "#F00C0C0E", Divider = "#26262B",
        Inner = "#18181B", Selected = "#2A1517", Button = "#26262B", ButtonHover = "#303036", ButtonPressed = "#3A3A41",
        ScrollThumb = "#3A3A41", ScrollThumbHover = "#55555E",
        Accent = "#E8353B", Accent2 = "#FF6B6F", Secondary = "#E9E9EC", Text = "#EDEDF0", TextDim = "#93939C", OnAccent = "#FFFFFF",
        CardRadius = 8, SmallRadius = 5, TitleLeft = true, TitleAccent = false,
        DisplayFont = "Segoe UI Variable Display, Segoe UI", BigFont = "Segoe UI Variable Display Semibold, Segoe UI Semibold, Segoe UI",
        Glow1 = "#E8353B", Glow2 = "#E8353B",
    };

    public static readonly ThemeDefinition Synthwave = new()
    {
        Id = "synthwave", Name = "Synthwave", Description = "Violett mit Pink-Cyan-Verläufen und Retro-Schrift",
        Background = "#150B2E", BackgroundTop = "#150B2E", BackgroundBottom = "#241047",
        Panel = "#C7251248", PanelBorder = "#40FF4FD8", Track = "#3A2466", Chrome = "#F0120826", Divider = "#2E1A55",
        Inner = "#1E0F3D", Selected = "#2E1659", Button = "#2E1659", ButtonHover = "#3A1D6E", ButtonPressed = "#472485",
        ScrollThumb = "#472485", ScrollThumbHover = "#5E30A8",
        Accent = "#FF4FD8", Accent2 = "#B86BFF", Secondary = "#22E4FF", Text = "#F7EEFF", TextDim = "#A992D1", OnAccent = "#150B2E",
        CardRadius = 14, SmallRadius = 6, TitleUppercase = true, Gradient = true,
        DisplayFont = "./Fonts/#Rajdhani, ./Fonts/#Rajdhani Medium, Bahnschrift", BigFont = "./Fonts/#Orbitron, Bahnschrift",
        Glow1 = "#FF4FD8", Glow2 = "#22E4FF", GridLine = "#1822E4FF",
    };

    public static readonly ThemeDefinition Mono = new()
    {
        Id = "mono", Name = "Graphit Mono", Description = "Nur Grautöne, schlicht und ruhig – mit weißem Meteorschauer",
        Background = "#101113", BackgroundTop = "#101113", BackgroundBottom = "#16171A",
        Panel = "#E01D1F22", PanelBorder = "#2A2D31", Track = "#2E3135", Chrome = "#F00E0F11", Divider = "#232629",
        Inner = "#191B1E", Selected = "#222428", Button = "#23262A", ButtonHover = "#2D3135", ButtonPressed = "#373B40",
        ScrollThumb = "#3A3E43", ScrollThumbHover = "#555A60",
        Accent = "#E3E6E9", Accent2 = "#B9BEC4", Secondary = "#8D949B", Text = "#ECEEF0", TextDim = "#7E858C", OnAccent = "#101113",
        CardRadius = 10, SmallRadius = 5, TitleLeft = true, TitleAccent = false, Gradient = true,
        DisplayFont = "Segoe UI Variable Display, Segoe UI", BigFont = "Segoe UI Variable Display Semibold, Segoe UI Semibold, Segoe UI",
        Glow1 = "#FFFFFF", Glow2 = "#FFFFFF", GridLine = "#08FFFFFF",
        Effect = BackgroundEffects.Meteors, EffectColor = "#FFFFFF",
    };

    public static readonly ThemeDefinition Meteor = new()
    {
        Id = "meteor", Name = "Meteor", Description = "Tiefschwarz mit Orange und verglühenden Meteoren",
        Background = "#060607", BackgroundTop = "#060607", BackgroundBottom = "#0C0C0E",
        Panel = "#DB131315", PanelBorder = "#29FF7A1A", Track = "#29292E", Chrome = "#EB080809", Divider = "#1E1E22",
        Inner = "#111113", Selected = "#1F1813", Button = "#1D1D21", ButtonHover = "#27272C", ButtonPressed = "#313137",
        ScrollThumb = "#313137", ScrollThumbHover = "#4A4A52",
        Accent = "#FF7A1A", Accent2 = "#FFC38A", Secondary = "#FFC38A", Text = "#F3F3F4", TextDim = "#8C8C96", OnAccent = "#060607",
        CardRadius = 6, SmallRadius = 3, TitleUppercase = true, TitleLeft = true, Gradient = true, GradientEnd = "#FFB347",
        DisplayFont = "./Fonts/#Rajdhani, ./Fonts/#Rajdhani Medium, Bahnschrift SemiCondensed, Bahnschrift", BigFont = "./Fonts/#Rajdhani, ./Fonts/#Rajdhani SemiBold, Bahnschrift",
        Glow1 = "#FF7A1A", Glow2 = "#FF7A1A", GridLine = "#08FFFFFF",
        Effect = BackgroundEffects.Meteors,
    };

    public static readonly ThemeDefinition KiAura = new()
    {
        Id = "kiaura", Name = "Ki-Aura", Description = "Anime-Stil: Orange und Blau, goldene Energie-Aura, Comic-Schrift",
        Background = "#0A0D1C", BackgroundTop = "#0A0D1C", BackgroundBottom = "#141A3C",
        Panel = "#D6101630", PanelBorder = "#4DFF8C1A", Track = "#232B55", Chrome = "#F0090C1A", Divider = "#1E2548",
        Inner = "#121834", Selected = "#2A2140", Button = "#1C2346", ButtonHover = "#252E5A", ButtonPressed = "#2F3A6E",
        ScrollThumb = "#2F3A6E", ScrollThumbHover = "#44528F",
        Accent = "#FF8C1A", Accent2 = "#FFD23F", Secondary = "#3D8BFF", Text = "#FFF6E8", TextDim = "#98A2CC", OnAccent = "#0A0D1C",
        CardRadius = 12, SmallRadius = 6, TitleUppercase = true, TitleLeft = true, Gradient = true, GradientEnd = "#FFD23F",
        DisplayFont = "Segoe UI Variable Display, Segoe UI", BigFont = "./Fonts/#Bangers, Impact", TitleFont = "./Fonts/#Bangers, Impact",
        Glow1 = "#FF8C1A", Glow2 = "#3D8BFF", GridLine = "#0AFFD23F",
        Effect = BackgroundEffects.Aura,
    };

    public static IReadOnlyList<ThemeDefinition> All { get; } = [Graphit, Mono, Meteor, Midnight, Carbon, Rubin, Synthwave, KiAura];

    public static ThemeDefinition Get(string? id) => All.FirstOrDefault(t => t.Id == id) ?? Graphit;
}

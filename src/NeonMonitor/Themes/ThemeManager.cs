using System.Windows;
using System.Windows.Media;

namespace NeonMonitor.Themes;

/// <summary>
/// Lädt ein Design: erzeugt aus der <see cref="ThemeDefinition"/> ein ResourceDictionary mit allen
/// Farb-, Pinsel-, Schrift- und Form-Schlüsseln und lädt danach Theme.xaml (Stile) und Templates.xaml.
/// Muss vor dem ersten Fenster aufgerufen werden; ein Wechsel wird nach einem Neustart wirksam.
/// </summary>
public static class ThemeManager
{
    /// <summary>Aktuell geladenes Design.</summary>
    public static ThemeDefinition Current { get; private set; } = ThemeCatalog.Graphit;

    public static void Apply(Application app, string? themeId)
    {
        Current = ThemeCatalog.Get(themeId);
        var res = app.Resources.MergedDictionaries;
        res.Clear();
        res.Add(BuildPalette(Current));
        res.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Theme.xaml") });
        res.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Templates.xaml") });
    }

    public static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static ResourceDictionary BuildPalette(ThemeDefinition t)
    {
        var d = new ResourceDictionary();
        void C(string key, string hex)
        {
            var c = ColorOf(hex);
            d[key + "Color"] = c;
            var b = new SolidColorBrush(c);
            b.Freeze();
            d[key + "Brush"] = b;
        }

        C("Accent", t.Accent);
        C("Accent2", t.Accent2);
        C("Secondary", t.Secondary);
        C("Warn", t.Warn);
        C("Critical", t.Critical);
        C("Background", t.Background);
        C("Text", t.Text);
        C("TextDim", t.TextDim);
        C("Panel", t.Panel);
        C("PanelBorder", t.PanelBorder);
        C("Track", t.Track);
        C("Chrome", t.Chrome);
        C("Divider", t.Divider);
        C("Inner", t.Inner);
        C("Selected", t.Selected);
        C("Button", t.Button);
        C("ButtonHover", t.ButtonHover);
        C("ButtonPressed", t.ButtonPressed);
        C("OnAccent", t.OnAccent);
        C("ScrollThumb", t.ScrollThumb);
        C("ScrollThumbHover", t.ScrollThumbHover);

        var accent = ColorOf(t.Accent);
        var warn = ColorOf(t.Warn);
        d["HoverBrush"] = Frozen(new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)));
        d["PressedBrush"] = Frozen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));
        d["StatTileBrush"] = Frozen(new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)));
        d["WarnBackBrush"] = Frozen(new SolidColorBrush(Color.FromArgb(0x2A, warn.R, warn.G, warn.B)));
        d["AccentBackBrush"] = Frozen(new SolidColorBrush(Color.FromArgb(0x26, accent.R, accent.G, accent.B)));
        d["TooltipBrush"] = Frozen(new SolidColorBrush(ColorOf(t.Button)) { Opacity = 0.97 });

        // Balken und Ringe: Akzentfarbe oder Verlauf Akzent → Secondary
        Brush fill = t.Gradient
            ? new LinearGradientBrush(accent, ColorOf(t.GradientEnd ?? t.Secondary), 0)
            : new SolidColorBrush(accent);
        d["BarBrush"] = Frozen(fill);
        d["RingBrush"] = d["BarBrush"];

        // Schriften (eingebettete Schriften über pack-URI)
        var baseUri = new Uri("pack://application:,,,/");
        d["DisplayFont"] = new FontFamily(baseUri, t.DisplayFont);
        d["UiFont"] = new FontFamily(baseUri, t.UiFont);
        d["BigFont"] = new FontFamily(baseUri, t.BigFont);
        d["TitleFont"] = new FontFamily(baseUri, t.TitleFont ?? t.DisplayFont);
        d["MonoFont"] = new FontFamily("Cascadia Mono, Consolas");
        d["IconFont"] = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        // Formen und Überschriften
        d["CardCornerRadius"] = new CornerRadius(t.CardRadius);
        d["SmallCornerRadius"] = new CornerRadius(t.SmallRadius);
        d["TitleUppercase"] = t.TitleUppercase;
        d["TitleAlignment"] = t.TitleLeft ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        d["TitleBrush"] = t.TitleAccent ? d["AccentBrush"] : d["TextBrush"];
        return d;
    }

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}

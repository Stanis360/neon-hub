using System.Windows;
using System.Windows.Media;
using NeonMonitor.Themes;

namespace NeonMonitor.Controls;

/// <summary>
/// Bildröhren-Look für Designs mit <see cref="ThemeDefinition.Crt"/>: feine dunkle Zeilen und abgedunkelte
/// Ränder über dem ganzen Fenster. Statisch (keine Animation) und ohne Mausreaktion.
/// </summary>
public sealed class CrtOverlay : FrameworkElement
{
    private static readonly Brush Lines = CreateLines();
    private static readonly Brush Vignette = CreateVignette();

    public CrtOverlay()
    {
        IsHitTestVisible = false;
        Visibility = ThemeManager.Current.Crt ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var area = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Lines, null, area);
        dc.DrawRectangle(Vignette, null, area);
    }

    /// <summary>Alle 3 Pixel eine 1 Pixel hohe, leicht dunkle Zeile (gekachelt).</summary>
    private static Brush CreateLines()
    {
        var b = new DrawingBrush(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), null,
            new RectangleGeometry(new Rect(0, 0, 3, 1))))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 3, 3),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 3, 3),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        b.Freeze();
        return b;
    }

    private static Brush CreateVignette()
    {
        var b = new RadialGradientBrush { RadiusX = 0.75, RadiusY = 0.75 };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.6));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 0, 0, 0), 1));
        b.Freeze();
        return b;
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NeonMonitor.Services.Fps;

namespace NeonMonitor.Controls;

/// <summary>
/// Balkendiagramm der Frametime-Verteilung: Wie viel Prozent der Bilder lagen in welchem Bereich?
/// Ein schmaler, hoher Balken = gleichmäßiges Spielgefühl; Balken weit rechts = Ruckler.
/// </summary>
public sealed class HistogramChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(double[]), typeof(HistogramChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(HistogramChart),
        new FrameworkPropertyMetadata(Brushes.MediumSeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Anteile je Klasse (Summe 1), Reihenfolge wie <see cref="FrameAnalysis.BinLabels"/>.</summary>
    public double[]? Values { get => (double[]?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xC0, 0x8A, 0x95, 0x99)));
    private static readonly Brush ValueBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE4, 0xEA, 0xE8)));
    private static readonly Brush TrackBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
    private static readonly Typeface Font = new("Bahnschrift, Segoe UI");

    protected override void OnRender(DrawingContext dc)
    {
        var labels = FrameAnalysis.BinLabels;
        var values = Values is { Length: > 0 } v ? v : new double[labels.Length];
        var w = ActualWidth;
        var h = ActualHeight;
        const double labelHeight = 28, valueHeight = 14;
        var plotH = h - labelHeight - valueHeight;
        if (w < 50 || plotH < 20) return;

        var n = labels.Length;
        var slot = w / n;
        var barW = slot * 0.7;
        var max = Math.Max(values.Max(), 0.01);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (var i = 0; i < n; i++)
        {
            var x = i * slot + (slot - barW) / 2;
            var frac = i < values.Length ? values[i] : 0;
            var barH = frac / max * plotH;

            dc.DrawRoundedRectangle(TrackBrush, null, new Rect(x, valueHeight, barW, plotH), 2, 2);
            if (barH > 0.5)
                dc.DrawRoundedRectangle(i >= 7 ? Brushes.IndianRed : Fill, null, new Rect(x, valueHeight + plotH - barH, barW, barH), 2, 2);

            if (frac >= 0.005)
            {
                var t = Text($"{frac * 100:0}%", 10, ValueBrush, dpi);
                dc.DrawText(t, new Point(x + barW / 2 - t.Width / 2, Math.Max(0, valueHeight + plotH - barH - t.Height)));
            }

            var lbl = Text(labels[i], 9, LabelBrush, dpi);
            lbl.TextAlignment = TextAlignment.Center;
            lbl.MaxTextWidth = slot;
            dc.DrawText(lbl, new Point(i * slot, h - labelHeight + 2));
        }
    }

    private static FormattedText Text(string s, double size, Brush brush, double dpi) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dpi);

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}

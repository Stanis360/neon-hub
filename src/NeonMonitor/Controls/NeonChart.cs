using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NeonMonitor.ViewModels;

namespace NeonMonitor.Controls;

/// <summary>
/// Leichtgewichtiges Liniendiagramm im Neon-Look (bis zu zwei Verläufe).
/// Zeichnet direkt per <see cref="OnRender"/> – keine externe Chart-Bibliothek nötig,
/// sehr sparsam (wichtig, weil das Tool neben Spielen läuft).
/// <para>Neuester Wert rechts. NaN-Werte erzeugen eine Lücke.</para>
/// </summary>
public sealed class NeonChart : FrameworkElement
{
    // ---------------- Dependency Properties ----------------

    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(ChartSeries), typeof(NeonChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSeriesChanged));

    public static readonly DependencyProperty Series2Property = DependencyProperty.Register(
        nameof(Series2), typeof(ChartSeries), typeof(NeonChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSeriesChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(NeonChart),
        new FrameworkPropertyMetadata(Brushes.Lime, FrameworkPropertyMetadataOptions.AffectsRender, OnStyleChanged));

    public static readonly DependencyProperty Stroke2Property = DependencyProperty.Register(
        nameof(Stroke2), typeof(Brush), typeof(NeonChart),
        new FrameworkPropertyMetadata(Brushes.Cyan, FrameworkPropertyMetadataOptions.AffectsRender, OnStyleChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(NeonChart),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(NeonChart),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Hauptverlauf (mit Flächenfüllung).</summary>
    public ChartSeries? Series { get => (ChartSeries?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }

    /// <summary>Optionaler zweiter Verlauf (nur Linie).</summary>
    public ChartSeries? Series2 { get => (ChartSeries?)GetValue(Series2Property); set => SetValue(Series2Property, value); }

    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush Stroke2 { get => (Brush)GetValue(Stroke2Property); set => SetValue(Stroke2Property, value); }

    /// <summary>Obergrenze der Y-Achse. Wird automatisch erweitert, wenn Werte darüber liegen.</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Einheit für die Achsenbeschriftung.</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    // ---------------- Zeichenressourcen (gecacht) ----------------

    private static readonly Pen GridPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0x39, 0xFF, 0x14)), 1)
    {
        DashStyle = new DashStyle([2, 4], 0)
    });
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x90, 0x6F, 0xA7, 0x7E)));
    private static readonly Typeface LabelFont = new("Cascadia Mono, Consolas");

    private Pen? _pen1, _glow1, _pen2, _glow2;
    private Brush? _fill1;

    private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (NeonChart)d;
        if (e.OldValue is ChartSeries old) old.Changed -= chart.OnDataChanged;
        if (e.NewValue is ChartSeries neu) neu.Changed += chart.OnDataChanged;
    }

    private static void OnStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NeonChart)d)._pen1 = null; // Stifte beim nächsten Zeichnen neu erzeugen

    private void OnDataChanged(object? sender, EventArgs e)
    {
        // Mehrere Serien melden kurz nacheinander – WPF fasst InvalidateVisual zusammen.
        if (IsVisible) InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 10 || h < 10) return;

        EnsurePens();

        // Y-Achse: mindestens Maximum, bei höheren Werten aufrunden
        var max = Math.Max(Maximum, Math.Max(SeriesMax(Series), SeriesMax(Series2)));
        if (max > Maximum) max = Math.Ceiling(max / 10) * 10;

        // Hilfslinien bei 0/50/100 %
        const double labelSpace = 34;
        var plotW = w - labelSpace;
        foreach (var frac in new[] { 0.0, 0.5, 1.0 })
        {
            var y = Math.Round(h - frac * (h - 1)) + 0.5;
            dc.DrawLine(GridPen, new Point(0, y), new Point(plotW, y));
            var text = new FormattedText($"{max * frac:0}{Unit}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, LabelFont, 9, LabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(plotW + 4, Math.Clamp(y - text.Height / 2, 0, h - text.Height)));
        }

        var clip = new RectangleGeometry(new Rect(0, 0, plotW, h));
        dc.PushClip(clip);
        DrawSeries(dc, Series, plotW, h, max, _pen1!, _glow1!, _fill1);
        DrawSeries(dc, Series2, plotW, h, max, _pen2!, _glow2!, null);
        dc.Pop();
    }

    private static void DrawSeries(DrawingContext dc, ChartSeries? s, double w, double h, double max,
                                   Pen pen, Pen glow, Brush? fill)
    {
        if (s is null || s.Count < 2) return;

        var step = w / (s.Capacity - 1);
        var offset = (s.Capacity - s.Count) * step; // Daten von rechts einlaufen lassen

        var line = new StreamGeometry();
        var area = fill is null ? null : new StreamGeometry();

        using (var lc = line.Open())
        using (var ac = area?.Open())
        {
            var inFigure = false;
            Point first = default, last = default;

            for (var i = 0; i < s.Count; i++)
            {
                var v = s[i];
                if (double.IsNaN(v))
                {
                    if (inFigure) CloseArea(ac, first, last, h);
                    inFigure = false;
                    continue;
                }

                var p = new Point(offset + i * step, h - Math.Clamp(v / max, 0, 1) * (h - 2) - 1);
                if (!inFigure)
                {
                    lc.BeginFigure(p, false, false);
                    ac?.BeginFigure(new Point(p.X, h), true, true);
                    ac?.LineTo(p, false, false);
                    first = p;
                    inFigure = true;
                }
                else
                {
                    lc.LineTo(p, true, true);
                    ac?.LineTo(p, false, false);
                }
                last = p;
            }
            if (inFigure) CloseArea(ac, first, last, h);
        }

        line.Freeze();
        if (area is not null)
        {
            area.Freeze();
            dc.DrawGeometry(fill, null, area);
        }
        dc.DrawGeometry(null, glow, line); // breiter, transparenter Strich = Leuchteffekt
        dc.DrawGeometry(null, pen, line);
    }

    private static void CloseArea(StreamGeometryContext? ac, Point first, Point last, double h) =>
        ac?.LineTo(new Point(last.X, h), false, false);

    private static double SeriesMax(ChartSeries? s)
    {
        if (s is null) return 0;
        var max = 0.0;
        for (var i = 0; i < s.Count; i++)
            if (!double.IsNaN(s[i]) && s[i] > max) max = s[i];
        return max;
    }

    private void EnsurePens()
    {
        if (_pen1 is not null) return;
        var c1 = ColorOf(Stroke);
        var c2 = ColorOf(Stroke2);

        _pen1 = Freeze(new Pen(new SolidColorBrush(c1), 1.6) { LineJoin = PenLineJoin.Round });
        _glow1 = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x30, c1.R, c1.G, c1.B)), 6) { LineJoin = PenLineJoin.Round });
        _pen2 = Freeze(new Pen(new SolidColorBrush(c2), 1.4) { LineJoin = PenLineJoin.Round });
        _glow2 = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x28, c2.R, c2.G, c2.B)), 5) { LineJoin = PenLineJoin.Round });
        _fill1 = Freeze(new LinearGradientBrush(
            Color.FromArgb(0x50, c1.R, c1.G, c1.B), Color.FromArgb(0x00, c1.R, c1.G, c1.B), 90));
    }

    private static Color ColorOf(Brush b) => b is SolidColorBrush sc ? sc.Color : Colors.Lime;

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}

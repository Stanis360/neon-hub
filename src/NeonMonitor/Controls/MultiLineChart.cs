using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NeonMonitor.Controls;

/// <summary>Eine Linie im Vergleichsdiagramm: X = Sekunden seit Start, Y = Messwert (NaN = Lücke).</summary>
public sealed record ChartLine(double[] X, double[] Y, Color Color);

/// <summary>
/// Diagramm mit beliebig vielen Linien über eine gemeinsame Zeitachse – für den Sitzungsvergleich.
/// Kürzere Sitzungen enden entsprechend früher.
/// </summary>
public sealed class MultiLineChart : FrameworkElement
{
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
        nameof(Lines), typeof(IReadOnlyList<ChartLine>), typeof(MultiLineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(MultiLineChart),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(
        nameof(StartDate), typeof(DateTime?), typeof(MultiLineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty XMaxProperty = DependencyProperty.Register(
        nameof(XMax), typeof(double), typeof(MultiLineChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Gesetzt → X-Werte sind Tage ab diesem Datum, Beschriftung „dd.MM.“ statt mm:ss.</summary>
    public DateTime? StartDate { get => (DateTime?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }

    /// <summary>Feste Breite der X-Achse (z. B. 30 Tage); NaN = aus den Daten.</summary>
    public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }

    public IReadOnlyList<ChartLine>? Lines { get => (IReadOnlyList<ChartLine>?)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    private static readonly Pen GridPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)), 1)
    {
        DashStyle = new DashStyle([2, 4], 0),
    });
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xA0, 0x8A, 0x95, 0x99)));
    private static readonly Typeface LabelFont = new("Cascadia Mono, Consolas");

    protected override void OnRender(DrawingContext dc)
    {
        const double left = 4, right = 44, bottom = 18;
        var w = ActualWidth - left - right;
        var h = ActualHeight - bottom;
        if (w < 20 || h < 20) return;

        var lines = Lines ?? [];
        var xMax = double.IsNaN(XMax) ? lines.SelectMany(l => l.X).DefaultIfEmpty(1).Max() : XMax;
        var yMax = lines.SelectMany(l => l.Y).Where(double.IsFinite).DefaultIfEmpty(1).Max();
        yMax = NiceCeiling(Math.Max(yMax * 1.05, 1));
        if (xMax <= 0) xMax = 1;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Y-Hilfslinien mit Beschriftung rechts
        foreach (var f in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var y = Math.Round(h - f * (h - 1)) + 0.5;
            dc.DrawLine(GridPen, new Point(left, y), new Point(left + w, y));
            var t = Text($"{yMax * f:0}{Unit}", dpi);
            dc.DrawText(t, new Point(left + w + 4, Math.Clamp(y - t.Height / 2, 0, h - t.Height)));
        }

        // X-Beschriftung: Start, Mitte, Ende als mm:ss
        foreach (var f in new[] { 0.0, 0.5, 1.0 })
        {
            var label = StartDate is { } start
                ? start.AddDays(Math.Round(xMax * f)).ToString("dd.MM.")
                : TimeSpan.FromSeconds(xMax * f).ToString(xMax >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");
            var t = Text(label, dpi);
            var x = left + f * w - (f switch { 0 => 0, 1 => t.Width, _ => t.Width / 2 });
            dc.DrawText(t, new Point(x, h + 3));
        }

        dc.PushClip(new RectangleGeometry(new Rect(left, 0, w, h)));
        foreach (var line in lines)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                var open = false;
                for (var i = 0; i < Math.Min(line.X.Length, line.Y.Length); i++)
                {
                    if (!double.IsFinite(line.Y[i])) { open = false; continue; }
                    var p = new Point(left + line.X[i] / xMax * w, h - Math.Clamp(line.Y[i] / yMax, 0, 1) * (h - 2) - 1);
                    if (!open) { ctx.BeginFigure(p, false, false); open = true; }
                    else ctx.LineTo(p, true, true);
                }
            }
            geo.Freeze();
            var brush = Freeze(new SolidColorBrush(line.Color));
            dc.DrawGeometry(null, Freeze(new Pen(brush, 1.6) { LineJoin = PenLineJoin.Round }), geo);
            if (StartDate is not null) // Tageswerte zusätzlich als Punkte
                for (var i = 0; i < Math.Min(line.X.Length, line.Y.Length); i++)
                    if (double.IsFinite(line.Y[i]))
                        dc.DrawEllipse(brush, null, new Point(left + line.X[i] / xMax * w, h - Math.Clamp(line.Y[i] / yMax, 0, 1) * (h - 2) - 1), 2.5, 2.5);
        }
        dc.Pop();
    }

    private static FormattedText Text(string s, double dpi) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFont, 9, LabelBrush, dpi);

    /// <summary>Runde Obergrenze: 1, 2, 2.5, 5 × 10^n.</summary>
    private static double NiceCeiling(double v)
    {
        var exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * exp >= v) return m * exp;
        return 10 * exp;
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}

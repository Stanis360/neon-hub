using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NeonMonitor.Controls;

/// <summary>
/// Ringanzeige mit bis zu zwei Bögen (außen z. B. Last, innen Temperatur).
/// Der Ring ist zu 3/4 geschlossen, die Lücke liegt unten. Wertänderungen werden
/// kurz weich animiert. Beschriftungen in der Mitte legt man per XAML darüber.
/// </summary>
public sealed class RingGauge : FrameworkElement
{
    private const double StartAngle = 135; // Grad, im Uhrzeigersinn ab „3 Uhr“ → unten links
    private const double SweepAngle = 270; // Dreiviertelkreis

    public static readonly DependencyProperty ValueProperty = Register(nameof(Value), 0.0, OnValueChanged);
    public static readonly DependencyProperty MaximumProperty = Register(nameof(Maximum), 100.0);
    public static readonly DependencyProperty Value2Property = Register(nameof(Value2), double.NaN, OnValueChanged);
    public static readonly DependencyProperty Maximum2Property = Register(nameof(Maximum2), 100.0);
    public static readonly DependencyProperty ThicknessProperty = Register(nameof(Thickness), 14.0);

    // Animierte Anzeigewerte (folgen Value/Value2 mit kurzer Verzögerung)
    private static readonly DependencyProperty ShownProperty = Register("Shown", 0.0);
    private static readonly DependencyProperty Shown2Property = Register("Shown2", 0.0);

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.MediumSeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty Stroke2Property = DependencyProperty.Register(
        nameof(Stroke2), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Wert des äußeren Bogens.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Wert des inneren Bogens (NaN = innerer Ring ausgeblendet).</summary>
    public double Value2 { get => (double)GetValue(Value2Property); set => SetValue(Value2Property, value); }
    public double Maximum2 { get => (double)GetValue(Maximum2Property); set => SetValue(Maximum2Property, value); }

    /// <summary>Stärke des äußeren Rings in Pixeln (innerer Ring ist etwas dünner).</summary>
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush Stroke2 { get => (Brush)GetValue(Stroke2Property); set => SetValue(Stroke2Property, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    private static DependencyProperty Register(string name, double def, PropertyChangedCallback? changed = null) =>
        DependencyProperty.Register(name, typeof(double), typeof(RingGauge),
            new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender, changed));

    /// <summary>Neuen Wert sanft anfahren statt springen.</summary>
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var g = (RingGauge)d;
        var target = e.Property == ValueProperty ? ShownProperty : Shown2Property;
        var to = e.NewValue is double v && double.IsFinite(v) ? v : 0;
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Timeline.SetDesiredFrameRate(anim, 30);
        g.BeginAnimation(target, anim);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size < 20) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var t1 = Thickness;
        var t2 = Thickness * 0.55;
        var r1 = size / 2 - t1 / 2;
        var r2 = r1 - t1 / 2 - 6 - t2 / 2;

        // Außen: Hintergrundspur + Wert
        DrawArc(dc, center, r1, t1, TrackBrush, 1);
        DrawArc(dc, center, r1, t1, Stroke, Fraction((double)GetValue(ShownProperty), Maximum));

        // Innen (optional)
        if (!double.IsNaN(Value2) && r2 > t2)
        {
            DrawArc(dc, center, r2, t2, TrackBrush, 1);
            DrawArc(dc, center, r2, t2, Stroke2, Fraction((double)GetValue(Shown2Property), Maximum2));
        }
    }

    private static double Fraction(double v, double max) => max > 0 ? Math.Clamp(v / max, 0, 1) : 0;

    private static void DrawArc(DrawingContext dc, Point c, double r, double thickness, Brush brush, double fraction)
    {
        if (fraction <= 0.001) return;
        var sweep = SweepAngle * fraction;
        var start = PointOnCircle(c, r, StartAngle);
        var end = PointOnCircle(c, r, StartAngle + sweep);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(brush, thickness), geo);
    }

    private static Point PointOnCircle(Point c, double r, double degrees)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace NeonMonitor.Controls;

/// <summary>
/// Ruhiger, animierter Hintergrund: ein sehr dezentes, langsam driftendes Raster,
/// zwei weiche Lichtflecken und wenige aufsteigende Punkte.
/// <para>
/// Alle Bewegungen laufen als WPF-Animationen auf Transformationen (vom Render-Thread
/// komponiert) mit reduzierter Bildrate – das hält die Last minimal.
/// Über <see cref="IsAnimated"/> lässt sich alles anhalten (z. B. beim Spielen).
/// </para>
/// </summary>
public sealed class AnimatedBackground : Grid
{
    private const int ParticleCount = 16;
    private const int FrameRate = 30;  // reicht für langsame Bewegungen völlig
    private const double Cell = 48;    // Rastergröße in Pixeln

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(AnimatedBackground),
        new PropertyMetadata(true, (d, _) => ((AnimatedBackground)d).Restart()));

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    // Gleiche Akzentfarbe wie im Theme (Mintgrün), hier nur sehr transparent eingesetzt
    private static readonly Color Accent = Color.FromRgb(0x3D, 0xDC, 0x97);

    private readonly TranslateTransform _gridShift = new();
    private readonly Canvas _orbLayer = new() { IsHitTestVisible = false };
    private readonly Canvas _particleLayer = new() { IsHitTestVisible = false };
    private readonly List<(Ellipse Shape, TranslateTransform Move, double XFrac, double Seconds, double Delay)> _particles = new();
    private readonly List<(Ellipse Shape, TranslateTransform Move, Vector Range, double Seconds)> _orbs = new();
    private readonly Random _rng = new(9800);

    public AnimatedBackground()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        // Dunkles Anthrazit statt Schwarz-Grün
        Background = new LinearGradientBrush(Color.FromRgb(0x14, 0x17, 0x19), Color.FromRgb(0x1C, 0x20, 0x23), 90);

        Children.Add(CreateGridLayer());
        Children.Add(_orbLayer);
        Children.Add(_particleLayer);

        CreateOrbs();
        CreateParticles();

        SizeChanged += (_, _) => Restart();
        IsVisibleChanged += (_, _) => Restart();
    }

    // ---------------- Ebenen ----------------

    /// <summary>Gekacheltes Linienraster, das per Verschiebung endlos „fließt“.</summary>
    private UIElement CreateGridLayer()
    {
        var lines = new GeometryGroup();
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(Cell, 0)));
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, Cell)));

        var brush = new DrawingBrush(new GeometryDrawing(null,
            new Pen(new SolidColorBrush(Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF)), 1), lines))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, Cell, Cell),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, Cell, Cell),
            ViewboxUnits = BrushMappingMode.Absolute,
            Transform = _gridShift,
        };

        return new Rectangle
        {
            Fill = brush,
            // Raster zur Mitte hin sichtbar, an den Rändern ausblenden
            OpacityMask = new RadialGradientBrush(
                Color.FromArgb(0xFF, 0, 0, 0), Color.FromArgb(0x00, 0, 0, 0)) { RadiusX = 0.8, RadiusY = 0.8 },
        };
    }

    /// <summary>Zwei große, sehr weiche Lichtflecken, die langsam hin und her wandern.</summary>
    private void CreateOrbs()
    {
        (double Size, double X, double Y, Vector Range, double Seconds)[] defs =
        [
            (700, 0.10, 0.15, new Vector(240, 120), 50),
            (600, 0.85, 0.80, new Vector(-220, -140), 60),
        ];

        foreach (var (size, x, y, range, seconds) in defs)
        {
            var move = new TranslateTransform();
            var orb = new Ellipse
            {
                Width = size,
                Height = size,
                RenderTransform = move,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(0x12, Accent.R, Accent.G, Accent.B),
                    Color.FromArgb(0x00, Accent.R, Accent.G, Accent.B)),
                Tag = new Point(x, y), // relative Startposition
            };
            _orbLayer.Children.Add(orb);
            _orbs.Add((orb, move, range, seconds));
        }
    }

    /// <summary>Wenige, kleine Punkte, die langsam nach oben steigen.</summary>
    private void CreateParticles()
    {
        for (var i = 0; i < ParticleCount; i++)
        {
            var size = 1.5 + _rng.NextDouble() * 2;
            var move = new TranslateTransform();
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Accent),
                Opacity = 0.08 + _rng.NextDouble() * 0.18,
                RenderTransform = move,
            };
            _particleLayer.Children.Add(dot);
            _particles.Add((dot, move, _rng.NextDouble(), 25 + _rng.NextDouble() * 30, _rng.NextDouble() * 30));
        }
    }

    // ---------------- Animationen ----------------

    /// <summary>Startet alle Animationen passend zur aktuellen Größe neu (oder stoppt sie).</summary>
    private void Restart()
    {
        var w = ActualWidth;
        var h = ActualHeight;
        var run = IsAnimated && IsVisible && w > 0 && h > 0;

        // Lichtflecken auch im Standbild sinnvoll platzieren
        foreach (var (orb, _, _, _) in _orbs)
        {
            var start = (Point)orb.Tag;
            Canvas.SetLeft(orb, start.X * w - orb.Width / 2);
            Canvas.SetTop(orb, start.Y * h - orb.Height / 2);
        }

        _particleLayer.Visibility = run ? Visibility.Visible : Visibility.Collapsed;

        if (!run)
        {
            StopAll();
            return;
        }

        // Raster: eine Zellenbreite verschieben → nahtlose Endlosschleife (sehr langsam)
        Animate(_gridShift, TranslateTransform.XProperty, 0, Cell, 20, autoReverse: false);
        Animate(_gridShift, TranslateTransform.YProperty, 0, Cell, 20, autoReverse: false);

        foreach (var (_, move, range, seconds) in _orbs)
        {
            Animate(move, TranslateTransform.XProperty, 0, range.X, seconds, autoReverse: true);
            Animate(move, TranslateTransform.YProperty, 0, range.Y, seconds * 0.8, autoReverse: true);
        }

        foreach (var (dot, move, xFrac, seconds, delay) in _particles)
        {
            Canvas.SetLeft(dot, xFrac * w);
            Canvas.SetTop(dot, h + 10);
            Animate(move, TranslateTransform.YProperty, 0, -(h + 30), seconds, autoReverse: false, beginSeconds: -delay);
        }
    }

    private static void Animate(Animatable target, DependencyProperty prop, double from, double to,
                                double seconds, bool autoReverse, double beginSeconds = 0)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = autoReverse,
            RepeatBehavior = RepeatBehavior.Forever,
            // negativer Beginn = Animation startet „mittendrin“ → Punkte sofort verteilt
            BeginTime = TimeSpan.FromSeconds(beginSeconds),
        };
        if (autoReverse) anim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
        Timeline.SetDesiredFrameRate(anim, FrameRate);
        target.BeginAnimation(prop, anim);
    }

    private void StopAll()
    {
        _gridShift.BeginAnimation(TranslateTransform.XProperty, null);
        _gridShift.BeginAnimation(TranslateTransform.YProperty, null);
        foreach (var o in _orbs)
        {
            o.Move.BeginAnimation(TranslateTransform.XProperty, null);
            o.Move.BeginAnimation(TranslateTransform.YProperty, null);
        }
        foreach (var p in _particles) p.Move.BeginAnimation(TranslateTransform.YProperty, null);
    }
}

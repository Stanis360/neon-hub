using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace NeonMonitor.Controls;

/// <summary>
/// Animierter Hintergrund: driftendes Neon-Gitter, langsam wandernde Leuchtwolken,
/// ein Scan-Streifen und aufsteigende Partikel.
/// <para>
/// Alle Bewegungen laufen als WPF-Animationen auf Transformationen (vom Render-Thread
/// komponiert) mit reduzierter Bildrate – das hält die Last minimal.
/// Über <see cref="IsAnimated"/> lässt sich alles anhalten (z. B. beim Spielen).
/// </para>
/// </summary>
public sealed class NeonBackground : Grid
{
    private const int ParticleCount = 28;
    private const int FrameRate = 30; // reicht für langsame Bewegungen völlig

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(NeonBackground),
        new PropertyMetadata(true, (d, _) => ((NeonBackground)d).Restart()));

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    private static readonly Color Green = Color.FromRgb(0x39, 0xFF, 0x14);
    private static readonly Color Mint = Color.FromRgb(0x00, 0xFF, 0xA3);

    private readonly TranslateTransform _gridShift = new();
    private readonly TranslateTransform _scan = new();
    private readonly Canvas _orbLayer = new() { IsHitTestVisible = false };
    private readonly Canvas _particleLayer = new() { IsHitTestVisible = false };
    private readonly Rectangle _scanLine;
    private readonly List<(Ellipse Shape, TranslateTransform Move, double XFrac, double Seconds, double Delay)> _particles = new();
    private readonly List<(Ellipse Shape, TranslateTransform Move, Vector Range, double Seconds)> _orbs = new();
    private readonly Random _rng = new(9800);

    public NeonBackground()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Background = new LinearGradientBrush(Color.FromRgb(0x02, 0x07, 0x04), Color.FromRgb(0x04, 0x10, 0x09), 90);

        Children.Add(CreateGridLayer());
        Children.Add(_orbLayer);
        Children.Add(_particleLayer);
        _scanLine = CreateScanLine();
        Children.Add(_scanLine);
        Children.Add(CreateVignette());

        CreateOrbs();
        CreateParticles();

        SizeChanged += (_, _) => Restart();
        IsVisibleChanged += (_, _) => Restart();
    }

    // ---------------- Ebenen ----------------

    /// <summary>Gekacheltes Linienraster, das per Verschiebung endlos „fließt“.</summary>
    private UIElement CreateGridLayer()
    {
        const double cell = 44;
        var lines = new GeometryGroup();
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(cell, 0)));
        lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, cell)));

        var brush = new DrawingBrush(new GeometryDrawing(null,
            new Pen(new SolidColorBrush(Color.FromArgb(0x1C, Green.R, Green.G, Green.B)), 1), lines))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, cell, cell),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, cell, cell),
            ViewboxUnits = BrushMappingMode.Absolute,
            Transform = _gridShift,
        };

        return new Rectangle
        {
            Fill = brush,
            // Raster zur Mitte hin sichtbar, an den Rändern ausblenden
            OpacityMask = new RadialGradientBrush(
                Color.FromArgb(0xFF, 0, 0, 0), Color.FromArgb(0x10, 0, 0, 0)) { RadiusX = 0.75, RadiusY = 0.75 },
        };
    }

    private Rectangle CreateScanLine() => new()
    {
        Height = 160,
        VerticalAlignment = VerticalAlignment.Top,
        RenderTransform = _scan,
        Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new(Color.FromArgb(0x00, Green.R, Green.G, Green.B), 0),
            new(Color.FromArgb(0x0E, Green.R, Green.G, Green.B), 0.85),
            new(Color.FromArgb(0x30, Green.R, Green.G, Green.B), 1),
        }, 90),
    };

    private static Rectangle CreateVignette() => new()
    {
        Fill = new RadialGradientBrush(Color.FromArgb(0x00, 0, 0, 0), Color.FromArgb(0xB0, 0, 0, 0))
        {
            RadiusX = 0.8,
            RadiusY = 0.8,
        },
    };

    /// <summary>Große, weiche Leuchtwolken, die langsam hin und her wandern.</summary>
    private void CreateOrbs()
    {
        (Color Color, double Size, double X, double Y, Vector Range, double Seconds)[] defs =
        [
            (Green, 620, 0.05, 0.10, new Vector(260, 140), 38),
            (Mint, 520, 0.60, 0.55, new Vector(-220, -160), 46),
            (Green, 420, 0.80, 0.00, new Vector(-160, 220), 54),
        ];

        foreach (var (color, size, x, y, range, seconds) in defs)
        {
            var move = new TranslateTransform();
            var orb = new Ellipse
            {
                Width = size,
                Height = size,
                RenderTransform = move,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(0x26, color.R, color.G, color.B),
                    Color.FromArgb(0x00, color.R, color.G, color.B)),
                Tag = new Point(x, y), // relative Startposition
            };
            _orbLayer.Children.Add(orb);
            _orbs.Add((orb, move, range, seconds));
        }
    }

    /// <summary>Kleine Leuchtpunkte, die von unten nach oben steigen.</summary>
    private void CreateParticles()
    {
        for (var i = 0; i < ParticleCount; i++)
        {
            var size = 1.5 + _rng.NextDouble() * 2.5;
            var move = new TranslateTransform();
            var color = _rng.NextDouble() < 0.7 ? Green : Mint;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                Opacity = 0.25 + _rng.NextDouble() * 0.5,
                RenderTransform = move,
            };
            _particleLayer.Children.Add(dot);
            _particles.Add((dot, move, _rng.NextDouble(), 14 + _rng.NextDouble() * 22, _rng.NextDouble() * 20));
        }
    }

    // ---------------- Animationen ----------------

    /// <summary>Startet alle Animationen passend zur aktuellen Größe neu (oder stoppt sie).</summary>
    private void Restart()
    {
        var w = ActualWidth;
        var h = ActualHeight;
        var run = IsAnimated && IsVisible && w > 0 && h > 0;

        // Leuchtwolken auch im Standbild sinnvoll platzieren
        foreach (var (orb, _, _, _) in _orbs)
        {
            var start = (Point)orb.Tag;
            Canvas.SetLeft(orb, start.X * w - orb.Width / 2);
            Canvas.SetTop(orb, start.Y * h - orb.Height / 2);
        }

        _particleLayer.Visibility = run ? Visibility.Visible : Visibility.Collapsed;
        _scanLine.Visibility = run ? Visibility.Visible : Visibility.Collapsed;

        if (!run)
        {
            StopAll();
            return;
        }

        // Raster: eine Zellenbreite diagonal verschieben → nahtlose Endlosschleife
        Animate(_gridShift, TranslateTransform.XProperty, 0, 44, 6, autoReverse: false);
        Animate(_gridShift, TranslateTransform.YProperty, 0, 44, 6, autoReverse: false);

        Animate(_scan, TranslateTransform.YProperty, -160, h, 9, autoReverse: false);

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
            // negativer Beginn = Animation startet „mittendrin“ → Partikel sofort verteilt
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
        _scan.BeginAnimation(TranslateTransform.YProperty, null);
        foreach (var o in _orbs)
        {
            o.Move.BeginAnimation(TranslateTransform.XProperty, null);
            o.Move.BeginAnimation(TranslateTransform.YProperty, null);
        }
        foreach (var p in _particles) p.Move.BeginAnimation(TranslateTransform.YProperty, null);
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace NeonMonitor.Controls;

/// <summary>
/// Zeichenfläche für die Hintergrund-Effekte (Meteore, Blitze, Ki-Aura).
/// <para>
/// Jedes Bild wird neu in eine <see cref="DrawingGroup"/> gezeichnet, die das Element einmalig
/// rendert – WPF übernimmt Änderungen daran ohne Layout-Durchlauf. Die Bildrate ist auf ~60 fps
/// begrenzt (auch auf 240-Hz-Monitoren), gestoppt wird über <see cref="Stop"/>, z. B. wenn das
/// Fenster minimiert ist.
/// </para>
/// </summary>
public sealed class EffectLayer : FrameworkElement
{
    private const double MinFrameSeconds = 1.0 / 62;

    private readonly DrawingGroup _drawing = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private SkyEffect? _effect;
    private bool _running;
    private double _last;

    public EffectLayer() => IsHitTestVisible = false;

    /// <summary>Aktueller Effekt (null = leer).</summary>
    public SkyEffect? Sky
    {
        get => _effect;
        set
        {
            Stop();
            _effect = value;
        }
    }

    protected override void OnRender(DrawingContext dc) => dc.DrawDrawing(_drawing);

    public void Start()
    {
        if (_running || _effect == null) return;
        _running = true;
        _last = _clock.Elapsed.TotalSeconds;
        _effect.Reset(_last, new Size(ActualWidth, ActualHeight));
        CompositionTarget.Rendering += OnFrame;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnFrame;
        using (_drawing.Open()) { } // leeren
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var t = _clock.Elapsed.TotalSeconds;
        var dt = t - _last;
        if (dt < MinFrameSeconds || _effect == null) return;
        _last = t;

        var size = new Size(ActualWidth, ActualHeight);
        if (size.Width < 1 || size.Height < 1) return;

        try
        {
            _effect.Update(t, Math.Min(dt, 0.05), size);
            using var dc = _drawing.Open();
            _effect.Draw(dc, t, size);
        }
        catch (Exception ex)
        {
            // Ein Fehler im Effekt darf nie zu einer Meldungsflut führen: Effekt abschalten, nur protokollieren
            Infrastructure.Log.Error("Hintergrund-Effekt abgeschaltet", ex);
            Sky = null;
        }
    }
}

/// <summary>Grundgerüst eines Effekts: Zustand fortschreiben, dann zeichnen.</summary>
public abstract class SkyEffect
{
    protected readonly Random Rng = new();

    protected double R() => Rng.NextDouble();

    /// <summary>Startzustand herstellen (beim Einschalten der Animation).</summary>
    public abstract void Reset(double t, Size size);

    /// <summary>Simulation um <paramref name="dt"/> Sekunden weiterrechnen.</summary>
    public abstract void Update(double t, double dt, Size size);

    public abstract void Draw(DrawingContext dc, double t, Size size);

    /// <summary>Erzeugt den Effekt zur Kennung (siehe <see cref="Themes.BackgroundEffects"/>) oder null.</summary>
    public static SkyEffect? Create(string effect, Themes.ThemeDefinition theme)
    {
        var color = Themes.ThemeManager.ColorOf(theme.EffectColor ?? theme.Accent);
        return effect switch
        {
            Themes.BackgroundEffects.Meteors => new MeteorShower(color),
            Themes.BackgroundEffects.Lightning => new Thunderstorm(color),
            Themes.BackgroundEffects.Aura => new KiAura(
                Themes.ThemeManager.ColorOf(theme.Accent2),
                Themes.ThemeManager.ColorOf(theme.Accent),
                Themes.ThemeManager.ColorOf(theme.Secondary)),
            _ => null,
        };
    }
}

/// <summary>Farb-Hilfen; einfarbige Pinsel werden (gerundet) zwischengespeichert, um pro Bild kaum Objekte zu erzeugen.</summary>
internal static class Fx
{
    public static readonly Color White = Colors.White;
    private static readonly Dictionary<uint, SolidColorBrush> Brushes = new();

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static Color Mix(Color a, Color b, double f)
    {
        f = Math.Clamp(f, 0, 1);
        return Color.FromRgb((byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f));
    }

    public static Color A(Color c, double alpha) => Color.FromArgb((byte)(Math.Clamp(alpha, 0, 1) * 255), c.R, c.G, c.B);

    /// <summary>Sättigung 0…1 (0 = grau/weiß).</summary>
    public static double Saturation(Color c)
    {
        var max = Math.Max(c.R, Math.Max(c.G, c.B));
        var min = Math.Min(c.R, Math.Min(c.G, c.B));
        return max == 0 ? 0 : (max - min) / (double)max;
    }

    /// <summary>Einfarbiger Pinsel mit Deckkraft – auf 32 Stufen gerundet und eingefroren.</summary>
    public static Brush Solid(Color c, double alpha)
    {
        var a = (int)Math.Round(Math.Clamp(alpha, 0, 1) * 31) * 255 / 31;
        var key = ((uint)a << 24) | ((uint)(c.R & 0xF8) << 16) | ((uint)(c.G & 0xF8) << 8) | (uint)(c.B & 0xF8);
        if (!Brushes.TryGetValue(key, out var brush))
        {
            if (Brushes.Count > 4096) Brushes.Clear();
            brush = new SolidColorBrush(Color.FromArgb((byte)a, c.R, c.G, c.B));
            brush.Freeze();
            Brushes[key] = brush;
        }
        return brush;
    }

    /// <summary>Weicher, runder Lichtfleck (für Ellipsen; Mitte → Rand transparent).</summary>
    public static Brush Glow(params (double Offset, Color Color)[] stops)
    {
        var b = new RadialGradientBrush();
        foreach (var (o, c) in stops) b.GradientStops.Add(new GradientStop(c, o));
        b.Freeze();
        return b;
    }

    public static Pen RoundPen(Brush brush, double width)
    {
        var p = new Pen(brush, width) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        p.Freeze();
        return p;
    }
}

// ============================================================================================
//  Meteorschauer: Meteore verglühen in der Atmosphäre
// ============================================================================================

/// <summary>
/// Dicke Meteore mit keilförmigem Schweif. Der Kopf glüht weiß und flackert; der Schweif läuft von Weiß über die
/// Designfarbe ins Dunkle und wird mit der Zeit röter und dünner. Funken platzen ab und kühlen aus, am Ende
/// zerplatzt etwa jeder zweite Meteor in einen Funkenregen und hinterlässt kurz eine glimmende Spur.
/// </summary>
public sealed class MeteorShower : SkyEffect
{
    private sealed class Meteor { public double X, Y, V, L, W, T0, Life, Seed, SparkDebt; public bool Burst; }
    private sealed class Spark { public double X, Y, Vx, Vy, T0, Life, S; }
    private sealed record Trail(Point From, Point To, double W, double T0);
    private sealed record Star(double X, double Y, double S, double A, double P);

    // Flugrichtung: von rechts oben nach links unten
    private static readonly Vector Dir = new(-Math.Cos(Math.PI / 5), Math.Sin(Math.PI / 5));
    private static readonly Vector Normal = new(-Dir.Y, Dir.X);

    private readonly Color _core, _hot, _mid, _cool;
    private readonly List<Meteor> _meteors = new();
    private readonly List<Spark> _sparks = new();
    private readonly List<Trail> _trails = new();
    private readonly List<Star> _stars = new();
    private double _lastSpawn;

    public MeteorShower(Color accent)
    {
        if (Fx.Saturation(accent) < 0.15)
        {
            // Grau/Weiß-Designs: weiße Meteore mit silbernem Schweif
            (_core, _hot, _mid, _cool) = (Fx.White, Fx.Rgb(240, 244, 248), Fx.Rgb(200, 206, 214), Fx.Rgb(110, 116, 124));
        }
        else
        {
            _core = Fx.Rgb(255, 252, 240);
            _hot = Fx.Mix(accent, Fx.Rgb(255, 236, 170), 0.55);
            _mid = accent;
            _cool = Fx.Mix(accent, Fx.Rgb(90, 10, 0), 0.55);
        }
    }

    public override void Reset(double t, Size size)
    {
        _meteors.Clear();
        _sparks.Clear();
        _trails.Clear();
        _stars.Clear();
        for (var i = 0; i < 70; i++) _stars.Add(new Star(R(), R(), R() * 1.4 + 0.4, R() * 0.45 + 0.1, R() * 6));
        for (var i = 0; i < 8; i++) Spawn(t - R() * 1.4, size);
        _lastSpawn = t;
    }

    private void Spawn(double t0, Size size) => _meteors.Add(new Meteor
    {
        X = size.Width * (0.19 + R() * 0.95), Y = -60 - R() * 220,
        V = 420 + R() * 380, L = 240 + R() * 360, W = 10 + R() * 10,
        T0 = t0, Life = 1.7 + R() * 1.2, Burst = R() < 0.45, Seed = R() * 100,
    });

    private static Point Head(Meteor m, double t) => new(m.X + Dir.X * m.V * (t - m.T0), m.Y + Dir.Y * m.V * (t - m.T0));

    public override void Update(double t, double dt, Size size)
    {
        // Große Fenster bekommen etwas mehr Meteore
        var area = Math.Clamp(size.Width * size.Height / (1320.0 * 860), 0.6, 1.6);
        if (t - _lastSpawn > 0.3 / area && _meteors.Count < 12 * area)
        {
            Spawn(t, size);
            _lastSpawn = t;
        }

        for (var i = _meteors.Count - 1; i >= 0; i--)
        {
            var m = _meteors[i];
            var age = t - m.T0;
            var head = Head(m, t);
            if (age > m.Life)
            {
                // Verglüht: manchmal Funkenregen, immer eine kurz nachglimmende Spur
                if (m.Burst)
                    for (var k = 0; k < 22; k++)
                    {
                        var a = R() * Math.PI * 2;
                        var sp = 60 + R() * 260;
                        _sparks.Add(new Spark
                        {
                            X = head.X, Y = head.Y, T0 = t, Life = 0.4 + R() * 0.7, S = 1.5 + R() * 2.5,
                            Vx = Math.Cos(a) * sp + Dir.X * m.V * 0.25, Vy = Math.Sin(a) * sp + Dir.Y * m.V * 0.25,
                        });
                    }
                _trails.Add(new Trail(head, head - Dir * m.L * 0.8, m.W, t));
                _meteors.RemoveAt(i);
                continue;
            }

            // Abplatzende Funken (~32 pro Sekunde und Meteor)
            if (age < 0) continue;
            m.SparkDebt += dt * 32;
            while (m.SparkDebt >= 1)
            {
                m.SparkDebt--;
                _sparks.Add(new Spark
                {
                    X = head.X - Dir.X * m.W * R(), Y = head.Y - Dir.Y * m.W * R(), T0 = t,
                    Vx = Dir.X * m.V * (0.15 + R() * 0.25) + (R() - 0.5) * 90,
                    Vy = Dir.Y * m.V * (0.15 + R() * 0.25) + (R() - 0.5) * 90,
                    Life = 0.25 + R() * 0.55, S = 1.3 + R() * 2.4,
                });
            }
        }

        // Funken werden langsamer und sinken leicht
        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            if (t - s.T0 >= s.Life) { _sparks.RemoveAt(i); continue; }
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt;
            var drag = Math.Pow(0.96, dt * 60);
            s.Vx *= drag;
            s.Vy = s.Vy * drag + 40 * dt;
        }
        _trails.RemoveAll(tr => t - tr.T0 > 1.1);
    }

    public override void Draw(DrawingContext dc, double t, Size size)
    {
        // Funkelnder Sternenstaub
        foreach (var st in _stars)
            dc.DrawRectangle(Fx.Solid(_hot, st.A * (0.6 + 0.4 * Math.Sin(t * 1.5 + st.P))), null,
                new Rect(st.X * size.Width, st.Y * size.Height, st.S, st.S));

        // Ionisierte Spur nach dem Verglühen
        foreach (var tr in _trails)
        {
            var a = 1 - (t - tr.T0) / 1.1;
            dc.DrawLine(Fx.RoundPen(Fx.Solid(_cool, 0.18 * a), tr.W * 0.6), tr.From, tr.To);
        }

        foreach (var m in _meteors)
        {
            var age = t - m.T0;
            if (age < 0) continue;
            var f = Math.Min(1, age * 4) * Math.Min(1, (m.Life - age) * 2.2);
            if (f <= 0.01) continue;
            var flick = 0.82 + 0.18 * Math.Sin(t * 38 + m.Seed) * Math.Sin(t * 23 + m.Seed * 2);
            var burn = age / m.Life; // je weiter, desto röter
            var head = Head(m, t);
            var tail = head - Dir * m.L * (0.7 + 0.3 * Math.Min(1, age * 4));
            var w = m.W * (1 - burn * 0.35) * flick;

            var gradient = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = head, EndPoint = tail };
            gradient.GradientStops.Add(new GradientStop(Fx.A(_core, f), 0));
            gradient.GradientStops.Add(new GradientStop(Fx.A(Fx.Mix(_hot, _mid, burn * 0.6), 0.95 * f), 0.07));
            gradient.GradientStops.Add(new GradientStop(Fx.A(Fx.Mix(_mid, _cool, burn * 0.5), 0.6 * f), 0.35));
            gradient.GradientStops.Add(new GradientStop(Fx.A(_cool, 0), 1));
            gradient.Freeze();

            // Weicher Schein um den Schweif, dann der Schweif selbst (Keil: breit am Kopf, spitz am Ende)
            dc.PushOpacity(0.28);
            dc.DrawGeometry(gradient, null, Wedge(head, tail, w * 2.8));
            dc.Pop();
            dc.DrawGeometry(gradient, null, Wedge(head, tail, w));

            // Glühender Kopf mit Hitzeschein
            var r = w * 1.8 * flick;
            dc.DrawEllipse(Fx.Glow((0, Fx.A(_mid, 0.30 * f)), (1, Fx.A(_mid, 0))), null, head, r * 2, r * 2);
            dc.DrawEllipse(Fx.Glow((0, Fx.A(_core, f)), (0.35, Fx.A(_hot, 0.8 * f)), (1, Fx.A(_mid, 0))), null, head, r, r);
        }

        // Funken kühlen ab: heiß → Designfarbe → dunkel
        foreach (var s in _sparks)
        {
            var a = (t - s.T0) / s.Life;
            var c = a < 0.4 ? Fx.Mix(_core, _hot, a / 0.4) : Fx.Mix(_hot, _cool, (a - 0.4) / 0.6);
            var radius = s.S * (1 - a * 0.5);
            var p = new Point(s.X, s.Y);
            if (s.S > 2.6) dc.DrawEllipse(Fx.Solid(_mid, 0.22 * (1 - a)), null, p, radius * 2.4, radius * 2.4);
            dc.DrawEllipse(Fx.Solid(c, 1 - a), null, p, radius, radius);
        }
    }

    private static StreamGeometry Wedge(Point head, Point tail, double width)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(head + Normal * width / 2, true, true);
            ctx.LineTo(tail, false, false);
            ctx.LineTo(head - Normal * width / 2, false, false);
            ctx.ArcTo(head + Normal * width / 2, new Size(width / 2, width / 2), 0, false, SweepDirection.Clockwise, false, false); // Bogen nach vorn (Flugrichtung)
        }
        g.Freeze();
        return g;
    }
}

// ============================================================================================
//  Gewitter: verästelte Blitze mit Nachblitz und Wetterleuchten
// ============================================================================================

/// <summary>
/// Zackige, verästelte Blitze (Mittelpunkt-Verschiebung) in der Designfarbe mit weißem Kern.
/// Ablauf je Einschlag: greller Blitz – kurz dunkel – oft ein Nachblitz – langsames Verglimmen.
/// Dazu ein Lichtschein am Himmel, Funken am Einschlag und gelegentliches Wetterleuchten.
/// </summary>
public sealed class Thunderstorm : SkyEffect
{
    private sealed record Strike(StreamGeometry Main, StreamGeometry Branches, Point Impact, double T0, bool Restrike);
    private sealed record Sheet(Point Center, double Radius, double T0, double Alpha);
    private sealed class Spark { public double X, Y, Vx, Vy, T0, Life, S; }

    private readonly Color _acc, _glow, _soft;
    private readonly List<Strike> _strikes = new();
    private readonly List<Sheet> _sheets = new();
    private readonly List<Spark> _sparks = new();
    private readonly List<(double Time, double X)> _pending = new();
    private double _next;

    public Thunderstorm(Color accent)
    {
        _acc = accent;
        _glow = Fx.Mix(accent, Fx.White, 0.25);
        _soft = Fx.Mix(accent, Fx.White, 0.65);
    }

    public override void Reset(double t, Size size)
    {
        _strikes.Clear();
        _sheets.Clear();
        _sparks.Clear();
        _pending.Clear();
        _next = t + 0.4;
    }

    /// <summary>Aus einer Strecke wird durch wiederholtes Verschieben der Mittelpunkte eine Zickzacklinie.</summary>
    private List<Point> ZigZag(Point a, Point b, int depth, double rough)
    {
        var pts = new List<Point> { a, b };
        for (var d = 0; d < depth; d++)
        {
            var next = new List<Point>(pts.Count * 2) { pts[0] };
            for (var i = 1; i < pts.Count; i++)
            {
                var p = pts[i - 1];
                var q = pts[i];
                var v = q - p;
                var len = v.Length;
                if (len < 0.001) { next.Add(q); continue; }
                var off = (R() - 0.5) * len * rough;
                next.Add(new Point((p.X + q.X) / 2 - v.Y / len * off, (p.Y + q.Y) / 2 + v.X / len * off));
                next.Add(q);
            }
            pts = next;
        }
        return pts;
    }

    private static void AddPolyline(StreamGeometryContext ctx, List<Point> pts)
    {
        ctx.BeginFigure(pts[0], false, false);
        ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
    }

    private void MakeStrike(double t, Size size, double? x = null)
    {
        var sx = x ?? size.Width * (0.09 + R() * 0.82);
        var impact = new Point(sx + (R() - 0.5) * 420, size.Height * (0.49 + R() * 0.49));
        var main = ZigZag(new Point(sx, -30), impact, 7, 0.55);

        var mainGeo = new StreamGeometry();
        using (var ctx = mainGeo.Open()) AddPolyline(ctx, main);
        mainGeo.Freeze();

        // 2–5 Verästelungen, schräg vom Hauptkanal weg
        var branchGeo = new StreamGeometry();
        using (var ctx = branchGeo.Open())
        {
            var baseAngle = Math.Atan2(impact.Y + 30, impact.X - sx);
            var count = 2 + Rng.Next(4);
            for (var b = 0; b < count; b++)
            {
                var start = main[8 + Rng.Next((int)(main.Count * 0.6))];
                var ang = baseAngle + (R() < 0.5 ? -1 : 1) * (0.5 + R() * 0.6);
                var len = 90 + R() * 220;
                AddPolyline(ctx, ZigZag(start, new Point(start.X + Math.Cos(ang) * len, start.Y + Math.Sin(ang) * len), 5, 0.6));
            }
        }
        branchGeo.Freeze();

        _strikes.Add(new Strike(mainGeo, branchGeo, impact, t, R() < 0.6));
        _sheets.Add(new Sheet(new Point(sx, 0), 700, t, 0.22));
        for (var k = 0; k < 18; k++)
        {
            var a = -Math.PI * R();
            var sp = 80 + R() * 260;
            _sparks.Add(new Spark { X = impact.X, Y = impact.Y, Vx = Math.Cos(a) * sp, Vy = Math.Sin(a) * sp, T0 = t, Life = 0.3 + R() * 0.5, S = 1 + R() * 2 });
        }
    }

    /// <summary>Helligkeit eines Einschlags über die Zeit (Blitz – dunkel – Nachblitz – Verglimmen).</summary>
    private static double Intensity(double a, bool restrike)
    {
        if (a < 0.05) return 1;
        if (a < 0.11) return 1 - (a - 0.05) / 0.06 * 0.75;
        if (restrike && a < 0.16) return 0.95;
        return Math.Max(0, (restrike ? 0.95 : 0.25) * Math.Exp(-(a - (restrike ? 0.16 : 0.11)) * 5.5));
    }

    public override void Update(double t, double dt, Size size)
    {
        if (t > _next)
        {
            MakeStrike(t, size);
            // Manchmal schlägt kurz danach ein zweiter Blitz daneben ein
            if (R() < 0.3) _pending.Add((t + 0.14, size.Width * (0.09 + R() * 0.82)));
            _next = t + 1.1 + R() * 2.2;
        }
        for (var i = _pending.Count - 1; i >= 0; i--)
            if (t >= _pending[i].Time)
            {
                MakeStrike(t, size, _pending[i].X);
                _pending.RemoveAt(i);
            }

        // Wetterleuchten in den „Wolken“ (~1,2 pro Sekunde)
        if (R() < 1.2 * dt)
            _sheets.Add(new Sheet(new Point(R() * size.Width, R() * size.Height * 0.3), 260 + R() * 340, t, 0.08 + R() * 0.07));

        _strikes.RemoveAll(s => t - s.T0 > 1.2);
        _sheets.RemoveAll(s => t - s.T0 > 0.6);
        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            if (t - s.T0 >= s.Life) { _sparks.RemoveAt(i); continue; }
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt;
            s.Vy += 500 * dt;
        }
    }

    public override void Draw(DrawingContext dc, double t, Size size)
    {
        foreach (var sh in _sheets)
        {
            var a = (t - sh.T0) / 0.6;
            var k = sh.Alpha * (a < 0.1 ? a / 0.1 : 1 - (a - 0.1) / 0.9) * (0.75 + 0.25 * Math.Sin(t * 60));
            dc.DrawEllipse(Fx.Glow((0, Fx.A(_soft, k)), (1, Fx.A(_acc, 0))), null, sh.Center, sh.Radius, sh.Radius);
        }

        foreach (var s in _strikes)
        {
            var k = Intensity(t - s.T0, s.Restrike);
            if (k <= 0.01) continue;
            // Von außen nach innen: breiter farbiger Schein → heller Kanal → weißer Kern
            foreach (var (w, c, al) in new[] { (26.0, _acc, 0.10), (12.0, _glow, 0.28), (5.0, _soft, 0.85), (2.0, Fx.White, 1.0) })
            {
                var brush = Fx.Solid(c, al * k);
                dc.DrawGeometry(null, Fx.RoundPen(brush, w), s.Main);
                dc.DrawGeometry(null, Fx.RoundPen(brush, w * 0.5), s.Branches);
            }
            dc.DrawEllipse(Fx.Glow((0, Fx.A(Fx.White, 0.8 * k)), (0.3, Fx.A(_glow, 0.4 * k)), (1, Fx.A(_acc, 0))), null, s.Impact, 90, 90);
        }

        foreach (var s in _sparks)
        {
            var a = (t - s.T0) / s.Life;
            dc.DrawEllipse(Fx.Solid(Fx.Mix(Fx.White, _acc, a), 1 - a), null, new Point(s.X, s.Y), s.S, s.S);
        }
    }
}

// ============================================================================================
//  Ki-Aura: pulsierende Energie, aufsteigende Funken, Anime-Speedlines
// ============================================================================================

/// <summary>
/// Goldene Aura, die von unten pulsiert, ein kühler Schein oben rechts, flackernd aufsteigende
/// Ki-Funken und alle paar Sekunden ein Bündel Speedlines (Energieschub).
/// </summary>
public sealed class KiAura : SkyEffect
{
    private sealed class Particle { public double X, Y, V, S, W; public bool Main; }

    private readonly Color _gold, _orange, _blue;
    private readonly List<Particle> _parts = new();
    private double _burstT = double.NegativeInfinity;
    private Point _burstAt;

    public KiAura(Color gold, Color orange, Color blue) => (_gold, _orange, _blue) = (gold, orange, blue);

    public override void Reset(double t, Size size)
    {
        _parts.Clear();
        for (var i = 0; i < 90; i++)
            _parts.Add(new Particle { X = R() * size.Width, Y = R() * size.Height * 1.2, V = 40 + R() * 120, S = 1 + R() * 3, W = R() * 6, Main = R() < 0.75 });
        _burstT = t - 1.5;
    }

    public override void Update(double t, double dt, Size size)
    {
        foreach (var p in _parts)
        {
            p.Y -= p.V * dt;
            p.X += Math.Sin(t * 3 + p.W) * 36 * dt;
            if (p.Y < -20)
            {
                p.Y = size.Height + 20;
                p.X = R() * size.Width;
            }
        }
        if (t - _burstT > 2.6)
        {
            _burstT = t;
            _burstAt = new Point(size.Width * (0.23 + R() * 0.68), size.Height * (0.23 + R() * 0.58));
        }
    }

    public override void Draw(DrawingContext dc, double t, Size size)
    {
        var scale = Math.Max(size.Width / 1320, size.Height / 860);

        // Pulsierende Aura von unten und kühler Schein oben rechts
        var pulse = 0.55 + 0.45 * Math.Sin(t * 2.2);
        var r = 760 * scale;
        dc.DrawEllipse(Fx.Glow((0, Fx.A(_gold, 0.22 * pulse)), (0.45, Fx.A(_orange, 0.10 * pulse)), (1, Fx.A(_orange, 0))), null,
            new Point(size.Width * 0.576, size.Height + 120 * scale), r, r);
        dc.DrawEllipse(Fx.Glow((0, Fx.A(_blue, 0.16)), (1, Fx.A(_blue, 0))), null,
            new Point(size.Width * 0.95, -80 * scale), 520 * scale, 520 * scale);

        // Aufsteigende Ki-Funken (längliche, flackernde Flammenzungen)
        foreach (var p in _parts)
        {
            var a = 0.35 + 0.35 * Math.Sin(t * 6 + p.W);
            var c = p.Main ? _gold : _orange;
            var at = new Point(p.X, p.Y);
            dc.DrawEllipse(Fx.Solid(_gold, a * 0.18), null, at, p.S * 2, p.S * 4.5);
            dc.DrawEllipse(Fx.Solid(c, a), null, at, p.S * 0.7, p.S * 2.4);
        }

        // Speedlines nach einem Energieschub
        var ba = 1 - (t - _burstT) / 0.45;
        if (ba > 0)
            for (var k = 0; k < 48; k++)
            {
                var ang = k / 48.0 * Math.PI * 2 + _burstT;
                var r1 = 140 + k % 5 * 30;
                var r2 = r1 + 260 + k % 7 * 40;
                var pen = new Pen(Fx.Solid(k % 3 != 0 ? _gold : Fx.White, 0.35 * ba), 1 + k % 4);
                pen.Freeze();
                dc.DrawLine(pen, new Point(_burstAt.X + Math.Cos(ang) * r1, _burstAt.Y + Math.Sin(ang) * r1),
                                 new Point(_burstAt.X + Math.Cos(ang) * r2, _burstAt.Y + Math.Sin(ang) * r2));
            }
    }
}

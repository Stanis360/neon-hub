namespace NeonMonitor.Services.Fps;

/// <summary>
/// Ruckler-Erkennung und Frametime-Verteilung – gemeinsam genutzt von der Live-Anzeige und der Aufnahme.
/// </summary>
public static class FrameAnalysis
{
    /// <summary>Anzahl vorheriger Bilder für den gleitenden Median.</summary>
    public const int MedianWindow = 30;

    /// <summary>
    /// Ruckler = ein Bild dauert mindestens doppelt so lange wie üblich (gleitender Median)
    /// <b>und</b> mindestens 8 ms länger. Die zweite Bedingung verhindert, dass bei sehr hohen FPS
    /// winzige Schwankungen (z. B. 3 → 7 ms) als Ruckler zählen.
    /// </summary>
    public static bool IsStutter(double frameMs, double medianMs) =>
        medianMs > 0 && frameMs >= 2 * medianMs && frameMs - medianMs >= 8;

    /// <summary>Zählt Ruckler in einer Folge von Frametimes (in Aufnahmereihenfolge).</summary>
    public static int CountStutters(IReadOnlyList<float> frames)
    {
        var window = new Queue<float>(MedianWindow);
        var count = 0;
        foreach (var f in frames)
        {
            if (window.Count >= 10 && IsStutter(f, Median(window))) count++;
            window.Enqueue(f);
            if (window.Count > MedianWindow) window.Dequeue();
        }
        return count;
    }

    public static double Median(IEnumerable<float> values)
    {
        var a = values.ToArray();
        if (a.Length == 0) return 0;
        Array.Sort(a);
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0;
    }

    /// <summary>Obergrenzen der Histogramm-Klassen in ms (letzte Klasse = alles darüber).</summary>
    public static readonly double[] BinEdges = [4.17, 5.56, 6.94, 8.33, 11.1, 16.7, 25, 33.3, 50];

    /// <summary>Beschriftungen: Frametime und die entsprechenden FPS.</summary>
    public static readonly string[] BinLabels =
        ["<4 ms\n>240", "4–5,6\n180–240", "5,6–7\n144–180", "7–8,3\n120–144", "8,3–11\n90–120",
         "11–17\n60–90", "17–25\n40–60", "25–33\n30–40", "33–50\n20–30", ">50 ms\n<20"];

    /// <summary>Anteil der Bilder je Klasse (Summe = 1).</summary>
    public static double[] Histogram(IEnumerable<double> frameTimes)
    {
        var bins = new double[BinEdges.Length + 1];
        var n = 0;
        foreach (var ft in frameTimes)
        {
            var i = 0;
            while (i < BinEdges.Length && ft >= BinEdges[i]) i++;
            bins[i]++;
            n++;
        }
        if (n > 0) for (var i = 0; i < bins.Length; i++) bins[i] /= n;
        return bins;
    }
}

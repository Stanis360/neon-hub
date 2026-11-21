using System.Globalization;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Fps;

/// <summary>
/// Liest die PresentMon-CSV. Die Spalten werden über die Kopfzeile zugeordnet,
/// damit Formatänderungen in neuen PresentMon-Versionen nichts kaputt machen.
/// <para>
/// Unterstützt das Standardformat von PresentMon 2.x (Zeit in <c>TimeInMs</c>, Latenzspalten
/// <c>MsAllInputToPhotonLatency</c>/<c>MsClickToPhotonLatency</c>) sowie die älteren Formate.
/// „NA“ oder 0 bei den Latenzen bedeutet: In diesem Bild war keine Eingabe → kein Wert.
/// </para>
/// </summary>
public sealed class PresentMonCsvParser
{
    private int _app = -1, _pid = -1, _time = -1, _frameTime = -1, _input = -1, _click = -1, _untilDisplayed = -1;
    private double _timeScale = 1; // Sekunden pro Einheit der Zeitspalte

    public bool TryParse(string line, out FrameSample frame)
    {
        frame = default;
        if (line.Length == 0) return false;
        var cols = line.Split(',');

        if (cols[0].Equals("Application", StringComparison.OrdinalIgnoreCase))
        {
            ReadHeader(cols);
            return false;
        }
        if (_app < 0 || _frameTime < 0 || cols.Length <= Math.Max(_frameTime, Math.Max(_pid, _time))) return false;

        if (!int.TryParse(cols[_pid], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) return false;
        if (!double.TryParse(cols[_frameTime], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)) return false;
        double.TryParse(cols[_time], NumberStyles.Float, CultureInfo.InvariantCulture, out var t);

        frame = new FrameSample(pid, cols[_app], t * _timeScale, ms,
            InputLatencyMs: Optional(cols, _input),
            ClickLatencyMs: Optional(cols, _click),
            DisplayDelayMs: Optional(cols, _untilDisplayed));
        return true;
    }

    /// <summary>Positiver Zahlenwert oder null (fehlende Spalte, „NA“, 0).</summary>
    private static double? Optional(string[] cols, int index) =>
        index >= 0 && index < cols.Length
        && double.TryParse(cols[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v : null;

    private void ReadHeader(string[] cols)
    {
        int Find(params string[] names)
        {
            foreach (var n in names)
            {
                var i = Array.FindIndex(cols, c => c.Trim().Equals(n, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            return -1;
        }

        _app = Find("Application");
        _pid = Find("ProcessID");
        _frameTime = Find("MsBetweenPresents", "FrameTime", "MsBetweenAppStart");
        _input = Find("MsAllInputToPhotonLatency", "AllInputToPhotonLatency");
        _click = Find("MsClickToPhotonLatency", "ClickToPhotonLatency");
        _untilDisplayed = Find("MsUntilDisplayed");

        // Zeitspalte: Sekunden oder Millisekunden, je nach Format
        _time = Find("TimeInSeconds", "CPUStartTimeInSeconds");
        _timeScale = 1;
        if (_time < 0)
        {
            _time = Find("TimeInMs", "CPUStartTime", "CPUStartTimeInMs");
            _timeScale = 0.001;
        }
        Log.Info($"PresentMon-Spalten: App={_app}, PID={_pid}, Zeit={_time}, Frametime={_frameTime}, " +
                 $"Eingabe→Bild={_input}, Klick→Bild={_click}, bis Anzeige={_untilDisplayed}");
    }
}

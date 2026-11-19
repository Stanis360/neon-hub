using System.Globalization;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Fps;

/// <summary>
/// Liest die PresentMon-CSV. Die Spalten werden über die Kopfzeile zugeordnet,
/// damit kleinere Formatänderungen in neuen PresentMon-Versionen nichts kaputt machen.
/// </summary>
public sealed class PresentMonCsvParser
{
    private int _app = -1, _pid = -1, _time = -1, _frameTime = -1;
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

        frame = new FrameSample(pid, cols[_app], t * _timeScale, ms);
        return true;
    }

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
        _frameTime = Find("msBetweenPresents", "FrameTime", "MsBetweenAppStart");
        _time = Find("TimeInSeconds", "CPUStartTimeInSeconds");
        _timeScale = 1;
        if (_time < 0)
        {
            _time = Find("CPUStartTime", "CPUStartTimeInMs");
            _timeScale = 0.001; // Spalte in Millisekunden
        }
        Log.Info($"PresentMon-Spalten: App={_app}, PID={_pid}, Zeit={_time}, Frametime={_frameTime}");
    }
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Recording;

/// <summary>Spaltenweise geladene Messwerte einer Sitzung (für die Vergleichsdiagramme).</summary>
public sealed class SessionSamples
{
    /// <summary>Sekunden seit Aufnahmestart je Zeile.</summary>
    public double[] Seconds { get; init; } = [];
    /// <summary>Spaltenname → Werte (NaN = leer).</summary>
    public Dictionary<string, double[]> Columns { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public double[]? Get(string column) => Columns.TryGetValue(column, out var v) ? v : null;
}

/// <summary>
/// Ablage der Sitzungen in <c>Dokumente\NeonMonitor\Sitzungen</c>:
/// <c>Spiel_Datum_Uhrzeit.csv</c> (Sensoren), <c>…-frames.csv</c> (Frametimes), <c>….json</c> (Zusammenfassung).
/// <para>
/// CSV-Format für deutsches Excel: Trennzeichen Semikolon, Dezimalkomma, erste Zeile <c>sep=;</c>.
/// </para>
/// </summary>
public static class SessionStore
{
    public const char Separator = ';';
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("de-DE");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Directory => AppPaths.SessionsDir;

    /// <summary>Basis-Dateiname, z. B. „Satisfactory_2026-11-20_19-45-03“.</summary>
    public static string BaseName(string game, DateTime start) =>
        $"{GameNames.ToFileName(game)}_{start:yyyy-MM-dd_HH-mm-ss}";

    public static string Format(double? v, string format = "0.##") =>
        v is { } d && double.IsFinite(d) ? d.ToString(format, Culture) : "";

    public static void SaveSummary(SessionSummary s)
    {
        File.WriteAllText(s.JsonPath, JsonSerializer.Serialize(s, JsonOptions), Encoding.UTF8);
    }

    /// <summary>Alle Sitzungen, neueste zuerst.</summary>
    public static List<SessionSummary> List()
    {
        var result = new List<SessionSummary>();
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            try
            {
                var s = JsonSerializer.Deserialize<SessionSummary>(File.ReadAllText(file));
                if (s is null) continue;
                s.JsonPath = file;
                result.Add(s);
            }
            catch (Exception ex)
            {
                Log.Warn($"Sitzung '{file}' unlesbar: {ex.Message}");
            }
        }
        return result.OrderByDescending(s => s.Start).ToList();
    }

    /// <summary>Lädt die Sensor-CSV einer Sitzung spaltenweise.</summary>
    public static SessionSamples LoadSamples(SessionSummary s)
    {
        var path = Path.Combine(Directory, s.CsvFile);
        var lines = File.ReadAllLines(path, Encoding.UTF8)
                        .Where(l => l.Length > 0 && !l.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
                        .ToList();
        if (lines.Count < 2) return new SessionSamples();

        var header = lines[0].Split(Separator);
        var rows = lines.Skip(1).Select(l => l.Split(Separator)).ToList();

        var columns = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        for (var c = 0; c < header.Length; c++)
        {
            var values = new double[rows.Count];
            var any = false;
            for (var r = 0; r < rows.Count; r++)
            {
                values[r] = c < rows[r].Length && double.TryParse(rows[r][c], NumberStyles.Float, Culture, out var v) ? v : double.NaN;
                any |= !double.IsNaN(values[r]);
            }
            if (any) columns[header[c]] = values;
        }
        return new SessionSamples
        {
            Seconds = columns.TryGetValue(RecordingColumns.Seconds, out var sec) ? sec : [],
            Columns = columns,
        };
    }

    /// <summary>Spielnamen ändern: Dateien umbenennen und Zusammenfassung anpassen.</summary>
    public static void Rename(SessionSummary s, string newGame)
    {
        newGame = newGame.Trim();
        if (newGame.Length == 0 || newGame == s.Game) return;

        var baseName = BaseName(newGame, s.Start);
        var moves = new List<(string From, string To)>
        {
            (s.CsvFile, baseName + ".csv"),
            (s.FramesFile, baseName + "-frames.csv"),
            (Path.GetFileName(s.JsonPath), baseName + ".json"),
        }.Where(m => !string.IsNullOrEmpty(m.From) && !m.From.Equals(m.To, StringComparison.OrdinalIgnoreCase)).ToList();

        if (moves.Any(m => File.Exists(Path.Combine(Directory, m.To))))
            throw new IOException($"Es gibt bereits eine Sitzung „{newGame}“ mit derselben Startzeit.");

        foreach (var (from, to) in moves)
            if (File.Exists(Path.Combine(Directory, from))) File.Move(Path.Combine(Directory, from), Path.Combine(Directory, to));

        s.CsvFile = baseName + ".csv";
        if (!string.IsNullOrEmpty(s.FramesFile)) s.FramesFile = baseName + "-frames.csv";
        s.JsonPath = Path.Combine(Directory, baseName + ".json");
        s.Game = newGame;
        SaveSummary(s);
    }

    public static void Delete(SessionSummary s)
    {
        foreach (var f in new[] { s.CsvFile, s.FramesFile })
            if (!string.IsNullOrEmpty(f)) File.Delete(Path.Combine(Directory, f));
        File.Delete(s.JsonPath);
    }
}

/// <summary>Spaltennamen der Sensor-CSV (gleichzeitig Schlüssel beim Einlesen).</summary>
public static class RecordingColumns
{
    public const string Time = "Uhrzeit";
    public const string Seconds = "Sekunden";
    public const string Application = "Anwendung";
    public const string Fps = "FPS";
    public const string Low1 = "1% Low";
    public const string FrameTime = "Frametime ms";
    public const string FrameTimeMax = "Frametime max ms";
    public const string CpuLoad = "CPU Last %";
    public const string CpuTemp = "CPU Temp °C";
    public const string CpuPower = "CPU Leistung W";
    public const string CpuClock = "CPU Takt MHz";
    public const string GpuLoad = "GPU Last %";
    public const string GpuTemp = "GPU Temp °C";
    public const string GpuHotspot = "GPU Hotspot °C";
    public const string GpuMemTemp = "GPU VRAM-Temp °C";
    public const string GpuClock = "GPU Takt MHz";
    public const string GpuMemClock = "GPU Speichertakt MHz";
    public const string GpuVram = "VRAM MB";
    public const string GpuPower = "GPU Leistung W";
    public const string GpuFan = "GPU Lüfter U/min";
    public const string RamUsed = "RAM GB";
    public const string RamLoad = "RAM %";
    public const string TotalPower = "CPU+GPU Leistung W";
    public const string CpuFan = "CPU-Lüfter U/min";
    public const string CaseFan = "Gehäuselüfter U/min";
    public const string NetDown = "Netz Empfang Mbit/s";
    public const string NetUp = "Netz Senden Mbit/s";
    public const string DiskRead = "Datenträger Lesen MB/s";
    public const string DiskWrite = "Datenträger Schreiben MB/s";

    public static readonly string[] All =
    [
        Time, Seconds, Application, Fps, Low1, FrameTime, FrameTimeMax,
        CpuLoad, CpuTemp, CpuPower, CpuClock,
        GpuLoad, GpuTemp, GpuHotspot, GpuClock, GpuMemClock, GpuVram, GpuPower, GpuFan,
        RamUsed, RamLoad, TotalPower, CpuFan, CaseFan, NetDown, NetUp, DiskRead, DiskWrite, GpuMemTemp,
    ];
}

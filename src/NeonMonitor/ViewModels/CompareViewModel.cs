using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NeonMonitor.Controls;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.ViewModels;

/// <summary>Eine Sitzung in der Liste der Vergleichsseite.</summary>
public sealed class SessionItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _game;
    private Brush _colorBrush = Brushes.Transparent;
    private readonly CompareViewModel _owner;

    public SessionItemViewModel(SessionSummary summary, CompareViewModel owner)
    {
        Summary = summary;
        _owner = owner;
        _game = summary.Game;
        DeleteCommand = new RelayCommand(() => owner.Delete(this));
        OpenCommand = new RelayCommand(() => Shell.Open(Path.Combine(SessionStore.Directory, summary.CsvFile)));
    }

    public SessionSummary Summary { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCommand { get; }

    public string DateText => Summary.Start.ToString("ddd dd.MM.yyyy  HH:mm");
    public string DurationText => FormatDuration(Summary.DurationSeconds);
    public string FpsText => (Summary.AvgFps is { } f ? $"{f:0} FPS" + (Summary.Low1 is { } l ? $" · 1% {l:0}" : "") : "keine FPS")
                             + (Summary.CostEuro is { } c ? $" · {c:0.00} €" : "");

    /// <summary>Spielname – Änderung benennt die Dateien um.</summary>
    public string Game
    {
        get => _game;
        set
        {
            if (!SetProperty(ref _game, value)) return;
            try
            {
                SessionStore.Rename(Summary, value);
                _owner.Recalculate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Umbenennen", MessageBoxButton.OK, MessageBoxImage.Warning);
                _game = Summary.Game;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value)) _owner.OnSelectionChanged(this);
        }
    }

    /// <summary>Farbe der Sitzung in Tabelle und Diagramm (transparent = nicht ausgewählt).</summary>
    public Brush ColorBrush { get => _colorBrush; set => SetProperty(ref _colorBrush, value); }

    public static string FormatDuration(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
    }
}

/// <summary>Spaltenkopf der Vergleichstabelle.</summary>
public sealed record CompareColumn(string Title, string Subtitle, Brush Color);

/// <summary>Eine Zelle: Text, Balkenlänge (0–1), ob es der beste Wert der Zeile ist.</summary>
public sealed record CompareCell(string Text, double Fraction, bool IsBest);

/// <summary>Eine Zeile der Vergleichstabelle, z. B. „Ø FPS“.</summary>
public sealed record CompareRow(string Label, IReadOnlyList<CompareCell> Cells);

/// <summary>Auswahlknopf für die Größe im Vergleichsdiagramm.</summary>
public sealed class ChartMetricChoice(string title, string column, string unit, ICommand select) : ObservableObject
{
    private bool _isSelected;
    public string Title { get; } = title;
    public string Column { get; } = column;
    public string Unit { get; } = unit;
    public ICommand SelectCommand { get; } = select;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

/// <summary>
/// Reiter „Vergleich“: Sitzungsliste, Auswahl (bis zu 6), Vergleichstabelle und Verlaufsdiagramm.
/// </summary>
public sealed class CompareViewModel : ObservableObject
{
    /// <summary>Farben der ausgewählten Sitzungen (in Auswahlreihenfolge).</summary>
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x3D, 0xDC, 0x97), Color.FromRgb(0x5A, 0xA9, 0xE6), Color.FromRgb(0xE8, 0xB4, 0x4C),
        Color.FromRgb(0xC7, 0x7D, 0xFF), Color.FromRgb(0xE5, 0x62, 0x6F), Color.FromRgb(0xC5, 0xD0, 0xCD),
    ];
    private const int MaxSelected = 6;
    private const int ChartPoints = 600; // Punkte pro Linie (gemittelt), hält das Diagramm flott

    private readonly List<SessionItemViewModel> _selected = new();
    private readonly Dictionary<string, SessionSamples> _cache = new();
    private IReadOnlyList<ChartLine> _chartLines = [];
    private ChartMetricChoice _metric;

    public CompareViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        OpenFolderCommand = new RelayCommand(() => Shell.Open(SessionStore.Directory));

        var select = new ParamCommand(col => SelectMetric(col));
        Metrics =
        [
            new("FPS", RecordingColumns.Fps, "", select),
            new("Frametime", RecordingColumns.FrameTimeMax, "ms", select),
            new("CPU Temp", RecordingColumns.CpuTemp, "°", select),
            new("GPU Temp", RecordingColumns.GpuTemp, "°", select),
            new("Hotspot", RecordingColumns.GpuHotspot, "°", select),
            new("CPU Last", RecordingColumns.CpuLoad, "%", select),
            new("GPU Last", RecordingColumns.GpuLoad, "%", select),
            new("CPU Watt", RecordingColumns.CpuPower, "W", select),
            new("GPU Watt", RecordingColumns.GpuPower, "W", select),
            new("CPU-Lüfter", RecordingColumns.CpuFan, "", select),
            new("Gehäuselüfter", RecordingColumns.CaseFan, "", select),
        ];
        _metric = Metrics[0];
        _metric.IsSelected = true;
        Refresh();
    }

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = new();
    public ObservableCollection<CompareColumn> Columns { get; } = new();
    public ObservableCollection<CompareRow> Rows { get; } = new();
    public IReadOnlyList<ChartMetricChoice> Metrics { get; }

    public ICommand RefreshCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public IReadOnlyList<ChartLine> ChartLines { get => _chartLines; private set => SetProperty(ref _chartLines, value); }
    public string ChartUnit => _metric.Unit;
    public string ChartTitle => _metric.Column == RecordingColumns.FrameTimeMax
        ? "Längste Frametime je Messung (Ruckler)"
        : $"Verlauf: {_metric.Title}";

    public bool HasSelection => _selected.Count > 0;
    public bool HasSessions => Sessions.Count > 0;
    public string SelectionHint => _selected.Count switch
    {
        0 => "Sitzungen links anhaken, um sie zu vergleichen (bis zu 6).",
        1 => "Noch eine Sitzung anhaken für einen Vergleich.",
        _ => $"{_selected.Count} Sitzungen im Vergleich – grün hervorgehoben ist jeweils der beste Wert.",
    };

    /// <summary>Liste neu einlesen (Auswahl bleibt erhalten, soweit die Sitzungen noch existieren).</summary>
    public void Refresh()
    {
        var keep = _selected.Select(s => s.Summary.Start).ToHashSet();
        _selected.Clear();
        Sessions.Clear();
        foreach (var s in SessionStore.List())
        {
            var item = new SessionItemViewModel(s, this);
            Sessions.Add(item);
            if (keep.Contains(s.Start)) item.IsSelected = true;
        }
        OnPropertyChanged(nameof(HasSessions));
        Recalculate();
    }

    public void Delete(SessionItemViewModel item)
    {
        var answer = MessageBox.Show($"Sitzung „{item.Game}“ vom {item.DateText} endgültig löschen?",
            "Sitzung löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            SessionStore.Delete(item.Summary);
        }
        catch (Exception ex)
        {
            Log.Warn($"Löschen fehlgeschlagen: {ex.Message}");
        }
        _selected.Remove(item);
        Sessions.Remove(item);
        OnPropertyChanged(nameof(HasSessions));
        Recalculate();
    }

    public void OnSelectionChanged(SessionItemViewModel item)
    {
        if (item.IsSelected)
        {
            if (_selected.Count >= MaxSelected)
            {
                // Haken nachträglich zurücknehmen (direkt im Setter würde WPF die Änderung ignorieren)
                Application.Current.Dispatcher.BeginInvoke(() => item.IsSelected = false);
                return;
            }
            if (!_selected.Contains(item)) _selected.Add(item);
        }
        else
        {
            _selected.Remove(item);
        }
        Recalculate();
    }

    private void SelectMetric(string? column)
    {
        _metric = Metrics.First(m => m.Column == column);
        foreach (var m in Metrics) m.IsSelected = m == _metric;
        OnPropertyChanged(nameof(ChartUnit));
        OnPropertyChanged(nameof(ChartTitle));
        UpdateChart();
    }

    /// <summary>Farben, Tabelle und Diagramm neu aufbauen.</summary>
    public void Recalculate()
    {
        foreach (var s in Sessions) s.ColorBrush = Brushes.Transparent;
        for (var i = 0; i < _selected.Count; i++) _selected[i].ColorBrush = Freeze(new SolidColorBrush(Palette[i]));

        Columns.Clear();
        foreach (var s in _selected)
            Columns.Add(new CompareColumn(s.Game, s.Summary.Start.ToString("dd.MM.  HH:mm"), s.ColorBrush));

        BuildRows();
        UpdateChart();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionHint));
    }

    private void BuildRows()
    {
        Rows.Clear();
        if (_selected.Count == 0) return;
        var list = _selected.Select(s => s.Summary).ToList();

        // betterHigh: true = höher ist besser, false = niedriger ist besser, null = neutral
        void Add(string label, Func<SessionSummary, double?> value, string format, string unit, bool? betterHigh)
        {
            var values = list.Select(value).ToList();
            var max = values.Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty(0).Max();
            double? best = betterHigh switch
            {
                true => values.Max(),
                false => values.Where(v => v is not null).Min(),
                null => null,
            };
            var showBest = list.Count > 1 && values.Count(v => v is not null) > 1;
            var cells = values.Select(v => new CompareCell(
                v is { } d ? $"{d.ToString(format)} {unit}".Trim() : "—",
                v is { } x && max > 0 ? x / max : 0,
                showBest && best is not null && v is { } y && Math.Abs(y - best.Value) < 1e-9)).ToList();
            Rows.Add(new CompareRow(label, cells));
        }

        Add("Dauer", s => s.DurationSeconds / 60, "0.0", "min", null);
        Add("Ø FPS", s => s.AvgFps, "0.0", "", true);
        Add("1 % Low", s => s.Low1, "0.0", "", true);
        Add("0,1 % Low", s => s.Low01, "0.0", "", true);
        Add("Ø Frametime", s => s.AvgFrameTimeMs, "0.00", "ms", false);
        Add("Max. CPU-Temp", s => s.MaxCpuTemp, "0", "°C", false);
        Add("Max. GPU-Temp", s => s.MaxGpuTemp, "0", "°C", false);
        Add("Max. Hotspot", s => s.MaxGpuHotspot, "0", "°C", false);
        Add("Ø CPU-Last", s => s.AvgCpuLoad, "0", "%", null);
        Add("Ø GPU-Last", s => s.AvgGpuLoad, "0", "%", null);
        Add("Ø CPU-Leistung", s => s.AvgCpuPower, "0", "W", false);
        Add("Ø GPU-Leistung", s => s.AvgGpuPower, "0", "W", false);
        Add("Max. RAM", s => s.MaxRamLoad, "0", "%", null);
        Add("Ø CPU-Lüfter", s => s.AvgCpuFan, "0", "U/min", false);
        Add("Ø Gehäuselüfter", s => s.AvgCaseFan, "0", "U/min", false);
        Add("FPS pro Watt", s => s.FpsPerWatt, "0.00", "", true);
        Add("Energie CPU+GPU", s => s.EnergyWh, "0", "Wh", false);
        Add("Stromkosten", s => s.CostEuro, "0.00", "€", false);
    }

    private void UpdateChart()
    {
        var lines = new List<ChartLine>();
        for (var i = 0; i < _selected.Count; i++)
        {
            var samples = Load(_selected[i].Summary);
            var y = samples?.Get(_metric.Column);
            if (samples is null || y is null || samples.Seconds.Length == 0) continue;
            var (bx, by) = Downsample(samples.Seconds, y, ChartPoints, useMax: _metric.Column == RecordingColumns.FrameTimeMax);
            lines.Add(new ChartLine(bx, by, Palette[i]));
        }
        ChartLines = lines;
    }

    private SessionSamples? Load(SessionSummary s)
    {
        if (_cache.TryGetValue(s.CsvFile, out var cached)) return cached;
        try
        {
            var samples = SessionStore.LoadSamples(s);
            _cache[s.CsvFile] = samples;
            return samples;
        }
        catch (Exception ex)
        {
            Log.Warn($"CSV '{s.CsvFile}' konnte nicht gelesen werden: {ex.Message}");
            return null;
        }
    }

    /// <summary>Auf höchstens <paramref name="points"/> Punkte reduzieren (Mittelwert bzw. Maximum je Abschnitt).</summary>
    private static (double[] X, double[] Y) Downsample(double[] x, double[] y, int points, bool useMax)
    {
        var n = Math.Min(x.Length, y.Length);
        if (n <= points) return (x[..n], y[..n]);

        var bx = new double[points];
        var by = new double[points];
        for (var b = 0; b < points; b++)
        {
            int from = b * n / points, to = (b + 1) * n / points;
            double sum = 0, max = double.NaN;
            var count = 0;
            for (var i = from; i < to; i++)
            {
                if (double.IsNaN(y[i])) continue;
                sum += y[i];
                count++;
                if (double.IsNaN(max) || y[i] > max) max = y[i];
            }
            bx[b] = x[(from + to - 1) / 2];
            by[b] = count == 0 ? double.NaN : useMax ? max : sum / count;
        }
        return (bx, by);
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}

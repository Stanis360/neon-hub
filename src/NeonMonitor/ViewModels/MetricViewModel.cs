using System.Globalization;

namespace NeonMonitor.ViewModels;

/// <summary>Ampelstufe eines Messwerts (steuert die Farbe der Kachel).</summary>
public enum Severity { Normal, Warning, Critical }

/// <summary>
/// Ein einzelner Messwert für die Oberfläche: aktueller Wert, Höchstwert seit Start,
/// Verlauf für Diagramme und Warnstufe.
/// </summary>
public sealed class MetricViewModel : ObservableObject
{
    private readonly string _format;
    private readonly double? _warnAt;
    private readonly double? _criticalAt;
    private double? _value;
    private double? _peak;
    private Severity _severity;

    /// <param name="label">Beschriftung, z. B. „TEMPERATUR“.</param>
    /// <param name="unit">Einheit, z. B. „°C“.</param>
    /// <param name="format">.NET-Zahlenformat für die Anzeige.</param>
    /// <param name="historyCapacity">Anzahl Punkte im Verlauf.</param>
    /// <param name="warnAt">Ab diesem Wert gelb (optional).</param>
    /// <param name="criticalAt">Ab diesem Wert rot (optional).</param>
    public MetricViewModel(string label, string unit, string format, int historyCapacity,
                           double? warnAt = null, double? criticalAt = null)
    {
        Label = label;
        Unit = unit;
        _format = format;
        _warnAt = warnAt;
        _criticalAt = criticalAt;
        History = new ChartSeries(historyCapacity);
    }

    public string Label { get; }
    public string Unit { get; }
    public ChartSeries History { get; }

    public double? Value
    {
        get => _value;
        private set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(IsAvailable));
            }
        }
    }

    public bool IsAvailable => _value.HasValue;

    /// <summary>Formatierter Wert oder „—“, wenn der Sensor fehlt.</summary>
    public string DisplayValue => _value is { } v ? v.ToString(_format, CultureInfo.CurrentCulture) : "—";

    /// <summary>Höchstwert seit Programmstart (bzw. seit <see cref="ResetPeak"/>).</summary>
    public string PeakText => _peak is { } p ? $"max {p.ToString(_format, CultureInfo.CurrentCulture)}" : "";

    public Severity Severity
    {
        get => _severity;
        private set => SetProperty(ref _severity, value);
    }

    /// <summary>Neuen Messwert übernehmen (null = Sensor nicht verfügbar).</summary>
    public void Update(double? value)
    {
        Value = value;
        History.Add(value ?? double.NaN);

        if (value is { } v)
        {
            if (_peak is null || v > _peak)
            {
                _peak = v;
                OnPropertyChanged(nameof(PeakText));
            }
            Severity = v >= _criticalAt ? Severity.Critical
                     : v >= _warnAt ? Severity.Warning
                     : Severity.Normal;
        }
        else
        {
            Severity = Severity.Normal;
        }
    }

    public void ResetPeak()
    {
        _peak = null;
        OnPropertyChanged(nameof(PeakText));
    }
}

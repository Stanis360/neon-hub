namespace NeonMonitor.ViewModels;

/// <summary>Takt eines einzelnen CPU-Kerns für die Kernliste (Balken + Wert).</summary>
public sealed class CoreClockViewModel : ObservableObject
{
    private double _mhz;
    private double _maximum;

    public CoreClockViewModel(string name, double maximum)
    {
        Name = name;
        _maximum = maximum;
    }

    /// <summary>Kurzname, z. B. „#1“.</summary>
    public string Name { get; }

    public double Mhz
    {
        get => _mhz;
        private set
        {
            if (SetProperty(ref _mhz, value)) OnPropertyChanged(nameof(Text));
        }
    }

    /// <summary>Vollausschlag des Balkens in MHz.</summary>
    public double Maximum
    {
        get => _maximum;
        private set => SetProperty(ref _maximum, value);
    }

    public string Text => $"{_mhz:0} MHz";

    public void Update(double mhz)
    {
        if (mhz > Maximum) Maximum = Math.Ceiling(mhz / 100) * 100;
        Mhz = Math.Round(mhz);
    }
}

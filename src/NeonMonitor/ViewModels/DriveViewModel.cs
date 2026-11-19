using NeonMonitor.Models;

namespace NeonMonitor.ViewModels;

/// <summary>Anzeige eines Laufwerks inkl. Füllstand.</summary>
public sealed class DriveViewModel : ObservableObject
{
    private double _usedGb;
    private double _totalGb;

    public DriveViewModel(string name, string label)
    {
        Name = name;
        Label = string.IsNullOrWhiteSpace(label) ? "Lokaler Datenträger" : label;
    }

    public string Name { get; }
    public string Label { get; }

    public double Percent => _totalGb > 0 ? _usedGb / _totalGb * 100 : 0;
    public string SizeText => $"{_usedGb:0} / {_totalGb:0} GB";
    public string PercentText => $"{Percent:0} %";
    public bool IsAlmostFull => Percent >= 90;

    public void Update(DriveUsage d)
    {
        if (Math.Abs(_usedGb - d.UsedGb) < 0.01 && Math.Abs(_totalGb - d.TotalGb) < 0.01) return;
        _usedGb = d.UsedGb;
        _totalGb = d.TotalGb;
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(PercentText));
        OnPropertyChanged(nameof(IsAlmostFull));
    }
}

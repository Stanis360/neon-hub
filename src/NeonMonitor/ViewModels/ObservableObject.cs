using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NeonMonitor.ViewModels;

/// <summary>Basisklasse für ViewModels mit INotifyPropertyChanged.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Setzt das Feld und meldet die Änderung, falls sich der Wert geändert hat.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

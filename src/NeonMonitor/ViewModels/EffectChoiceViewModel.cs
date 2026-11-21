namespace NeonMonitor.ViewModels;

/// <summary>Ein Hintergrund-Effekt in der Auswahl (Chip unter Einstellungen → Design).</summary>
public sealed class EffectChoiceViewModel : ObservableObject
{
    private readonly Action<EffectChoiceViewModel> _select;
    private bool _isSelected;

    public EffectChoiceViewModel(string id, string name, bool selected, Action<EffectChoiceViewModel> select)
    {
        Id = id;
        Name = name;
        _isSelected = selected;
        _select = select;
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Zwei-Wege-gebunden an den Chip: Anklicken wählt den Effekt; das Abwählen erledigt die Auswahl selbst.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value && !_isSelected) _select(this);
            else SetProperty(ref _isSelected, value);
        }
    }

    internal void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));
}

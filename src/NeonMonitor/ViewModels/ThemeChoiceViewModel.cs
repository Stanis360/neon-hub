using System.Windows.Input;
using System.Windows.Media;
using NeonMonitor.Themes;

namespace NeonMonitor.ViewModels;

/// <summary>Ein Design in der Auswahl (mit kleinen Farbfeldern als Vorschau).</summary>
public sealed class ThemeChoiceViewModel : ObservableObject
{
    private bool _isSelected;

    public ThemeChoiceViewModel(ThemeDefinition theme, bool selected, Action<ThemeChoiceViewModel> select)
    {
        Id = theme.Id;
        Name = theme.Name;
        Description = theme.Description;
        Background = Brush(theme.BackgroundBottom);
        Panel = Brush(theme.Panel);
        Accent = Brush(theme.Accent);
        Secondary = Brush(theme.Secondary);
        _isSelected = selected;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public Brush Background { get; }
    public Brush Panel { get; }
    public Brush Accent { get; }
    public Brush Secondary { get; }
    public ICommand SelectCommand { get; }

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private static Brush Brush(string hex)
    {
        var b = new SolidColorBrush(ThemeManager.ColorOf(hex));
        b.Freeze();
        return b;
    }
}

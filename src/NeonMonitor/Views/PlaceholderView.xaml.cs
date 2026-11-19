using System.Windows;
using System.Windows.Controls;

namespace NeonMonitor.Views;

/// <summary>Platzhalterseite für noch nicht umgesetzte Bereiche.</summary>
public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PlaceholderView), new PropertyMetadata(""));
    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(string), typeof(PlaceholderView), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(PlaceholderView), new PropertyMetadata(""));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Step { get => (string)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace NeonMonitor.Infrastructure;

/// <summary>
/// WPF kennt kein „text-transform: uppercase“. Diese angehängte Eigenschaft schreibt den Text eines
/// TextBlocks in Großbuchstaben – auch wenn er per Binding später wechselt (z. B. CPU-Name).
/// </summary>
public static class TextCase
{
    public static readonly DependencyProperty UpperProperty = DependencyProperty.RegisterAttached(
        "Upper", typeof(bool), typeof(TextCase), new PropertyMetadata(false, OnChanged));

    public static bool GetUpper(DependencyObject d) => (bool)d.GetValue(UpperProperty);
    public static void SetUpper(DependencyObject d, bool value) => d.SetValue(UpperProperty, value);

    private static readonly DependencyPropertyDescriptor TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        TextDescriptor.RemoveValueChanged(tb, OnTextChanged);
        if ((bool)e.NewValue)
        {
            TextDescriptor.AddValueChanged(tb, OnTextChanged);
            OnTextChanged(tb, EventArgs.Empty);
        }
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock tb || string.IsNullOrEmpty(tb.Text)) return;
        var upper = tb.Text.ToUpper(System.Globalization.CultureInfo.CurrentCulture);
        if (upper != tb.Text) tb.SetCurrentValue(TextBlock.TextProperty, upper); // Binding bleibt erhalten
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace NeonMonitor.Infrastructure;

/// <summary>
/// WPF kennt kein „text-transform: uppercase“. Diese angehängten Eigenschaften schreiben den Text eines
/// TextBlocks in Großbuchstaben (<c>Upper</c>) bzw. setzen einen festen Text davor (<c>Prefix</c>) –
/// auch wenn er per Binding später wechselt (z. B. CPU-Name).
/// </summary>
public static class TextCase
{
    public static readonly DependencyProperty UpperProperty = DependencyProperty.RegisterAttached(
        "Upper", typeof(bool), typeof(TextCase), new PropertyMetadata(false, OnChanged));

    public static bool GetUpper(DependencyObject d) => (bool)d.GetValue(UpperProperty);
    public static void SetUpper(DependencyObject d, bool value) => d.SetValue(UpperProperty, value);

    public static readonly DependencyProperty PrefixProperty = DependencyProperty.RegisterAttached(
        "Prefix", typeof(string), typeof(TextCase), new PropertyMetadata("", OnChanged));

    public static string GetPrefix(DependencyObject d) => (string)d.GetValue(PrefixProperty);
    public static void SetPrefix(DependencyObject d, string value) => d.SetValue(PrefixProperty, value);

    private static readonly DependencyPropertyDescriptor TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        TextDescriptor.RemoveValueChanged(tb, OnTextChanged);
        if (GetUpper(tb) || !string.IsNullOrEmpty(GetPrefix(tb)))
        {
            TextDescriptor.AddValueChanged(tb, OnTextChanged);
            OnTextChanged(tb, EventArgs.Empty);
        }
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock tb || string.IsNullOrEmpty(tb.Text)) return;
        var prefix = GetPrefix(tb) ?? "";
        var body = prefix.Length > 0 && tb.Text.StartsWith(prefix, StringComparison.Ordinal) ? tb.Text[prefix.Length..] : tb.Text;
        if (GetUpper(tb)) body = body.ToUpper(System.Globalization.CultureInfo.CurrentCulture);
        var wanted = prefix + body;
        if (wanted != tb.Text) tb.SetCurrentValue(TextBlock.TextProperty, wanted); // Binding bleibt erhalten
    }
}

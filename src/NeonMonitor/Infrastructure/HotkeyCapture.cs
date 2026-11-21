using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NeonMonitor.Services.Deck;

namespace NeonMonitor.Infrastructure;

/// <summary>
/// Angehängte Eigenschaft für Textfelder: Statt Text zu tippen, wird die gedrückte Tastenkombination
/// eingetragen (z. B. „Strg+Umschalt+M“). Rücktaste/Entf ohne Zusatztaste löscht.
/// </summary>
public static class HotkeyCapture
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(HotkeyCapture), new PropertyMetadata(false, OnChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool v) => d.SetValue(IsEnabledProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.PreviewKeyDown -= OnKeyDown;
        if ((bool)e.NewValue) box.PreviewKeyDown += OnKeyDown;
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        // Tab ohne Zusatztaste bleibt Tab (Fokus weiter), sonst wird nichts getippt
        if (key == Key.Tab && mods == ModifierKeys.None) return;
        e.Handled = true;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0 || Hotkey.IsModifierVk(vk)) return; // nur Zusatztaste gedrückt → auf die eigentliche Taste warten

        box.Text = (key is Key.Back or Key.Delete) && mods == ModifierKeys.None
            ? ""
            : Hotkey.Format(mods.HasFlag(ModifierKeys.Control), mods.HasFlag(ModifierKeys.Shift),
                            mods.HasFlag(ModifierKeys.Alt), mods.HasFlag(ModifierKeys.Windows), (ushort)vk);
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        box.CaretIndex = box.Text.Length;
    }
}

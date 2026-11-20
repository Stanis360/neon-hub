using System.Windows;
using System.Windows.Threading;
using NeonMonitor.Infrastructure;
using NeonMonitor.ViewModels;

namespace NeonMonitor.Views;

/// <summary>
/// Erzeugt, zeigt und positioniert das Overlay-Fenster (Umsetzung von <see cref="IOverlayHost"/>).
/// Positioniert wird in physischen Pixeln, damit es auch bei Monitoren mit unterschiedlicher
/// Skalierung genau in der gewünschten Ecke landet.
/// </summary>
public sealed class OverlayController : IOverlayHost
{
    private const int EdgeMargin = 16; // Abstand zum Bildschirmrand in Pixeln

    private readonly OverlayViewModel _vm;
    private OverlayWindow? _window;

    public OverlayController(OverlayViewModel vm) => _vm = vm;

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            if (_window is null) CreateWindow();
            _window!.Show();
        }
        else
        {
            _window?.Hide();
        }
    }

    public void SetClickThrough(bool clickThrough)
    {
        if (_window is not null) NativeWindows.SetOverlayStyle(_window.Handle, clickThrough);
    }

    public void MoveTo(MonitorInfo monitor, OverlayCorner corner)
    {
        if (_window is null) return;
        var work = monitor.WorkArea;

        // Erst auf den Zielmonitor schieben – Windows passt dabei ggf. die Skalierung (DPI) an –,
        // danach mit der dann gültigen Fenstergröße exakt in die Ecke setzen.
        NativeWindows.MoveWindow(_window.Handle, work.Left + EdgeMargin, work.Top + EdgeMargin);
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var size = NativeWindows.GetWindowRect(_window.Handle);
            var x = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft
                ? work.Left + EdgeMargin
                : work.Right - size.Width - EdgeMargin;
            var y = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight
                ? work.Top + EdgeMargin
                : work.Bottom - size.Height - EdgeMargin;
            NativeWindows.MoveWindow(_window.Handle, x, y);
            _vm.SavePosition(x, y);
        });
    }

    private void CreateWindow()
    {
        _window = new OverlayWindow(_vm);
        _window.SourceInitialized += (_, _) =>
        {
            NativeWindows.SetOverlayStyle(_window.Handle, _vm.ClickThrough);
            RestorePosition();
        };
        // Owner bewusst nicht gesetzt: Das Overlay bleibt sichtbar, wenn das Hauptfenster minimiert ist.
        Application.Current.Exit += (_, _) => _window?.Close();
    }

    /// <summary>Letzte Position wiederherstellen – oder beim ersten Mal: oben rechts auf einem Nebenmonitor.</summary>
    private void RestorePosition()
    {
        var monitors = NativeWindows.GetMonitors();
        if (_vm.SavedPosition is { } p && monitors.Any(m => m.Bounds.Contains(p.X + 20, p.Y + 20)))
        {
            NativeWindows.MoveWindow(_window!.Handle, p.X, p.Y);
            return;
        }
        var target = monitors.FirstOrDefault(m => !m.IsPrimary) ?? monitors.FirstOrDefault();
        if (target is not null) MoveTo(target, OverlayCorner.TopRight);
    }
}

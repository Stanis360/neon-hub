using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using NeonMonitor.Infrastructure;
using NeonMonitor.ViewModels;

namespace NeonMonitor.Views;

/// <summary>Das Overlay-Fenster. Verschieben per Maus; die Position wird gespeichert.</summary>
public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;

    public OverlayWindow(OverlayViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        MouseLeftButtonDown += OnMouseDown;
    }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm.ClickThrough) return;
        DragMove(); // kehrt erst nach dem Loslassen zurück
        var r = NativeWindows.GetWindowRect(Handle);
        _vm.SavePosition(r.Left, r.Top);
    }
}

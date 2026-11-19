using System.Windows.Controls;

namespace NeonMonitor.Views;

/// <summary>FPS-Seite: Kennzahlen, Verlauf, Frametimes, Auswahl der Anwendung. DataContext = FpsViewModel.</summary>
public partial class FpsView : UserControl
{
    public FpsView() => InitializeComponent();
}

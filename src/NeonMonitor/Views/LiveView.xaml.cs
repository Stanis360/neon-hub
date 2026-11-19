using System.Windows.Controls;

namespace NeonMonitor.Views;

/// <summary>Live-Dashboard: CPU, GPU, RAM und Laufwerke. Daten kommen per Binding aus dem MainViewModel.</summary>
public partial class LiveView : UserControl
{
    public LiveView() => InitializeComponent();
}

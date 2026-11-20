using System.Windows.Controls;

namespace NeonMonitor.Views;

/// <summary>Reiter „Kühlung“: Lüfter, Temperaturen, Lüftersteuerung. DataContext = CoolingViewModel.</summary>
public partial class CoolingView : UserControl
{
    public CoolingView() => InitializeComponent();
}

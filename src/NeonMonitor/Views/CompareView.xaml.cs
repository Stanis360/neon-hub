using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NeonMonitor.Infrastructure;
using NeonMonitor.ViewModels;

namespace NeonMonitor.Views;

/// <summary>Reiter „Vergleich“. DataContext = CompareViewModel. Enthält den Bild-Export (reine Darstellungsaufgabe).</summary>
public partial class CompareView : UserControl
{
    public CompareView() => InitializeComponent();

    private void OnSaveImage(object sender, RoutedEventArgs e)
    {
        var image = Render();
        if (image is null) return;
        var file = Path.Combine(AppPaths.ReportsDir, $"Vergleich_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        using (var fs = File.Create(file))
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(image));
            enc.Save(fs);
        }
        // Explorer öffnen und die Datei markieren
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
    }

    private void OnCopyImage(object sender, RoutedEventArgs e)
    {
        var image = Render();
        if (image is null) return;
        Clipboard.SetImage(image);
        MessageBox.Show("Vergleich als Bild kopiert – mit Strg+V z. B. in Discord einfügen.", "Bericht",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Rendert Tabelle + Diagramm in voller Höhe mit dunklem Hintergrund (1,5-fache Auflösung).</summary>
    private BitmapSource? Render()
    {
        if (DataContext is not CompareViewModel { HasSelection: true }) return null;

        // SetCurrentValue statt Zuweisung: die Bindung an HasSelection bleibt erhalten
        ExportButtons.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
        ReportPanel.UpdateLayout();
        try
        {
            const double scale = 1.5;
            var w = ReportPanel.ActualWidth;
            var h = ReportPanel.ActualHeight + 28; // Platz für die Fußzeile
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle((Brush)FindResource("BackgroundBrush"), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(ReportPanel) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                                 null, new Rect(0, 0, w, ReportPanel.ActualHeight));
                var footer = new FormattedText($"NeonMonitor · {DateTime.Now:dd.MM.yyyy HH:mm}",
                    System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Bahnschrift"), 11, (Brush)FindResource("TextDimBrush"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(footer, new Point(w - footer.Width - 12, h - 22));
            }
            var bmp = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            ExportButtons.GetBindingExpression(VisibilityProperty)?.UpdateTarget();
        }
    }
}

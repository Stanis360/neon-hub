using System.Windows;

namespace NeonMonitor;

/// <summary>Hauptfenster mit eigener Titelleiste.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += OnStateChanged;
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Rahmenlose Fenster ragen maximiert ein paar Pixel über den Bildschirmrand → ausgleichen
        RootBorder.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";

        // Minimiert: Animation pausieren (spart Ressourcen, während gespielt wird)
        BackgroundLayer.Visibility = WindowState == WindowState.Minimized ? Visibility.Hidden : Visibility.Visible;
    }
}

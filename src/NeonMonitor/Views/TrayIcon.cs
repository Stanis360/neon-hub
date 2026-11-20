using System.ComponentModel;
using WinForms = System.Windows.Forms;

namespace NeonMonitor.Views;

/// <summary>
/// Symbol im Infobereich der Taskleiste: Kurzinfo (Temperaturen, FPS) beim Darüberfahren,
/// Doppelklick öffnet das Fenster, Rechtsklick zeigt ein Menü. Zeigt auch Warnungen als Windows-Benachrichtigung.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _overlayItem;
    private readonly WinForms.ToolStripMenuItem _recordItem;

    public TrayIcon(Action open, Action toggleOverlay, Action toggleRecording, Action benchmark, Action exit)
    {
        _overlayItem = new WinForms.ToolStripMenuItem("Overlay ein/aus   (Strg+Alt+O)", null, (_, _) => toggleOverlay());
        _recordItem = new WinForms.ToolStripMenuItem("Aufnahme starten   (Strg+Alt+R)", null, (_, _) => toggleRecording());
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(new WinForms.ToolStripMenuItem("NeonMonitor öffnen", null, (_, _) => open()) { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(_overlayItem);
        menu.Items.Add(_recordItem);
        menu.Items.Add(new WinForms.ToolStripMenuItem("Benchmark starten   (Strg+Alt+B)", null, (_, _) => benchmark()));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Beenden", null, (_, _) => exit()));

        _icon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Text = "NeonMonitor",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => open();
    }

    /// <summary>Text beim Darüberfahren (max. 127 Zeichen).</summary>
    public void SetText(string text) => _icon.Text = text.Length > 127 ? text[..127] : text;

    public void SetRecording(bool recording) =>
        _recordItem.Text = recording ? "Aufnahme stoppen   (Strg+Alt+R)" : "Aufnahme starten   (Strg+Alt+R)";

    /// <summary>Windows-Benachrichtigung anzeigen.</summary>
    public void Notify(string title, string text, bool warning = false) =>
        _icon.ShowBalloonTip(5000, title, text, warning ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

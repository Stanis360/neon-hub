using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace NeonMonitor.Infrastructure;

/// <summary>
/// Systemweite Tastenkürzel (funktionieren auch, während ein Spiel im Vordergrund ist).
/// Nutzt RegisterHotKey – es wird nichts in Spiele eingeschleust und keine Tastatur „mitgelesen“.
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    public const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const int WmHotkey = 0x0312;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0xB000;

    /// <param name="windowHandle">Fenster, das die Hotkey-Nachrichten empfängt (Hauptfenster).</param>
    public GlobalHotkeys(IntPtr windowHandle)
    {
        _source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("Kein Fensterhandle");
        _source.AddHook(WndProc);
    }

    /// <summary>Registriert ein Kürzel. Gibt false zurück, wenn ein anderes Programm es schon belegt.</summary>
    /// <param name="modifiers">Kombination aus ModControl/ModAlt/ModShift.</param>
    /// <param name="virtualKey">Virtueller Tastencode, z. B. 0x4F für „O“.</param>
    public bool Register(uint modifiers, uint virtualKey, Action action)
    {
        var id = _nextId++;
        if (!RegisterHotKey(_source.Handle, id, modifiers | ModNoRepeat, virtualKey))
        {
            Log.Warn($"Hotkey 0x{virtualKey:X} (Mod {modifiers}) ist bereits belegt");
            return false;
        }
        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}

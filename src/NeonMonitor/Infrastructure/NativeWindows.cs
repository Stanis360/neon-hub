using System.Runtime.InteropServices;

namespace NeonMonitor.Infrastructure;

/// <summary>Ein angeschlossener Bildschirm (Koordinaten in physischen Pixeln).</summary>
/// <param name="Index">Laufende Nummer von links nach rechts (1 = ganz links).</param>
/// <param name="Bounds">Gesamte Fläche.</param>
/// <param name="WorkArea">Fläche ohne Taskleiste.</param>
/// <param name="IsPrimary">Hauptbildschirm?</param>
public sealed record MonitorInfo(int Index, RectPx Bounds, RectPx WorkArea, bool IsPrimary)
{
    public string Title => $"Monitor {Index}" + (IsPrimary ? " (Haupt)" : "");
    public string Resolution => $"{Bounds.Width} × {Bounds.Height}";
}

/// <summary>Rechteck in physischen Pixeln.</summary>
public readonly record struct RectPx(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>
/// Win32-Hilfsfunktionen für Fenster und Bildschirme. WPF kennt keine Bildschirmliste und rechnet
/// in geräteunabhängigen Einheiten – bei drei Monitoren mit evtl. unterschiedlicher Skalierung ist
/// es am zuverlässigsten, direkt in physischen Pixeln zu positionieren.
/// </summary>
public static class NativeWindows
{
    /// <summary>Alle Bildschirme, sortiert von links nach rechts.</summary>
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var raw = new List<(RectPx Bounds, RectPx Work, bool Primary)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (GetMonitorInfo(hMon, ref info))
                raw.Add((info.Monitor.ToRect(), info.Work.ToRect(), (info.Flags & MonitorInfoPrimary) != 0));
            return true;
        }, IntPtr.Zero);

        return raw.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top)
                  .Select((m, i) => new MonitorInfo(i + 1, m.Bounds, m.Work, m.Primary))
                  .ToList();
    }

    /// <summary>Fensterrechteck in physischen Pixeln.</summary>
    public static RectPx GetWindowRect(IntPtr hwnd) =>
        GetWindowRectNative(hwnd, out var r) ? r.ToRect() : default;

    /// <summary>Fenster in physischen Pixeln verschieben (Größe bleibt).</summary>
    public static void MoveWindow(IntPtr hwnd, int x, int y) =>
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);

    /// <summary>
    /// Fensterstile für das Overlay: nicht in Alt+Tab, stiehlt keinen Fokus und
    /// optional „klick-durchlässig“ (Mausklicks gehen an das Fenster darunter).
    /// </summary>
    public static void SetOverlayStyle(IntPtr hwnd, bool clickThrough)
    {
        var ex = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        ex |= WsExToolWindow | WsExNoActivate;
        ex = clickThrough ? ex | WsExTransparent | WsExLayered : ex & ~WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(ex));
    }

    // ---------------- P/Invoke ----------------

    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExLayered = 0x80000, WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x1, SwpNoZOrder = 0x4, SwpNoActivate = 0x10;
    private const uint MonitorInfoPrimary = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public RectPx ToRect() => new(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    private static extern bool GetWindowRectNative(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}

using System.Runtime.InteropServices;

namespace NeonMonitor.Services.Sensors;

/// <summary>
/// Schlanker Wrapper um die Windows-Leistungsindikatoren (PDH, pdh.dll).
/// Unterstützt Platzhalter-Pfade wie <c>\GPU Engine(*engtype_3D)\Utilization Percentage</c>
/// und liefert dann alle aktuellen Instanzen mit ihrem Wert zurück.
/// Das ist dieselbe Datenquelle, die auch der Task-Manager für die GPU-Anzeige nutzt.
/// </summary>
internal sealed class PdhQuery : IDisposable
{
    private IntPtr _query;
    private readonly Dictionary<string, IntPtr> _counters = new();

    public PdhQuery()
    {
        Check(PdhOpenQuery(null, IntPtr.Zero, out _query), "PdhOpenQuery");
    }

    /// <summary>Fügt einen (sprachunabhängigen, englischen) Zählerpfad hinzu.</summary>
    public void Add(string path)
    {
        Check(PdhAddEnglishCounter(_query, path, IntPtr.Zero, out var counter), $"PdhAddEnglishCounter({path})");
        _counters[path] = counter;
    }

    /// <summary>Neue Messwerte einsammeln (Ratenzähler brauchen zwei Aufrufe für den ersten Wert).</summary>
    public void Collect() => PdhCollectQueryData(_query);

    /// <summary>Alle Instanzen eines Zählers mit ihrem aktuellen Wert.</summary>
    public List<(string Instance, double Value)> Read(string path)
    {
        var result = new List<(string, double)>();
        if (!_counters.TryGetValue(path, out var counter)) return result;

        uint size = 0;
        var status = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out _, IntPtr.Zero);
        if (status != PDH_MORE_DATA || size == 0) return result;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            status = PdhGetFormattedCounterArray(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out var count, buffer);
            if (status != 0) return result;

            var stride = Marshal.SizeOf<PdhFmtCounterValueItem>();
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buffer + i * stride);
                // CStatus 0 = gültig, 1 = neuer Wert (PDH_CSTATUS_NEW_DATA)
                if (item.CStatus > 1) continue;
                var name = Marshal.PtrToStringUni(item.Name) ?? "";
                result.Add((name, item.DoubleValue));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return result;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) PdhCloseQuery(_query);
        _query = IntPtr.Zero;
    }

    private static void Check(uint status, string what)
    {
        if (status != 0) throw new InvalidOperationException($"{what} fehlgeschlagen (0x{status:X8})");
    }

    // ---------------- P/Invoke ----------------

    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_FMT_NOCAP100 = 0x00008000;
    private const uint PDH_MORE_DATA = 0x800007D2;

    /// <summary>PDH_FMT_COUNTERVALUE_ITEM_W für x64 (Zeiger 8 Byte, Union ab Offset 8 im Wert).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PdhFmtCounterValueItem
    {
        [FieldOffset(0)] public IntPtr Name;
        [FieldOffset(8)] public uint CStatus;
        [FieldOffset(16)] public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}

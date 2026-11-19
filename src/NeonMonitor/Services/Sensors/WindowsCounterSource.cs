using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Sensors;

/// <summary>
/// Fallback-Quelle über Bordmittel von Windows (funktioniert ohne Treiber und mit jeder GPU):
/// <list type="bullet">
/// <item>CPU-Last: „Processor Information / % Processor Utility“ (wie im Task-Manager).</item>
/// <item>GPU-Last und VRAM-Belegung: „GPU Engine“ / „GPU Adapter Memory“ (WDDM).</item>
/// <item>RAM: GlobalMemoryStatusEx.</item>
/// </list>
/// Springt nur ein, wenn LibreHardwareMonitor einen Wert nicht liefert –
/// z. B. falls ADL auf einer neuen RDNA-4-Karte (noch) keine Last meldet.
/// Temperaturen, Takte und Leistung gibt es hier nicht (dafür bräuchte man ADLX).
/// </summary>
public sealed partial class WindowsCounterSource : ISensorSource
{
    private const string CpuUtility = @"\Processor Information(_Total)\% Processor Utility";
    private const string GpuEngine3D = @"\GPU Engine(*engtype_3D)\Utilization Percentage";
    private const string GpuDedicated = @"\GPU Adapter Memory(*)\Dedicated Usage";

    private PdhQuery? _pdh;

    public string Name => "Windows-Leistungsindikatoren";
    public bool IsAvailable { get; private set; }
    public string Status { get; private set; } = "nicht gestartet";

    public void Initialize()
    {
        _pdh = new PdhQuery();
        var added = new List<string>();
        foreach (var path in new[] { CpuUtility, GpuEngine3D, GpuDedicated })
        {
            try { _pdh.Add(path); added.Add(path); }
            catch { /* einzelner Zähler fehlt (z. B. ältere Windows-Version) – weiter */ }
        }
        _pdh.Collect(); // erste Messung als Basis für Ratenzähler
        IsAvailable = true;
        Status = $"OK ({added.Count}/3 Zähler)";
    }

    public void Fill(HardwareSnapshot s)
    {
        // RAM geht immer, auch ohne PDH.
        if (s.RamUsedGb is null || s.RamTotalGb is null || s.RamLoad is null)
        {
            var mem = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref mem))
            {
                const double gb = 1024d * 1024 * 1024;
                s.RamTotalGb ??= mem.TotalPhys / gb;
                s.RamUsedGb ??= (mem.TotalPhys - mem.AvailPhys) / gb;
                s.RamLoad ??= mem.MemoryLoad;
            }
        }

        if (_pdh is null) return;
        _pdh.Collect();

        if (s.CpuLoad is null)
        {
            var cpu = _pdh.Read(CpuUtility);
            if (cpu.Count > 0) s.CpuLoad = Math.Clamp(cpu[0].Value, 0, 100);
        }

        if (s.GpuLoad is null || s.GpuVramUsedMb is null)
        {
            var (luid, vramBytes) = FindDiscreteAdapter();
            if (luid is not null)
            {
                s.GpuVramUsedMb ??= vramBytes / (1024d * 1024);
                // Summe aller Prozesse auf der 3D-Engine dieser Karte = Auslastung der Engine
                var load = _pdh.Read(GpuEngine3D)
                    .Where(x => ExtractLuid(x.Instance) == luid)
                    .Sum(x => x.Value);
                s.GpuLoad ??= Math.Clamp(load, 0, 100);
            }
        }
    }

    /// <summary>Die Grafikkarte mit der höchsten dedizierten Speicherbelegung = dedizierte GPU.</summary>
    private (string? Luid, double Bytes) FindDiscreteAdapter()
    {
        if (_pdh is null) return (null, 0);
        var best = _pdh.Read(GpuDedicated)
            .Select(x => (Luid: ExtractLuid(x.Instance), x.Value))
            .Where(x => x.Luid is not null)
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();
        return (best.Luid, best.Value);
    }

    /// <summary>Instanznamen enthalten z. B. „luid_0x00000000_0x0000D1A3“.</summary>
    private static string? ExtractLuid(string instance)
    {
        var m = LuidRegex().Match(instance);
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    [GeneratedRegex(@"luid_0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+")]
    private static partial Regex LuidRegex();

    public void AppendReport(StringBuilder r)
    {
        r.AppendLine($"=== {Name} ===");
        r.AppendLine($"Status: {Status}");
        if (_pdh is null) return;
        _pdh.Collect();
        foreach (var (inst, val) in _pdh.Read(CpuUtility)) r.AppendLine($"  CPU Utility {inst} = {val:0.0} %");
        foreach (var (inst, val) in _pdh.Read(GpuDedicated)) r.AppendLine($"  VRAM {inst} = {val / 1048576:0} MB");
        var engines = _pdh.Read(GpuEngine3D)
            .GroupBy(x => ExtractLuid(x.Instance) ?? "?")
            .Select(g => $"  3D-Last {g.Key} = {g.Sum(x => x.Value):0.0} %");
        foreach (var line in engines) r.AppendLine(line);
    }

    public void Dispose()
    {
        _pdh?.Dispose();
        _pdh = null;
        IsAvailable = false;
    }

    // ---------------- P/Invoke ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}

using System.Text;
using LibreHardwareMonitor.Hardware;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Sensors;

/// <summary>
/// Hauptquelle: LibreHardwareMonitor.
/// <list type="bullet">
/// <item>CPU (Zen 5 / 9800X3D): Last, Tctl/Tdie, Kerntakte, Package-Leistung – über PawnIO + SMU/MSR.</item>
/// <item>GPU (RDNA 4 / RX 9070 XT): Last, Temperaturen, Takte, VRAM, Leistung – über AMD ADL (Teil des Adrenalin-Treibers).</item>
/// <item>RAM: Belegung.</item>
/// </list>
/// Sensoren werden über Typ + Name gesucht. Für jeden Wert gibt es eine Prioritätsliste
/// möglicher Sensornamen, weil sich die Namen je nach Hardware/Version unterscheiden.
/// </summary>
public sealed class LibreHardwareSource : ISensorSource
{
    private Computer? _computer;
    private IHardware? _cpu;
    private IHardware? _gpu;
    private IHardware? _memory;

    public string Name => "LibreHardwareMonitor";
    public bool IsAvailable { get; private set; }
    public string Status { get; private set; } = "nicht gestartet";

    /// <summary>Name der automatisch gewählten Grafikkarte (die mit dem meisten VRAM).</summary>
    public string? SelectedGpuName => _gpu?.Name;

    public void Initialize()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            // Mainboard/Lüfter/Laufwerke vorerst aus: spart Startzeit; Laufwerke liest DriveSource.
            IsMotherboardEnabled = false,
            IsStorageEnabled = false,
        };
        _computer.Open(); // kann einige Sekunden dauern (Treiber laden, Hardware erkennen)

        foreach (var hw in _computer.Hardware) UpdateRecursive(hw);

        _cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        _memory = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        _gpu = SelectDiscreteGpu(_computer.Hardware);

        IsAvailable = true;
        var parts = new List<string>();
        if (_cpu is not null) parts.Add("CPU");
        if (_gpu is not null) parts.Add("GPU");
        if (_memory is not null) parts.Add("RAM");
        Status = parts.Count > 0 ? "OK (" + string.Join(", ", parts) + ")" : "keine Hardware erkannt";

        Log.Info($"LHM: CPU={_cpu?.Name ?? "-"}, GPU={_gpu?.Name ?? "-"}, RAM={(_memory is null ? "-" : "ok")}");
    }

    /// <summary>
    /// Der 9800X3D hat eine integrierte Radeon-Grafik – es gibt also zwei AMD-GPUs.
    /// Wir nehmen die mit dem meisten dedizierten Speicher (= die RX 9070 XT).
    /// </summary>
    private static IHardware? SelectDiscreteGpu(IEnumerable<IHardware> hardware)
    {
        var gpus = hardware.Where(h => h.HardwareType is HardwareType.GpuAmd
                                                     or HardwareType.GpuNvidia
                                                     or HardwareType.GpuIntel).ToList();
        if (gpus.Count <= 1) return gpus.FirstOrDefault();

        return gpus
            .OrderByDescending(g => FindValue(g, SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total") ?? 0)
            // Fallback, falls kein Speicherwert existiert: iGPU heißt meist „AMD Radeon(TM) Graphics“
            .ThenBy(g => g.Name.Contains("(TM) Graphics", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .First();
    }

    public void Fill(HardwareSnapshot s)
    {
        if (!IsAvailable) return;

        if (_cpu is not null)
        {
            UpdateRecursive(_cpu);
            s.CpuName ??= _cpu.Name;
            s.CpuLoad ??= FindValue(_cpu, SensorType.Load, "CPU Total");
            s.CpuTemp ??= FindValue(_cpu, SensorType.Temperature,
                              "Core (Tctl/Tdie)", "Core (Tdie)", "Core (Tctl)", "CCDs Max (Tdie)",
                              "CCD1 (Tdie)", "CPU Package", "Package", "Core Max")
                          ?? MaxValue(_cpu, SensorType.Temperature, n => !n.Contains("Distance", StringComparison.OrdinalIgnoreCase));
            s.CpuPower ??= FindValue(_cpu, SensorType.Power, "Package", "CPU Package");

            // Kerntakte: „Core #1“ … „Core #8“
            var cores = AllSensors(_cpu)
                .Where(x => x.SensorType == SensorType.Clock
                            && x.Name.StartsWith("Core #", StringComparison.OrdinalIgnoreCase)
                            && x.Value is > 0)
                .OrderBy(x => x.Index)
                .Select(x => new CoreClock(x.Name.Replace("Core ", ""), x.Value!.Value))
                .ToList();
            if (cores.Count > 0)
            {
                s.CpuClockAvg ??= cores.Average(c => c.Mhz);
                s.CpuClockMax ??= cores.Max(c => c.Mhz);
                if (s.CpuCoreClocks.Count == 0) s.CpuCoreClocks = cores;
            }
        }

        if (_gpu is not null)
        {
            UpdateRecursive(_gpu);
            s.GpuName ??= _gpu.Name;
            s.GpuLoad ??= FindValue(_gpu, SensorType.Load, "GPU Core", "D3D 3D");
            s.GpuTemp ??= FindValue(_gpu, SensorType.Temperature, "GPU Core", "GPU Edge");
            s.GpuHotspot ??= FindValue(_gpu, SensorType.Temperature, "GPU Hot Spot", "GPU Junction");
            s.GpuClock ??= FindValue(_gpu, SensorType.Clock, "GPU Core", "GPU Shader");
            s.GpuMemClock ??= FindValue(_gpu, SensorType.Clock, "GPU Memory");
            s.GpuVramUsedMb ??= FindValue(_gpu, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used");
            s.GpuVramTotalMb ??= FindValue(_gpu, SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total");
            s.GpuPower ??= FindValue(_gpu, SensorType.Power, "GPU Package", "GPU Total", "GPU PPT", "GPU Power", "GPU Core");
            s.GpuFan ??= FindValue(_gpu, SensorType.Fan, "GPU Fan");
        }

        if (_memory is not null)
        {
            UpdateRecursive(_memory);
            var used = FindValue(_memory, SensorType.Data, "Memory Used");
            var avail = FindValue(_memory, SensorType.Data, "Memory Available");
            s.RamUsedGb ??= used;
            if (used is not null && avail is not null) s.RamTotalGb ??= used + avail;
            s.RamLoad ??= FindValue(_memory, SensorType.Load, "Memory");
        }
    }

    public void AppendReport(StringBuilder r)
    {
        r.AppendLine($"=== {Name} ===");
        r.AppendLine($"Status: {Status}");
        r.AppendLine($"Gewählte GPU: {_gpu?.Name ?? "-"}");
        if (_computer is null) return;

        foreach (var hw in _computer.Hardware)
        {
            UpdateRecursive(hw);
            AppendHardware(r, hw, 0);
        }
    }

    private static void AppendHardware(StringBuilder r, IHardware hw, int depth)
    {
        var indent = new string(' ', depth * 2);
        r.AppendLine($"{indent}[{hw.HardwareType}] {hw.Name}  ({hw.Identifier})");
        foreach (var s in hw.Sensors.OrderBy(s => s.SensorType).ThenBy(s => s.Index))
        {
            var value = s.Value.HasValue ? s.Value.Value.ToString("0.###") : "—";
            r.AppendLine($"{indent}  {s.SensorType,-12} {s.Name,-34} = {value,10}   {s.Identifier}");
        }
        foreach (var sub in hw.SubHardware) AppendHardware(r, sub, depth + 1);
    }

    // ---------------- Hilfsfunktionen ----------------

    private static void UpdateRecursive(IHardware hw)
    {
        hw.Update();
        foreach (var sub in hw.SubHardware) UpdateRecursive(sub);
    }

    private static IEnumerable<ISensor> AllSensors(IHardware hw) =>
        hw.Sensors.Concat(hw.SubHardware.SelectMany(AllSensors));

    /// <summary>Erster Sensor (in Reihenfolge der Namen), der einen Wert liefert.</summary>
    private static double? FindValue(IHardware hw, SensorType type, params string[] names)
    {
        var candidates = AllSensors(hw).Where(x => x.SensorType == type && x.Value.HasValue).ToList();
        foreach (var name in names)
        {
            var match = candidates.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null && float.IsFinite(match.Value!.Value)) return match.Value.Value;
        }
        return null;
    }

    private static double? MaxValue(IHardware hw, SensorType type, Func<string, bool> nameFilter)
    {
        var values = AllSensors(hw)
            .Where(x => x.SensorType == type && x.Value.HasValue && nameFilter(x.Name))
            .Select(x => (double)x.Value!.Value)
            .Where(double.IsFinite)
            .ToList();
        return values.Count > 0 ? values.Max() : null;
    }

    public void Dispose()
    {
        try { _computer?.Close(); } // entlädt Treiber/Handles sauber
        catch (Exception ex) { Log.Warn($"LHM Close: {ex.Message}"); }
        _computer = null;
        IsAvailable = false;
    }
}

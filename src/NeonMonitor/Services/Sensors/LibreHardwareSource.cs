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
public sealed class LibreHardwareSource(bool safeMode, AppSettings settings) : ISensorSource
{
    private Computer? _computer;
    private IHardware? _cpu;
    private IHardware? _gpu;
    private IHardware? _memory;
    private IHardware? _board;
    private List<IHardware> _dimms = new();
    private List<IHardware> _storage = new();
    private int _storageTick;
    private string _baseStatus = "";
    private int _badBoardReads;
    private DateTime _lastBoardReconnect = DateTime.MinValue;
    private bool _boardInvalidLogged;

    /// <summary>Lüftersteuerung (Mainboard-Lüfter).</summary>
    public FanControlEngine FanControl { get; } = new();

    public string Name => "LibreHardwareMonitor";
    public bool IsAvailable { get; private set; }
    public string Status { get; private set; } = "nicht gestartet";

    /// <summary>Name der automatisch gewählten Grafikkarte (die mit dem meisten VRAM).</summary>
    public string? SelectedGpuName => _gpu?.Name;

    public void Initialize()
    {
        // Sicherer Modus: CPU und RAM brauchen den PawnIO-Kernel-Treiber (MSR/SMU/SMBus).
        // Ohne sie bleibt nur die GPU über die AMD-Treiberschnittstelle; RAM und CPU-Last liefert dann Windows.
        _computer = new Computer
        {
            IsCpuEnabled = !safeMode,
            IsGpuEnabled = true,
            IsMemoryEnabled = !safeMode,
            // Mainboard/Lüfter/Laufwerke vorerst aus: spart Startzeit; Laufwerke liest DriveSource.
            // Mainboard (Lüfter, Mainboard-Temperaturen) braucht ebenfalls PawnIO → im sicheren Modus aus
            IsMotherboardEnabled = !safeMode,
            // SSD-Temperaturen über SMART (ohne PawnIO)
            IsStorageEnabled = true,
        };
        _computer.Open(); // kann einige Sekunden dauern (Treiber laden, Hardware erkennen)

        foreach (var hw in _computer.Hardware) UpdateRecursive(hw);

        _cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        // LHM meldet mehrere „Memory“-Einträge: /ram (echter Arbeitsspeicher), /vram (virtueller Speicher
        // inkl. Auslagerungsdatei) und /memory/dimm/n (einzelne Module mit Temperatur). Wir brauchen /ram.
        var memories = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Memory).ToList();
        _memory = memories.FirstOrDefault(h => h.Identifier.ToString() == "/ram")
                  ?? memories.FirstOrDefault(h => h.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                  ?? memories.FirstOrDefault(h => !h.Identifier.ToString().StartsWith("/vram") && !h.Identifier.ToString().Contains("/dimm/"));
        _dimms = memories.Where(h => h.Identifier.ToString().Contains("/dimm/")).ToList();
        _gpu = SelectDiscreteGpu(_computer.Hardware);
        _board = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
        _storage = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();

        IsAvailable = true;
        var parts = new List<string>();
        if (_cpu is not null) parts.Add("CPU");
        if (_gpu is not null) parts.Add("GPU");
        if (_memory is not null) parts.Add("RAM");
        if (_board is not null) parts.Add("Mainboard");
        if (_storage.Count > 0) parts.Add($"{_storage.Count} Laufwerk(e)");
        Status = parts.Count > 0 ? "OK (" + string.Join(", ", parts) + ")" : "keine Hardware erkannt";
        if (safeMode) Status += " · sicherer Modus";
        _baseStatus = Status;

        Log.Info($"LHM: CPU={_cpu?.Name ?? "-"}, GPU={_gpu?.Name ?? "-"}, RAM={(_memory is null ? "-" : "ok")}, Board={_board?.Name ?? "-"}");
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
            s.CpuCoreMaxLoad ??= FindValue(_cpu, SensorType.Load, "CPU Core Max")
                                 ?? MaxValue(_cpu, SensorType.Load, n => n.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase));
            s.CpuTemp ??= FindValue(_cpu, SensorType.Temperature,
                              "Core (Tctl/Tdie)", "Core (Tdie)", "Core (Tctl)", "CCDs Max (Tdie)",
                              "CCD1 (Tdie)", "CPU Package", "Package", "Core Max")
                          ?? MaxValue(_cpu, SensorType.Temperature, n => !n.Contains("Distance", StringComparison.OrdinalIgnoreCase));
            s.CpuPower ??= FindValue(_cpu, SensorType.Power, "Package", "CPU Package");

            // Kerntakte: LHM liefert pro Kern zwei Werte – „Core #1“ (Nenntakt) und
            // „Core #1 (Effective)“ (tatsächlich gearbeiteter Takt, wie in Ryzen Master/HWiNFO).
            // Wir zeigen nur einen Wert pro Kern und bevorzugen den effektiven.
            var coreSensors = AllSensors(_cpu)
                .Where(x => x.SensorType == SensorType.Clock
                            && x.Name.StartsWith("Core #", StringComparison.OrdinalIgnoreCase)
                            && x.Value is > 0)
                .ToList();
            var effective = coreSensors.Where(IsEffective).ToList();
            var cores = (effective.Count > 0 ? effective : coreSensors.Where(x => !IsEffective(x)))
                .Select(x => (Number: CoreNumber(x.Name), Mhz: (double)x.Value!.Value))
                .OrderBy(x => x.Number)
                .Select(x => new CoreClock($"#{x.Number}", x.Mhz))
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
            s.GpuMemTemp ??= FindValue(_gpu, SensorType.Temperature, "GPU Memory", "GPU Memory Junction");
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

        // DDR5-Module: nur der eigentliche Sensor „DIMM #n“ (die übrigen Einträge sind Grenzwerte wie 55/85 °C)
        foreach (var dimm in _dimms)
        {
            try { dimm.Update(); } catch { continue; }
            var t = dimm.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature
                                                     && x.Name.StartsWith("DIMM", StringComparison.OrdinalIgnoreCase)
                                                     && IsPlausible(x.Value));
            if (t is not null) s.ExtraTemps.Add(new TempReading("RAM", $"{t.Name} · {ShortDimmName(dimm.Name)}", t.Value!.Value));
        }

        FillBoard(s);
        FillStorage(s);
    }

    /// <summary>
    /// Erkennt, ob der Mainboard-Chip (Super-I/O) gerade nur „FF“ liefert: alle Spannungen auf dem Höchstwert
    /// (z. B. 2,04 V / 4,08 V), alle Lüfterregler auf 100 % und alle Lüfter auf 0 U/min. Das passiert, wenn
    /// die Chip-Adresse beim Start falsch erkannt wurde oder ein anderes Programm gleichzeitig zugreift.
    /// Echte Werte sehen nie so aus.
    /// </summary>
    internal static bool LooksLikeInvalidSuperIo(IReadOnlyList<(SensorType Type, float? Value)> sensors)
    {
        var volts = sensors.Where(x => x.Type == SensorType.Voltage && x.Value.HasValue).Select(x => x.Value!.Value).ToList();
        var controls = sensors.Where(x => x.Type == SensorType.Control && x.Value.HasValue).Select(x => x.Value!.Value).ToList();
        var fans = sensors.Where(x => x.Type == SensorType.Fan).ToList();
        if (volts.Count < 3 || controls.Count == 0 || fans.Count == 0) return false;

        // Höchstwert eines 8-Bit-Registers: 255 × Schrittweite (8 mV bzw. 16 mV bei geteilten Eingängen)
        static bool Saturated(float v) => Math.Abs(v - 2.04f) < 0.005f || Math.Abs(v - 4.08f) < 0.005f;
        return volts.Count(Saturated) >= volts.Count * 0.8
               && controls.All(c => c >= 99.9f)
               && fans.All(f => f.Value is null or 0);
    }

    /// <summary>Mainboard: Lüfter, Lüftersteuerung, Temperaturen.</summary>
    private void FillBoard(HardwareSnapshot s)
    {
        if (_board is null) return;
        UpdateRecursive(_board);
        var sensors = AllSensors(_board).ToList();

        if (LooksLikeInvalidSuperIo(sensors.Select(x => (x.SensorType, x.Value)).ToList()))
        {
            // Keine Fantasiewerte anzeigen und die Lüftersteuerung nicht blind schreiben lassen –
            // die Lüfter regelt in dieser Zeit das BIOS. Nach 5 Fehlmessungen den Chip neu erkennen.
            _badBoardReads++;
            Status = _baseStatus + " · Mainboard-Sensoren: Lesefehler";
            if (!_boardInvalidLogged)
            {
                Log.Warn("Mainboard-Chip liefert ungültige Werte (alle Register FF) – Lüfter ausgeblendet, Lüftersteuerung pausiert.");
                _boardInvalidLogged = true;
            }
            if (_badBoardReads >= 5 && DateTime.UtcNow - _lastBoardReconnect > TimeSpan.FromMinutes(1)) ReconnectBoard();
            return;
        }
        if (_badBoardReads > 0)
        {
            Log.Info($"Mainboard-Sensoren liefern wieder gültige Werte (nach {_badBoardReads} Fehlmessungen).");
            _badBoardReads = 0;
            _boardInvalidLogged = false;
            Status = _baseStatus;
        }

        foreach (var f in sensors.Where(x => x.SensorType == SensorType.Fan && x.Value is not null))
            s.Fans.Add(new FanReading(f.Identifier.ToString(), f.Name, f.Value!.Value));

        // Unbelegte Mainboard-Eingänge zeigen oft Fantasiewerte (z. B. 3 °C, 6 °C) → erst ab 10 °C
        foreach (var t in sensors.Where(x => x.SensorType == SensorType.Temperature && x.Value is >= 10 and < 125))
            s.ExtraTemps.Add(new TempReading("Mainboard", t.Name, t.Value!.Value));

        // Zuordnung CPU-/Gehäuselüfter: Einstellung, sonst automatisch
        var active = s.Fans.Where(f => f.Rpm > 0).ToList();
        var cpuFan = s.Fans.FirstOrDefault(f => f.Id == settings.CpuFanId)
                     ?? active.FirstOrDefault(f => f.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                     ?? active.FirstOrDefault(f => f.Name == "Fan #2") // bei ASUS/Nuvoton meist CPU_FAN
                     ?? active.FirstOrDefault();
        var caseFan = s.Fans.FirstOrDefault(f => f.Id == settings.CaseFanId)
                      ?? active.FirstOrDefault(f => f != cpuFan);
        s.CpuFanRpm ??= cpuFan?.Rpm;
        s.CaseFanRpm ??= caseFan?.Rpm;

        // Lüftersteuerung (erst nach den Temperaturen, die kommen aus CPU/GPU oben)
        var controls = sensors.Where(x => x.SensorType == SensorType.Control && x.Control is not null).ToList();
        FanControl.Apply(controls, s);
        foreach (var c in controls)
        {
            var id = c.Identifier.ToString();
            s.FanControls.Add(new FanControlReading(id, c.Name, c.Value, FanControl.IsControlled(id)));
        }
    }

    /// <summary>Mainboard-Chip neu erkennen (Adresse neu einlesen) – wie ein Neustart nur für diesen Teil.</summary>
    private void ReconnectBoard()
    {
        if (_computer is null) return;
        _lastBoardReconnect = DateTime.UtcNow;
        Log.Info("Mainboard-Sensoren werden neu verbunden …");
        try
        {
            FanControl.ForgetApplied(); // die neuen Regler-Objekte starten auf BIOS-Steuerung
            _computer.IsMotherboardEnabled = false;
            _computer.IsMotherboardEnabled = true;
            _board = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
            if (_board is not null) UpdateRecursive(_board);
            Log.Info($"Mainboard neu verbunden: {_board?.Name ?? "nicht gefunden"}");
        }
        catch (Exception ex)
        {
            Log.Warn($"Mainboard neu verbinden: {ex.Message}");
        }
    }

    /// <summary>SSD/HDD-Temperaturen (SMART). Nur alle 10 Messungen – SMART-Abfragen sind vergleichsweise langsam.</summary>
    private List<TempReading> _lastStorageTemps = new();
    private List<DriveHealth> _lastHealth = new();
    private void FillStorage(HardwareSnapshot s)
    {
        if (_storage.Count == 0) return;
        if (_storageTick++ % 10 == 0)
        {
            var temps = new List<TempReading>();
            var health = new List<DriveHealth>();
            foreach (var d in _storage)
            {
                try { d.Update(); } catch { continue; }
                var t = d.Sensors.Where(x => x.SensorType == SensorType.Temperature && IsPlausible(x.Value))
                                 .OrderBy(x => x.Index).FirstOrDefault();
                if (t is not null) temps.Add(new TempReading("SSD", d.Name, t.Value!.Value));

                // SMART: Lebensdauer, geschriebene Daten (LHM meldet GB), Betriebsstunden
                double? life = FindValue(d, SensorType.Level, "Life", "Remaining Life");
                if (life is null && FindValue(d, SensorType.Level, "Percentage Used") is { } used) life = 100 - used;
                health.Add(new DriveHealth(d.Name, life,
                    FindValue(d, SensorType.Data, "Data Written", "Total Bytes Written"),
                    FindValue(d, SensorType.Factor, "Power On Hours"),
                    t?.Value));
            }
            _lastStorageTemps = temps;
            _lastHealth = health;
        }
        s.ExtraTemps.AddRange(_lastStorageTemps);
        s.DriveHealth = _lastHealth;
    }

    /// <summary>„G Skill Intl - F5-6000J3238F16G (#1)“ → „G Skill F5-6000J3238F16G“</summary>
    private static string ShortDimmName(string name)
    {
        var n = name.Replace(" Intl", "").Replace(" - ", " ");
        var hash = n.IndexOf(" (#", StringComparison.Ordinal);
        return hash > 0 ? n[..hash] : n;
    }

    /// <summary>Unbelegte Sensoreingänge melden oft 0, -128 oder 127 °C.</summary>
    private static bool IsPlausible(float? v) => v is > 1 and < 125;

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

    private static bool IsEffective(ISensor s) => s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase);

    /// <summary>„Core #3 (Effective)“ → 3</summary>
    private static int CoreNumber(string name)
    {
        var digits = new string(name.SkipWhile(c => c != '#').Skip(1).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

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
        // Lüfter zuerst zurück ans Mainboard geben
        try
        {
            if (_board is not null)
                FanControl.ReleaseAll(AllSensors(_board).Where(x => x.SensorType == SensorType.Control));
        }
        catch (Exception ex) { Log.Warn($"Lüfter zurücksetzen: {ex.Message}"); }

        try { _computer?.Close(); } // entlädt Treiber/Handles sauber
        catch (Exception ex) { Log.Warn($"LHM Close: {ex.Message}"); }
        _computer = null;
        IsAvailable = false;
    }
}

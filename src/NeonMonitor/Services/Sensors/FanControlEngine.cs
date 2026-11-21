using LibreHardwareMonitor.Hardware;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Sensors;

/// <summary>Einstellung für einen Lüfterkanal.</summary>
public sealed class FanCurveConfig
{
    /// <summary>LHM-Kennung des Steuerkanals, z. B. „/lpc/nct6799d/0/control/1“.</summary>
    public string ControlId { get; set; } = "";
    /// <summary>„bios“ = Mainboard regelt, „fixed“ = feste Leistung, „curve“ = Lüfterkurve.</summary>
    public string Mode { get; set; } = "bios";
    public double FixedPercent { get; set; } = 50;
    /// <summary>„quiet“, „balanced“ oder „performance“.</summary>
    public string Preset { get; set; } = "balanced";
    /// <summary>Bezugstemperatur: „cpu“ oder „max“ (höherer Wert von CPU und GPU – gut für Gehäuselüfter).</summary>
    public string Source { get; set; } = "cpu";

    public FanCurveConfig Clone() => (FanCurveConfig)MemberwiseClone();
}

/// <summary>
/// Regelt Mainboard-Lüfter über LibreHardwareMonitor – mit Sicherheitsnetz:
/// <list type="bullet">
/// <item>nie unter <see cref="HardMinimumPercent"/> % (Lüfter bleiben nie stehen),</item>
/// <item>Notfall: CPU ≥ 90 °C, Hotspot ≥ 105 °C oder fehlender Temperaturwert → 100 %,</item>
/// <item>nach unten nur langsam (kein Auf und Ab), nach oben sofort,</item>
/// <item>beim Ausschalten, bei „BIOS“ und beim Beenden geht die Steuerung zurück ans Mainboard.</item>
/// </list>
/// Wird ausschließlich vom Mess-Thread aufgerufen.
/// </summary>
public sealed class FanControlEngine
{
    public const double HardMinimumPercent = 20;
    private const double RampDownPerTick = 3;

    /// <summary>Lüfterkurven (Temperatur °C → Leistung %).</summary>
    public static readonly IReadOnlyDictionary<string, (double Temp, double Percent)[]> Presets =
        new Dictionary<string, (double, double)[]>
        {
            ["quiet"] = [(40, 25), (60, 35), (75, 55), (85, 100)],
            ["balanced"] = [(40, 30), (60, 45), (75, 70), (85, 100)],
            ["performance"] = [(40, 45), (60, 65), (70, 85), (80, 100)],
        };

    private readonly Dictionary<string, double> _applied = new(); // von uns gesetzte Werte
    private volatile bool _enabled;
    private volatile IReadOnlyList<FanCurveConfig> _configs = [];

    /// <summary>Hauptschalter (aus = alle Kanäle zurück ans BIOS).</summary>
    public bool Enabled { get => _enabled; set => _enabled = value; }

    /// <summary>Neue Einstellungen übernehmen (Liste wird als Ganzes ersetzt → threadsicher).</summary>
    public void SetConfigs(IEnumerable<FanCurveConfig> configs) => _configs = configs.Select(c => c.Clone()).ToList();

    /// <summary>Einmal pro Messung: Zielwerte berechnen und setzen.</summary>
    public void Apply(IReadOnlyList<ISensor> controls, HardwareSnapshot s)
    {
        foreach (var sensor in controls)
        {
            var control = sensor.Control;
            if (control is null) continue;
            var id = sensor.Identifier.ToString();
            var config = _enabled ? _configs.FirstOrDefault(c => c.ControlId == id) : null;

            if (config is null || config.Mode == "bios")
            {
                Release(control, id);
                continue;
            }

            var target = Target(config, s);
            if (_applied.TryGetValue(id, out var last) && target < last)
                target = Math.Max(target, last - RampDownPerTick); // langsam leiser werden

            if (!_applied.TryGetValue(id, out var prev) || Math.Abs(prev - target) >= 0.5)
            {
                try
                {
                    control.SetSoftware((float)target);
                    _applied[id] = target;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Lüfter {id}: {ex.Message}");
                }
            }
        }
    }

    public bool IsControlled(string id) => _applied.ContainsKey(id);

    /// <summary>Gesetzte Werte vergessen (nach dem Neuverbinden des Mainboards gibt es neue Regler-Objekte).</summary>
    public void ForgetApplied() => _applied.Clear();

    /// <summary>Zielleistung in % inkl. Sicherheitsregeln.</summary>
    public static double Target(FanCurveConfig c, HardwareSnapshot s)
    {
        // Notfall hat immer Vorrang
        if (s.CpuTemp >= 90 || s.GpuHotspot >= 105) return 100;

        double target;
        if (c.Mode == "fixed")
        {
            target = c.FixedPercent;
        }
        else
        {
            double? temp = c.Source == "max" ? Max(s.CpuTemp, s.GpuTemp) : s.CpuTemp;
            if (temp is null) return 100; // ohne Temperatur lieber zu laut als zu heiß
            target = Interpolate(Presets.TryGetValue(c.Preset, out var p) ? p : Presets["balanced"], temp.Value);
        }
        return Math.Clamp(target, HardMinimumPercent, 100);
    }

    public static double Interpolate((double Temp, double Percent)[] curve, double temp)
    {
        if (temp <= curve[0].Temp) return curve[0].Percent;
        for (var i = 1; i < curve.Length; i++)
        {
            if (temp <= curve[i].Temp)
            {
                var (t0, p0) = curve[i - 1];
                var (t1, p1) = curve[i];
                return p0 + (p1 - p0) * (temp - t0) / (t1 - t0);
            }
        }
        return curve[^1].Percent;
    }

    private static double? Max(double? a, double? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

    private void Release(IControl control, string id)
    {
        if (!_applied.Remove(id)) return;
        try { control.SetDefault(); }
        catch (Exception ex) { Log.Warn($"Lüfter {id} zurücksetzen: {ex.Message}"); }
    }

    /// <summary>Alle Kanäle zurück ans Mainboard (beim Beenden).</summary>
    public void ReleaseAll(IEnumerable<ISensor> controls)
    {
        foreach (var sensor in controls)
            if (sensor.Control is { } c) Release(c, sensor.Identifier.ToString());
    }
}

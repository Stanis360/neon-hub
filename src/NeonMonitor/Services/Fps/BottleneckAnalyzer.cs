using NeonMonitor.Models;

namespace NeonMonitor.Services.Fps;

/// <summary>
/// Schätzt, was die FPS gerade begrenzt. Faustregeln (wie bei CapFrameX/Afterburner-Auswertungen üblich):
/// <list type="bullet">
/// <item><b>GPU</b>: Grafikkarte ≥ 94 % ausgelastet → mehr FPS nur über Grafik-Einstellungen/Upscaling.</item>
/// <item><b>CPU</b>: Grafikkarte nicht voll, aber ein CPU-Thread ≥ 85 % → das Spiel wartet auf die CPU.</item>
/// <item><b>Limit</b>: weder GPU noch CPU voll → FPS-Grenze, V-Sync oder die Spiel-Engine bremst.</item>
/// <item><b>Ausgeglichen</b>: GPU zwischen 85 und 94 % – beides ungefähr gleich stark gefordert.</item>
/// </list>
/// Die CPU-Gesamtlast taugt dafür nicht: Ein Spiel kann einen Thread voll auslasten, während der Rest der
/// 16 Threads nichts tut – deshalb zählt die Last des am stärksten belasteten Threads.
/// </summary>
public static class BottleneckAnalyzer
{
    public const string Gpu = "GPU";
    public const string Cpu = "CPU";
    public const string Limit = "Limit";
    public const string Balanced = "Ausgeglichen";

    public static string? Classify(HardwareSnapshot s)
    {
        if (s.Fps is null || s.GpuLoad is not { } gpu) return null;
        if (gpu >= 94) return Gpu;
        if (s.CpuCoreMaxLoad >= 85) return Cpu;
        if (gpu < 85) return Limit;
        return Balanced;
    }

    /// <summary>Langtext für die Oberfläche.</summary>
    public static string Describe(string? b) => b switch
    {
        Gpu => "GPU-Limit – die Grafikkarte ist voll ausgelastet",
        Cpu => "CPU-Limit – das Spiel wartet auf den Prozessor",
        Limit => "Begrenzt – FPS-Grenze, V-Sync oder Spiel-Engine",
        Balanced => "Ausgeglichen – CPU und GPU ähnlich gefordert",
        _ => "",
    };

    /// <summary>Praktischer Tipp passend zum Engpass.</summary>
    public static string Tip(string? b) => b switch
    {
        Gpu => "Mehr FPS gibt es über Grafik-Einstellungen (Schatten, Raytracing) oder FSR. Eine höhere Auflösung kostet direkt FPS.",
        Cpu => "Grafik-Einstellungen bringen kaum FPS – eher Sichtweite/Physik/NPC-Dichte senken. Eine höhere Auflösung ist fast „kostenlos“.",
        Limit => "Die FPS werden absichtlich begrenzt (z. B. deine 237-FPS-Grenze) – CPU und GPU haben Reserven. Das ist gut für Temperatur und Strom.",
        Balanced => "System ist gut ausbalanciert.",
        _ => "",
    };
}

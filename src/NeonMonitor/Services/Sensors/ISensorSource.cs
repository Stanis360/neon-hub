using System.Text;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Sensors;

/// <summary>
/// Eine Quelle für Messwerte. Quellen werden der Reihe nach abgefragt;
/// jede füllt nur Werte, die im Snapshot noch fehlen (<c>null</c>).
/// <para>Alle Methoden werden ausschließlich vom Mess-Thread aufgerufen.</para>
/// </summary>
public interface ISensorSource : IDisposable
{
    /// <summary>Anzeigename, z. B. „LibreHardwareMonitor“.</summary>
    string Name { get; }

    /// <summary>Wurde die Quelle erfolgreich initialisiert?</summary>
    bool IsAvailable { get; }

    /// <summary>Kurzer Status für die Statusleiste.</summary>
    string Status { get; }

    /// <summary>Einmalige (ggf. langsame) Initialisierung.</summary>
    void Initialize();

    /// <summary>Liest aktuelle Werte und trägt fehlende Werte in <paramref name="snapshot"/> ein.</summary>
    void Fill(HardwareSnapshot snapshot);

    /// <summary>Schreibt alle bekannten Rohsensoren in den Diagnosebericht.</summary>
    void AppendReport(StringBuilder report);
}

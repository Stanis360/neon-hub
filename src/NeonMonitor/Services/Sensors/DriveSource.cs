using System.Text;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Sensors;

/// <summary>Belegung aller festen Laufwerke (SSD/HDD-Partitionen) über <see cref="DriveInfo"/>.</summary>
public sealed class DriveSource : ISensorSource
{
    private const double BytesPerGb = 1024d * 1024 * 1024;

    public string Name => "Laufwerke";
    public bool IsAvailable => true;
    public string Status => "OK";

    public void Initialize() { }

    public void Fill(HardwareSnapshot s)
    {
        if (s.Drives.Count > 0) return;
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                var total = d.TotalSize / BytesPerGb;
                var used = (d.TotalSize - d.TotalFreeSpace) / BytesPerGb;
                s.Drives.Add(new DriveUsage(d.Name, d.VolumeLabel, used, total));
            }
            catch
            {
                // Laufwerk kann zwischen Prüfung und Abfrage verschwinden (z. B. USB) – ignorieren.
            }
        }
    }

    public void AppendReport(StringBuilder r)
    {
        r.AppendLine($"=== {Name} ===");
        var s = new HardwareSnapshot();
        Fill(s);
        foreach (var d in s.Drives)
            r.AppendLine($"  {d.Name,-5} {d.Label,-20} {d.UsedGb,8:0.0} / {d.TotalGb,8:0.0} GB ({d.Percent:0}%)");
    }

    public void Dispose() { }
}

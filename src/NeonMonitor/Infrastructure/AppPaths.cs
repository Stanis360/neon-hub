namespace NeonMonitor.Infrastructure;

/// <summary>Zentrale Ablageorte für Einstellungen, Logs, Berichte und (später) Aufzeichnungen.</summary>
public static class AppPaths
{
    /// <summary>%AppData%\NeonMonitor – Einstellungen und Log.</summary>
    public static string AppDataDir { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeonMonitor"));

    /// <summary>Dokumente\NeonMonitor – alles, was der Nutzer selbst öffnen soll.</summary>
    public static string DocumentsDir { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NeonMonitor"));

    public static string ReportsDir => Ensure(Path.Combine(DocumentsDir, "Berichte"));
    public static string SessionsDir => Ensure(Path.Combine(DocumentsDir, "Sitzungen"));

    public static string SettingsFile => Path.Combine(AppDataDir, "settings.json");
    public static string LogFile => Path.Combine(AppDataDir, "neonmonitor.log");

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}

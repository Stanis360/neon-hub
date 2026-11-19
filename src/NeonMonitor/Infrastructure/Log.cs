namespace NeonMonitor.Infrastructure;

/// <summary>
/// Minimaler, threadsicherer Datei-Logger. Bewusst ohne Abhängigkeiten –
/// für Fehlersuche bei Sensorproblemen reicht eine Textdatei.
/// </summary>
public static class Log
{
    private static readonly object Sync = new();
    private const long MaxBytes = 2 * 1024 * 1024; // Log bei 2 MB neu beginnen

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                var file = new FileInfo(AppPaths.LogFile);
                if (file.Exists && file.Length > MaxBytes) file.Delete();
                File.AppendAllText(AppPaths.LogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging darf die App niemals zum Absturz bringen.
        }
    }
}

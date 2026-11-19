using System.Diagnostics;
using System.Text;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Fps;

/// <summary>
/// Startet das PresentMon-Konsolenprogramm (Intel, MIT-Lizenz) als unsichtbaren Kindprozess
/// und liest dessen CSV-Ausgabe zeilenweise mit.
/// <para>
/// PresentMon wertet die Ereignisse aus, die Windows ohnehin für jedes ausgegebene Bild erzeugt
/// (ETW: DXGI, D3D9, DxgKrnl …). Es wird <b>nichts</b> in Spiele eingeschleust – deshalb ist das
/// mit Anti-Cheat-Systemen verträglich.
/// </para>
/// Stürzt PresentMon ab, wird es nach kurzer Pause neu gestartet.
/// </summary>
public sealed class PresentMonRunner : IDisposable
{
    /// <summary>Eigener ETW-Sitzungsname, damit wir anderen PresentMon-Nutzern nicht in die Quere kommen.</summary>
    private const string SessionName = "NeonMonitor_PresentMon";

    private readonly string _exePath;
    private readonly Action<FrameSample> _onFrame;
    private readonly ProcessJob _job = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private Process? _process;

    public PresentMonRunner(string exePath, Action<FrameSample> onFrame)
    {
        _exePath = exePath;
        _onFrame = onFrame;
    }

    /// <summary>Kurzer Zustand für die Statusleiste.</summary>
    public string Status { get; private set; } = "startet …";

    public void Start() => _loop ??= Task.Run(() => RunLoopAsync(_cts.Token));

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(2);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Status = $"Fehler: {ex.Message}";
                Log.Error("PresentMon", ex);
            }
            if (ct.IsCancellationRequested) break;

            // Neustart mit wachsender Pause (max. 30 s), falls PresentMon wiederholt abbricht
            try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_exePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[]
        {
            "--output_stdout",          // CSV an uns statt in eine Datei
            "--no_console_stats",
            "--v1_metrics",             // stabiles, weit verbreitetes Spaltenformat
            "--no_track_gpu",           // brauchen wir nicht → weniger Last
            "--no_track_input",
            "--stop_existing_session",  // Reste einer abgestürzten Sitzung aufräumen
            "--session_name", SessionName,
            "--exclude", "dwm.exe",
            "--exclude", "NeonMonitor.exe",
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("PresentMon konnte nicht gestartet werden");
        _process = process;
        try { _job.Add(process); } catch (Exception ex) { Log.Warn($"Job-Objekt: {ex.Message}"); }

        Status = "läuft";
        Log.Info($"PresentMon gestartet (PID {process.Id})");

        // stderr mitlesen (sonst kann der Puffer volllaufen) und ins Log schreiben
        _ = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
                if (line.Length > 0) Log.Warn($"PresentMon: {line}");
        }, ct);

        await using var reg = ct.Register(() => { try { process.Kill(); } catch { /* schon beendet */ } });

        var parser = new PresentMonCsvParser();
        while (await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (parser.TryParse(line, out var frame)) _onFrame(frame);
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        Status = $"beendet (Code {process.ExitCode}) – Neustart …";
        Log.Warn($"PresentMon beendet mit Code {process.ExitCode}");
        _process = null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _process?.Kill(); } catch { /* egal */ }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* egal */ }

        // ETW-Sitzung sicher beenden (ein „Kill“ lässt sie sonst bis zum nächsten Start offen)
        try
        {
            var psi = new ProcessStartInfo(_exePath) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("--terminate_existing_session");
            psi.ArgumentList.Add("--session_name");
            psi.ArgumentList.Add(SessionName);
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
        }
        catch { /* beim Beenden egal */ }

        _job.Dispose();
        _cts.Dispose();
    }
}

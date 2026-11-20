using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Comfort;

/// <summary>Ein Release auf GitHub.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, string? ZipUrl, string? ZipName, string? ChecksumsUrl);

/// <summary>
/// Sucht auf GitHub nach einer neueren Version und installiert sie auf Wunsch:
/// ZIP laden → SHA-256 gegen <c>SHA256SUMS.txt</c> prüfen → entpacken → <c>Installieren.ps1 -FromUpdate</c> starten.
/// Das Skript beendet NeonMonitor sauber, kopiert die neue Version und startet sie wieder.
/// </summary>
public static class UpdateService
{
    /// <summary>GitHub-Repository mit den Releases.</summary>
    public const string Repository = "Stanis360/neon-hub";
    public static string ReleasesPage => $"https://github.com/{Repository}/releases";

    private static readonly HttpClient Http = CreateClient();

    public static Version CurrentVersion
    {
        get
        {
            var v = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>Läuft die portable Variante (mit eingebautem .NET)?</summary>
    public static bool IsSelfContained => File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"));

    /// <summary>Neuestes Release oder null (keins vorhanden / offline).</summary>
    public static async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct = default)
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;

        string? zipUrl = null, zipName = null, sumsUrl = null;
        var wantPortable = IsSelfContained;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) sumsUrl = url;
            else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                     && name.Contains("portable", StringComparison.OrdinalIgnoreCase) == wantPortable)
            {
                zipUrl = url;
                zipName = name;
            }
        }
        return new ReleaseInfo(version, tag, root.GetProperty("html_url").GetString() ?? ReleasesPage, zipUrl, zipName, sumsUrl);
    }

    /// <summary>
    /// Lädt, prüft und startet die Installation. Danach muss sich NeonMonitor beenden
    /// (das Installationsskript wartet darauf bzw. fordert es über das Exit-Signal an).
    /// </summary>
    public static async Task StartInstallAsync(ReleaseInfo release, IProgress<string> progress, CancellationToken ct = default)
    {
        if (release.ZipUrl is null || release.ZipName is null)
            throw new InvalidOperationException("Im Release fehlt die passende ZIP-Datei.");
        if (release.ChecksumsUrl is null)
            throw new InvalidOperationException("Im Release fehlt SHA256SUMS.txt – Update wird aus Sicherheitsgründen nicht installiert.");

        var work = Path.Combine(Path.GetTempPath(), "NeonMonitor-Update", release.Tag);
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        var zipPath = Path.Combine(work, release.ZipName);

        progress.Report("Lade Update herunter …");
        await using (var stream = await Http.GetStreamAsync(release.ZipUrl, ct))
        await using (var file = File.Create(zipPath))
            await stream.CopyToAsync(file, ct);

        progress.Report("Prüfe Prüfsumme …");
        var sums = await Http.GetStringAsync(release.ChecksumsUrl, ct);
        var expected = sums.Split('\n')
            .Select(l => l.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(p => p.Length == 2 && p[1].Equals(release.ZipName, StringComparison.OrdinalIgnoreCase))?[0];
        string actual;
        await using (var file = File.OpenRead(zipPath))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant();
        if (expected is null || !expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Prüfsumme stimmt nicht – Download beschädigt oder verändert. Update abgebrochen.");

        progress.Report("Entpacke …");
        var extractDir = Path.Combine(work, "files");
        ZipFile.ExtractToDirectory(zipPath, extractDir);
        var script = Directory.EnumerateFiles(extractDir, "Installieren.ps1", SearchOption.AllDirectories).FirstOrDefault()
                     ?? throw new InvalidOperationException("Installieren.ps1 fehlt im Update.");

        progress.Report("Starte Installation …");
        Log.Info($"Update {release.Tag}: starte {script}");
        Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -FromUpdate")
        {
            UseShellExecute = true, // eigenes Konsolenfenster mit Fortschritt
            WorkingDirectory = Path.GetDirectoryName(script)!,
        });
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // GitHub verlangt einen User-Agent; bewusst ohne persönliche Daten
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NeonMonitor", CurrentVersion.ToString()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }
}

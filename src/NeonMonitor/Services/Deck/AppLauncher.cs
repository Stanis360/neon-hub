using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NeonMonitor.Services.Deck;

/// <summary>
/// Startet Launcher und Spiele, ohne dass ein Pfad eingetragen werden muss: der Installationsort wird beim
/// Auslösen auf diesem PC gesucht (Registry, Steam-Bibliotheken, Epic-Manifeste).
/// <para>
/// Epic-Spiele werden über den Epic-Launcher gestartet (com.epicgames.launcher://…) – direkt gestartet würden
/// sie ohne Anmeldung nicht laufen. Steam-Spiele über steam://rungameid/….
/// </para>
/// </summary>
public static class AppLauncher
{
    /// <summary>Wählbare Ziele (Kennung → Anzeigename).</summary>
    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        ["steam"] = "Steam",
        ["operagx"] = "Opera GX",
        ["epic"] = "Epic Games",
        ["satisfactory"] = "Satisfactory",
        ["cod"] = "Call of Duty",
        ["spotify"] = "Spotify",
    };

    // Steam-App-IDs: Satisfactory, Call of Duty HQ (enthält die aktuellen CoD-Teile)
    public const int SatisfactorySteamId = 526870;
    public const int CallOfDutySteamId = 1938090;

    /// <summary>Was beim Auslösen gestartet würde (Pfad oder Adresse) – oder null, wenn nichts gefunden wurde.</summary>
    public static string? Resolve(string id)
    {
        if (!OperatingSystem.IsWindows()) return null;
        return id switch
        {
            "steam" => SteamExe() ?? (HasUriHandler("steam") ? "steam://open/main" : null),
            "operagx" => OperaGx(),
            "epic" => EpicLauncher() ?? (HasUriHandler("com.epicgames.launcher") ? "com.epicgames.launcher://store" : null),
            "satisfactory" => EpicGameUri(EpicManifestDir(), "Satisfactory")
                              ?? SteamGameUri(SteamLibraries(), SatisfactorySteamId),
            "cod" => SteamGameUri(SteamLibraries(), CallOfDutySteamId)
                     ?? BattleNet(),
            // Desktop-Version liegt in %AppData%, die Store-Version ist nur über spotify: erreichbar
            "spotify" => File.Exists(SpotifyExe()) ? SpotifyExe() : HasUriHandler("spotify") ? "spotify:" : null,
            _ => null,
        };
    }

    // ---------------- Steam ----------------

    private static string? SteamDir()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                   ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        path = path?.Replace('/', '\\');
        if (path is not null && Directory.Exists(path)) return path;
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static string? SteamExe()
    {
        var dir = SteamDir();
        var exe = dir is null ? null : Path.Combine(dir, "steam.exe");
        return exe is not null && File.Exists(exe) ? exe : null;
    }

    /// <summary>Alle Steam-Bibliotheksordner (Hauptordner + Einträge aus libraryfolders.vdf).</summary>
    public static List<string> SteamLibraries(string? steamDir = null)
    {
        steamDir ??= SteamDir();
        var result = new List<string>();
        if (steamDir is null) return result;
        result.Add(steamDir);
        var vdf = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (var lib in ParseLibraryFolders(File.ReadAllText(vdf)))
                if (!result.Contains(lib, StringComparer.OrdinalIgnoreCase)) result.Add(lib);
        return result;
    }

    /// <summary>Liest die „path“-Einträge aus libraryfolders.vdf (Backslashes sind dort verdoppelt).</summary>
    public static IEnumerable<string> ParseLibraryFolders(string vdf) =>
        Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"").Select(m => m.Groups[1].Value.Replace(@"\\", @"\"));

    /// <summary>steam://rungameid/… wenn das Spiel in einer der Bibliotheken installiert ist.</summary>
    public static string? SteamGameUri(IEnumerable<string> libraries, int appId) =>
        libraries.Any(l => File.Exists(Path.Combine(l, "steamapps", $"appmanifest_{appId}.acf")))
            ? $"steam://rungameid/{appId}" : null;

    // ---------------- Epic ----------------

    private static string EpicManifestDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

    private static string? EpicLauncher()
    {
        foreach (var pf in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
            foreach (var arch in new[] { "Win64", "Win32" })
            {
                var exe = Path.Combine(Environment.GetFolderPath(pf), "Epic Games", "Launcher", "Portal", "Binaries", arch, "EpicGamesLauncher.exe");
                if (File.Exists(exe)) return exe;
            }
        return null;
    }

    /// <summary>
    /// Sucht in den Epic-Manifesten (*.item, JSON) ein Spiel, dessen Name <paramref name="name"/> enthält,
    /// und baut die Start-Adresse für den Epic-Launcher.
    /// </summary>
    public static string? EpicGameUri(string manifestDir, string name)
    {
        if (!Directory.Exists(manifestDir)) return null;
        foreach (var file in Directory.EnumerateFiles(manifestDir, "*.item"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                var display = Str(root, "DisplayName");
                var app = Str(root, "AppName");
                if (app is null || display is null || !display.Contains(name, StringComparison.OrdinalIgnoreCase)) continue;
                var ns = Str(root, "CatalogNamespace");
                var item = Str(root, "CatalogItemId");
                var target = ns is not null && item is not null ? $"{ns}%3A{item}%3A{app}" : app;
                return $"com.epicgames.launcher://apps/{target}?action=launch&silent=true";
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // beschädigte oder gesperrte Datei überspringen
            }
        }
        return null;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // ---------------- Opera GX, Battle.net, Spotify ----------------

    private static string SpotifyExe() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");

    private static string? OperaGx()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(local, "Programs", "Opera GX", "launcher.exe"),
            Path.Combine(local, "Programs", "Opera GX", "opera.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Opera GX", "launcher.exe"),
        };
        return candidates.FirstOrDefault(File.Exists) ?? FromUninstallEntry("Opera GX", "launcher.exe");
    }

    private static string? BattleNet()
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Battle.net", "Battle.net Launcher.exe");
        return File.Exists(exe) ? exe : null;
    }

    /// <summary>Installationsort aus der Windows-Programmliste („Apps &amp; Features“).</summary>
    private static string? FromUninstallEntry(string displayNameStart, string exeName)
    {
        if (!OperatingSystem.IsWindows()) return null;
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (key is null) continue;
            foreach (var sub in key.GetSubKeyNames())
            {
                using var app = key.OpenSubKey(sub);
                if (app?.GetValue("DisplayName") is not string name || !name.StartsWith(displayNameStart, StringComparison.OrdinalIgnoreCase)) continue;
                if (app.GetValue("InstallLocation") is string dir && File.Exists(Path.Combine(dir, exeName))) return Path.Combine(dir, exeName);
                if (app.GetValue("DisplayIcon") is string icon)
                {
                    var path = icon.Split(',')[0].Trim('"');
                    if (File.Exists(path)) return path;
                }
            }
        }
        return null;
    }

    private static bool HasUriHandler(string scheme)
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var key = Registry.ClassesRoot.OpenSubKey(scheme);
        return key is not null;
    }
}

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;
using NeonMonitor.Services.Deck;
using NeonMonitor.Services.Recording;

namespace NeonMonitor.Services.Comfort;

/// <summary>
/// Kleine Webseite im Heimnetz: Werte live auf dem Handy/Tablet ansehen – ohne App, einfach im Browser.
/// <list type="bullet">
/// <item>Werte nur lesend; Deck-Knöpfe nur, wenn am PC erlaubt – und nur die dort angelegten,</item>
/// <item>nur aus privaten Netzen (192.168.x.x usw.), Anfragen aus dem Internet werden abgewiesen,</item>
/// <item>nur mit zufälligem Schlüssel in der Adresse (<c>?k=…</c>),</item>
/// <item>Windows-Firewall-Regel nur für das Profil „Privat“.</item>
/// </list>
/// </summary>
public sealed class WebDashboardService : IDisposable
{
    private const string FirewallRule = "NeonMonitor Handy-Ansicht";

    private readonly AppSettings _settings;
    private readonly RecordingService _recorder;
    private readonly DeckService _deck;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private volatile string _json = "{}";
    private static readonly string Html = LoadHtml();

    public WebDashboardService(HardwareMonitorService monitor, RecordingService recorder, DeckService deck, AppSettings settings)
    {
        _settings = settings;
        _recorder = recorder;
        _deck = deck;
        monitor.SnapshotReady += (_, s) => _json = BuildJson(s);
        if (string.IsNullOrEmpty(_settings.WebToken)) NewToken();
    }

    public bool IsRunning => _listener?.IsListening == true;
    public string? LastError { get; private set; }

    /// <summary>Adressen, unter denen das Handy die Seite erreicht (eine je Netzwerkadapter).</summary>
    public IReadOnlyList<string> Urls => LocalAddresses()
        .Select(ip => $"http://{ip}:{_settings.WebPort}/?k={_settings.WebToken}").ToList();

    public void NewToken()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // ohne leicht verwechselbare Zeichen
        _settings.WebToken = new string(RandomNumberGenerator.GetItems<char>(alphabet, 10));
        _settings.Save();
    }

    public bool Start()
    {
        Stop();
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_settings.WebPort}/");
            _listener.Start();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            Task.Run(() => AcceptLoopAsync(_listener, ct), ct);
            AddFirewallRule();
            LastError = null;
            Log.Info($"Handy-Ansicht läuft auf Port {_settings.WebPort}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex is HttpListenerException { ErrorCode: 183 or 32 }
                ? $"Port {_settings.WebPort} ist bereits belegt."
                : ex.Message;
            Log.Warn($"Handy-Ansicht: {ex.Message}");
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Close(); } catch { /* egal */ }
        _listener = null;
    }

    /// <summary>Beim Ausschalten auch die Firewall-Regel entfernen.</summary>
    public void Disable()
    {
        Stop();
        RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRule}\"");
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync().ConfigureAwait(false); }
            catch { break; } // beim Stoppen
            _ = Task.Run(() => Handle(ctx), ct);
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            if (!IsPrivate(req.RemoteEndPoint.Address))
            {
                Respond(ctx, 403, "text/plain", "Nur im Heimnetz erreichbar.");
                return;
            }
            if (req.QueryString["k"] != _settings.WebToken)
            {
                Respond(ctx, 403, "text/html; charset=utf-8",
                    "<meta name=viewport content='width=device-width'><body style='background:#15181A;color:#E4EAE8;font-family:sans-serif;padding:24px'>" +
                    "<h3>NeonMonitor</h3><p>Schlüssel fehlt oder ist falsch. Bitte die Adresse bzw. den QR-Code aus NeonMonitor → Einstellungen → Handy-Ansicht verwenden.</p>");
                return;
            }
            switch (req.Url?.AbsolutePath)
            {
                case "/api":
                    Respond(ctx, 200, "application/json; charset=utf-8", _json, noCache: true);
                    break;

                case "/api/deck":
                    // Nur Anzeige-Infos der Knöpfe – was sie tun (Pfade, Tasten) bleibt am PC
                    var deck = new
                    {
                        enabled = _settings.DeckEnabled,
                        buttons = _settings.DeckButtons.Select(b => new { id = b.Id, label = b.Label, icon = b.Icon, color = b.Color, kind = b.Type, value = b.Type == "neon" ? b.Value : null }),
                    };
                    Respond(ctx, 200, "application/json; charset=utf-8", JsonSerializer.Serialize(deck), noCache: true);
                    break;

                case "/api/action":
                    if (req.HttpMethod != "POST")
                    {
                        Respond(ctx, 405, "text/plain", "POST erforderlich");
                        break;
                    }
                    var (ok, message) = _deck.Execute(req.QueryString["id"] ?? "");
                    Respond(ctx, ok ? 200 : 403, "application/json; charset=utf-8",
                        JsonSerializer.Serialize(new { ok, message }), noCache: true);
                    break;
                default:
                    Respond(ctx, 200, "text/html; charset=utf-8", Html);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Handy-Ansicht Anfrage: {ex.Message}");
            try { ctx.Response.Abort(); } catch { /* egal */ }
        }
    }

    private static void Respond(HttpListenerContext ctx, int status, string type, string body, bool noCache = false)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = type;
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (noCache) ctx.Response.Headers["Cache-Control"] = "no-store";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes);
        ctx.Response.Close();
    }

    /// <summary>Nur private/lokale Adressen dürfen zugreifen.</summary>
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC; // fc00::/7
        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254);
    }

    private static IEnumerable<string> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel
                        && !n.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
                        && n.GetIPProperties().GatewayAddresses.Count > 0)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a.Address))
            .Select(a => a.Address.ToString())
            .Distinct();

    private void AddFirewallRule()
    {
        RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRule}\"");
        RunNetsh($"advfirewall firewall add rule name=\"{FirewallRule}\" dir=in action=allow protocol=TCP " +
                 $"localport={_settings.WebPort} profile=private remoteip=localsubnet");
    }

    private static void RunNetsh(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("netsh", args) { UseShellExecute = false, CreateNoWindow = true });
            p?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Log.Warn($"netsh {args}: {ex.Message}");
        }
    }

    private static string LoadHtml()
    {
        using var stream = typeof(WebDashboardService).Assembly.GetManifestResourceStream("NeonMonitor.handy.html");
        if (stream is null) return "<p>handy.html fehlt</p>";
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Werte fürs Handy als JSON (auf dem Mess-Thread erzeugt).</summary>
    private string BuildJson(HardwareSnapshot s)
    {
        static double? R(double? v, int digits = 0) => v is { } d && double.IsFinite(d) ? Math.Round(d, digits) : null;
        var rec = _recorder.GetStatus();
        var data = new
        {
            time = s.Timestamp.ToString("HH:mm:ss"),
            game = s.FpsApplication is null ? null : GameNames.FromApplication(s.FpsApplication),
            fps = R(s.Fps), low1 = R(s.Fps1PercentLow), frametime = R(s.FrameTimeMs, 1),
            latency = R(s.InputLatencyMs, 1), stutters = R(s.StuttersPerMin, 1), bottleneck = s.Bottleneck,
            cpu = new { name = s.CpuName, load = R(s.CpuLoad), temp = R(s.CpuTemp), power = R(s.CpuPower), clock = R(s.CpuClockAvg), fan = R(s.CpuFanRpm) },
            gpu = new { name = s.GpuName, load = R(s.GpuLoad), temp = R(s.GpuTemp), hotspot = R(s.GpuHotspot), memTemp = R(s.GpuMemTemp),
                        power = R(s.GpuPower), clock = R(s.GpuClock), vram = R(s.GpuVramUsedMb / 1024, 1), vramTotal = R(s.GpuVramTotalMb / 1024), fan = R(s.GpuFan) },
            ram = new { load = R(s.RamLoad), used = R(s.RamUsedGb, 1), total = R(s.RamTotalGb, 1) },
            caseFan = R(s.CaseFanRpm),
            net = new { ping = R(s.PingInternetMs), loss = R(s.LossInternetPct, 1), lossRouter = R(s.LossRouterPct, 1), type = s.ConnectionType },
            recording = rec.IsRecording ? rec.Elapsed.ToString(@"mm\:ss") : null,
        };
        return JsonSerializer.Serialize(data);
    }

    public void Dispose() => Stop();
}

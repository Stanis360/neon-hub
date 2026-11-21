using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NeonMonitor.Infrastructure;
using NeonMonitor.Models;

namespace NeonMonitor.Services.Sensors;

/// <summary>
/// Misst die Verbindungsqualität: jede Sekunde ein Ping zum <b>Router</b> und einer zum <b>Internet</b>
/// (1.1.1.1, Cloudflare). Aus den letzten 60 Pings: Paketverlust, Ø Ping und Jitter.
/// <para>
/// Damit lässt sich eingrenzen, woher Paketverlust im Spiel kommt: Verlust schon zum Router →
/// Heimnetz/WLAN; Router sauber, aber Verlust ins Internet → Anschluss/Anbieter. Sind beide sauber,
/// liegt das Problem eher beim Spielserver selbst (den Server-Ping kennt nur das Spiel).
/// </para>
/// </summary>
public sealed partial class NetworkQualitySource : ISensorSource
{
    private const string InternetTarget = "1.1.1.1";
    private const int Window = 60;

    private readonly object _sync = new();
    private readonly Queue<double?> _router = new();   // null = verloren
    private readonly Queue<double?> _internet = new();
    private CancellationTokenSource? _cts;
    private IPAddress? _gateway;
    private string? _connection;
    private double? _wifiSignal;

    public string Name => "Verbindung (Ping)";
    public bool IsAvailable { get; private set; }
    public string Status { get; private set; } = "nicht gestartet";

    public void Initialize()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        Task.Run(() => LoopAsync(ct), ct);
        IsAvailable = true;
        Status = "OK";
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var tick = 0;
        using var pingRouter = new Ping();
        using var pingInternet = new Ping();
        while (!ct.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                if (tick % 30 == 0) DetectAdapter();      // Router/Adapter alle 30 s neu bestimmen
                if (tick % 10 == 0) ReadWifiSignal();      // WLAN-Signal alle 10 s

                var routerTask = _gateway is { } gw ? PingOnce(pingRouter, gw) : Task.FromResult<double?>(null);
                var internetTask = PingOnce(pingInternet, IPAddress.Parse(InternetTarget));
                var results = await Task.WhenAll(routerTask, internetTask).ConfigureAwait(false);

                lock (_sync)
                {
                    if (_gateway is not null) Push(_router, results[0]);
                    Push(_internet, results[1]);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Ping: {ex.Message}");
            }
            tick++;
            var wait = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(started);
            if (wait > TimeSpan.Zero)
            {
                try { await Task.Delay(wait, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private static async Task<double?> PingOnce(Ping ping, IPAddress target)
    {
        try
        {
            var reply = await ping.SendPingAsync(target, 1000).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch
        {
            return null; // z. B. Netzwerk weg – zählt als Verlust
        }
    }

    private static void Push(Queue<double?> q, double? v)
    {
        q.Enqueue(v);
        while (q.Count > Window) q.Dequeue();
    }

    /// <summary>Aktiver Adapter mit Standard-Gateway: LAN oder WLAN, Router-Adresse.</summary>
    private void DetectAdapter()
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Select(n => (Nic: n, Gw: n.GetIPProperties().GatewayAddresses
                .Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any))))
            .FirstOrDefault(x => x.Gw is not null);

        lock (_sync)
        {
            if (!Equals(_gateway, nic.Gw)) _router.Clear();
            _gateway = nic.Gw;
            _connection = nic.Nic is null ? null
                : nic.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "WLAN" : "LAN";
        }
    }

    /// <summary>WLAN-Signalstärke aus „netsh wlan show interfaces“ (Zeile „Signal : 92%“).</summary>
    private void ReadWifiSignal()
    {
        if (_connection != "WLAN")
        {
            _wifiSignal = null;
            return;
        }
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.GetEncoding(850), // Konsolen-Codepage
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            var m = SignalRegex().Match(output);
            _wifiSignal = m.Success ? double.Parse(m.Groups[1].Value) : null;
        }
        catch
        {
            _wifiSignal = null;
        }
    }

    [GeneratedRegex(@"^\s*Signal\s*:\s*(\d+)\s*%", RegexOptions.Multiline)]
    private static partial Regex SignalRegex();

    public void Fill(HardwareSnapshot s)
    {
        lock (_sync)
        {
            s.ConnectionType ??= _connection;
            s.WifiSignalPct ??= _wifiSignal;
            if (_router.Count >= 5)
            {
                s.PingRouterMs ??= Average(_router);
                s.LossRouterPct ??= Loss(_router);
            }
            if (_internet.Count >= 5)
            {
                s.PingInternetMs ??= Average(_internet);
                s.LossInternetPct ??= Loss(_internet);
                s.JitterMs ??= Jitter(_internet);
            }
        }
    }

    private static double? Average(IEnumerable<double?> q)
    {
        var ok = q.Where(v => v is not null).Select(v => v!.Value).ToList();
        return ok.Count > 0 ? ok.Average() : null;
    }

    private static double Loss(IReadOnlyCollection<double?> q) => q.Count == 0 ? 0 : q.Count(v => v is null) * 100.0 / q.Count;

    /// <summary>Mittlere Abweichung aufeinanderfolgender Pings.</summary>
    private static double? Jitter(IEnumerable<double?> q)
    {
        var ok = q.Where(v => v is not null).Select(v => v!.Value).ToList();
        if (ok.Count < 2) return null;
        return ok.Zip(ok.Skip(1), (a, b) => Math.Abs(b - a)).Average();
    }

    public void AppendReport(StringBuilder r)
    {
        r.AppendLine($"=== {Name} ===");
        lock (_sync)
        {
            r.AppendLine($"  Verbindung: {_connection ?? "-"}  Router: {_gateway?.ToString() ?? "-"}  WLAN-Signal: {_wifiSignal?.ToString() ?? "-"}");
            r.AppendLine($"  Router:   Ø {Average(_router):0.0} ms, Verlust {Loss(_router):0.0} % ({_router.Count} Pings)");
            r.AppendLine($"  Internet: Ø {Average(_internet):0.0} ms, Verlust {Loss(_internet):0.0} %, Jitter {Jitter(_internet):0.0} ms ({_internet.Count} Pings an {InternetTarget})");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        IsAvailable = false;
    }
}

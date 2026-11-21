using System.Diagnostics;
using NeonMonitor.Infrastructure;

namespace NeonMonitor.Services.Deck;

/// <summary>
/// Führt Deck-Knöpfe aus. Es werden ausschließlich die am PC angelegten Knöpfe ausgeführt –
/// das Handy schickt nur die Kennung, nie einen Befehl.
/// </summary>
public sealed class DeckService(AppSettings settings)
{
    /// <summary>Medienaktionen → virtuelle Tastencodes.</summary>
    public static readonly IReadOnlyDictionary<string, (string Title, ushort Vk)> MediaActions = new Dictionary<string, (string, ushort)>
    {
        ["playpause"] = ("Play/Pause", 0xB3),
        ["next"] = ("Nächster Titel", 0xB0),
        ["prev"] = ("Vorheriger Titel", 0xB1),
        ["stop"] = ("Stopp", 0xB2),
        ["volup"] = ("Lauter", 0xAF),
        ["voldown"] = ("Leiser", 0xAE),
        ["mute"] = ("Stumm", 0xAD),
    };

    /// <summary>NeonMonitor-eigene Aktionen (Titel für den Editor).</summary>
    public static readonly IReadOnlyDictionary<string, string> NeonActions = new Dictionary<string, string>
    {
        ["record"] = "Aufnahme starten/stoppen",
        ["benchmark"] = "Benchmark starten",
        ["overlay"] = "Overlay ein/aus",
        ["window"] = "NeonMonitor-Fenster zeigen",
    };

    private readonly Queue<DateTime> _recent = new();

    /// <summary>Wird von der App gesetzt: führt eine NeonMonitor-Aktion auf dem UI-Thread aus.</summary>
    public Action<string>? RunNeonAction { get; set; }

    /// <summary>Knopf per Kennung auslösen (vom Handy). Gibt (ok, Meldung) zurück.</summary>
    public (bool Ok, string Message) Execute(string id)
    {
        if (!settings.DeckEnabled) return (false, "Steuerung ist am PC ausgeschaltet (NeonMonitor → Deck).");
        var button = settings.DeckButtons.FirstOrDefault(b => b.Id == id);
        if (button is null) return (false, "Knopf nicht gefunden – Seite neu laden.");

        // Schutz gegen versehentliches Dauerfeuer
        lock (_recent)
        {
            var now = DateTime.Now;
            while (_recent.Count > 0 && now - _recent.Peek() > TimeSpan.FromSeconds(1)) _recent.Dequeue();
            if (_recent.Count >= 8) return (false, "Zu viele Klicks auf einmal.");
            _recent.Enqueue(now);
        }
        return Run(button);
    }

    /// <summary>Knopf direkt ausführen (z. B. „Testen“ im Editor).</summary>
    public (bool Ok, string Message) Run(DeckButton b)
    {
        try
        {
            switch (b.Type)
            {
                case "hotkey":
                    var hk = Hotkey.Parse(b.Value);
                    if (hk is null) return (false, $"Ungültige Tastenkombination „{b.Value}“.");
                    InputSender.SendHotkey(hk.Value.Modifiers, hk.Value.Key);
                    break;

                case "media":
                    if (!MediaActions.TryGetValue(b.Value, out var media)) return (false, "Unbekannte Medienaktion.");
                    InputSender.SendVirtualKey(media.Vk);
                    break;

                case "text":
                    InputSender.SendText(b.Value);
                    break;

                case "program":
                    if (!File.Exists(b.Value) && !Directory.Exists(b.Value)) return (false, $"Nicht gefunden: {b.Value}");
                    StartUnelevated(b.Value);
                    break;

                case "url":
                    if (!Uri.TryCreate(b.Value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                        return (false, "Nur http/https-Adressen.");
                    StartUnelevated(uri.AbsoluteUri);
                    break;

                case "neon":
                    if (!NeonActions.ContainsKey(b.Value)) return (false, "Unbekannte NeonMonitor-Aktion.");
                    RunNeonAction?.Invoke(b.Value);
                    break;

                default:
                    return (false, "Unbekannte Aktion.");
            }
            Log.Info($"Deck: „{b.Label}“ ({b.Type})");
            return (true, b.Label);
        }
        catch (Exception ex)
        {
            Log.Warn($"Deck „{b.Label}“: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Über den Explorer starten: NeonMonitor läuft als Administrator – direkt gestartete Programme würden
    /// das erben (z. B. Discord als Admin). Der Explorer startet sie mit normalen Rechten.
    /// </summary>
    private static void StartUnelevated(string target) =>
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { target }, UseShellExecute = false });
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services.Deck;

namespace NeonMonitor.ViewModels;

/// <summary>Auswahl (Chip) mit Titel und Wert – für Farbe, Medien- und NeonMonitor-Aktionen.</summary>
public sealed record DeckChoice(string Title, string Value);

/// <summary>Ein Knopf im Deck-Editor.</summary>
public sealed class DeckButtonViewModel : ObservableObject
{
    private readonly DeckViewModel _owner;
    private readonly DeckButton _b;

    public DeckButtonViewModel(DeckButton b, DeckViewModel owner)
    {
        _b = b;
        _owner = owner;
        UpCommand = new RelayCommand(() => owner.Move(this, -1));
        DownCommand = new RelayCommand(() => owner.Move(this, +1));
        DeleteCommand = new RelayCommand(() => owner.Remove(this));
        TestCommand = new RelayCommand(() => owner.Test(this));
        BrowseCommand = new RelayCommand(Browse);
        SetColorCommand = new ParamCommand(c => { if (c is not null) Color = c; });
        SetValueCommand = new ParamCommand(v => { if (v is not null) Value = v; });
    }

    public DeckButton Model => _b;

    public string Label { get => _b.Label; set { _b.Label = value; Changed(); } }
    public string Icon { get => _b.Icon; set { _b.Icon = value; Changed(); } }
    public string Color { get => _b.Color; set { _b.Color = value; Changed(); } }

    public string Value
    {
        get => _b.Value;
        set { _b.Value = value; Changed(); OnPropertyChanged(nameof(ValueTitle)); }
    }

    public string Type
    {
        get => _b.Type;
        set
        {
            if (_b.Type == value) return;
            _b.Type = value;
            _b.Value = value switch { "media" => "playpause", "neon" => "record", "power" => "lock", _ => "" };
            Changed();
            foreach (var n in new[] { nameof(Value), nameof(ValueTitle), nameof(IsHotkey), nameof(IsProgram), nameof(IsUrl),
                                      nameof(IsMedia), nameof(IsText), nameof(IsNeon), nameof(IsPower), nameof(ValueHint) })
                OnPropertyChanged(n);
        }
    }

    // Typ als RadioButtons
    public bool IsHotkey { get => Type == "hotkey"; set { if (value) Type = "hotkey"; } }
    public bool IsProgram { get => Type == "program"; set { if (value) Type = "program"; } }
    public bool IsUrl { get => Type == "url"; set { if (value) Type = "url"; } }
    public bool IsMedia { get => Type == "media"; set { if (value) Type = "media"; } }
    public bool IsText { get => Type == "text"; set { if (value) Type = "text"; } }
    public bool IsNeon { get => Type == "neon"; set { if (value) Type = "neon"; } }
    public bool IsPower { get => Type == "power"; set { if (value) Type = "power"; } }

    /// <summary>Lesbarer Name des Werts bei Medien-/NeonMonitor-Aktionen.</summary>
    public string ValueTitle => Type switch
    {
        "media" => DeckService.MediaActions.TryGetValue(Value, out var m) ? m.Title : Value,
        "neon" => DeckService.NeonActions.TryGetValue(Value, out var n) ? n : Value,
        "power" => PowerActions.Titles.TryGetValue(Value, out var pw) ? pw : Value,
        _ => Value,
    };

    public string ValueHint => Type switch
    {
        "hotkey" => "Ins Feld klicken und die Kombination drücken (Rücktaste = löschen).",
        "program" => "Programm, Datei oder Ordner – wird mit normalen Rechten gestartet.",
        "url" => "z. B. https://www.twitch.tv",
        "text" => "Wird dort eingetippt, wo gerade der Cursor ist.",
        "power" => $"Herunterfahren/Neustart mit {PowerActions.DelaySeconds} s Vorwarnung; Herunterfahren, Neustart und Standby fragen auf dem Handy nach.",
        _ => "",
    };

    public ICommand UpCommand { get; }
    public ICommand DownCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand SetColorCommand { get; }
    public ICommand SetValueCommand { get; }

    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Programm oder Datei wählen",
            Filter = "Programme und Verknüpfungen|*.exe;*.lnk;*.url;*.bat|Alle Dateien|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            Value = dlg.FileName;
            if (Label is "Neu" or "") Label = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        _owner.Save();
    }
}

/// <summary>Reiter „Deck“: Knöpfe für das Handy anlegen und die Steuerung erlauben.</summary>
public sealed class DeckViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly DeckService _deck;
    private readonly WebViewModel _web;
    private string _status = "";
    private bool _loading;

    public static IReadOnlyList<DeckChoice> Colors { get; } =
    [
        new("Grün", "#3DDC97"), new("Blau", "#5AA9E6"), new("Gelb", "#E8B44C"),
        new("Lila", "#C77DFF"), new("Rot", "#E5626F"), new("Grau", "#4A5459"),
    ];
    public static IReadOnlyList<DeckChoice> MediaChoices { get; } =
        DeckService.MediaActions.Select(kv => new DeckChoice(kv.Value.Title, kv.Key)).ToList();
    public static IReadOnlyList<DeckChoice> PowerChoices { get; } =
        PowerActions.Titles.Select(kv => new DeckChoice(kv.Value, kv.Key)).ToList();
    public static IReadOnlyList<DeckChoice> NeonChoices { get; } =
        DeckService.NeonActions.Select(kv => new DeckChoice(kv.Value, kv.Key)).ToList();

    public DeckViewModel(AppSettings settings, DeckService deck, WebViewModel web)
    {
        _settings = settings;
        _deck = deck;
        _web = web;
        _loading = true;
        foreach (var b in settings.DeckButtons) Buttons.Add(new DeckButtonViewModel(b.Clone(), this));
        _loading = false;

        AddCommand = new RelayCommand(() =>
        {
            Buttons.Add(new DeckButtonViewModel(new DeckButton(), this));
            Save();
        });
        AddPowerCommand = new RelayCommand(() =>
        {
            foreach (var b in DeckButton.PowerDefaults()) Buttons.Add(new DeckButtonViewModel(b, this));
            Save();
            Status = "Energie-Knöpfe hinzugefügt (unten in der Liste – mit ↑ nach vorne holen).";
        });
        PrepareWolCommand = new RelayCommand(PrepareWol);
        RefreshWolCommand = new RelayCommand(RefreshWol);
        RefreshWol();

        ResetCommand = new RelayCommand(() =>
        {
            if (MessageBox.Show("Alle Knöpfe durch die Standardknöpfe ersetzen?", "Deck",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _loading = true;
            Buttons.Clear();
            foreach (var b in DeckButton.Defaults()) Buttons.Add(new DeckButtonViewModel(b, this));
            _loading = false;
            Save();
        });
    }

    public ObservableCollection<DeckButtonViewModel> Buttons { get; } = new();
    public ICommand AddCommand { get; }
    public ICommand AddPowerCommand { get; }
    public ICommand PrepareWolCommand { get; }
    public ICommand RefreshWolCommand { get; }

    // ---------------- Wake-on-LAN ----------------

    private string _wolText = "", _wolResult = "";
    private bool _wolWifiOnly;

    /// <summary>Adapter, MAC- und IP-Adresse für die WoL-App.</summary>
    public string WolText { get => _wolText; private set => SetProperty(ref _wolText, value); }
    public string WolResult { get => _wolResult; private set => SetProperty(ref _wolResult, value); }
    /// <summary>PC hängt nur per WLAN im Netz – Wake-on-LAN geht dann meist nicht.</summary>
    public bool WolWifiOnly { get => _wolWifiOnly; private set => SetProperty(ref _wolWifiOnly, value); }

    private void RefreshWol()
    {
        try
        {
            var adapters = WakeOnLan.Adapters();
            WolWifiOnly = adapters.Count > 0 && adapters.All(a => a.IsWifi);
            WolText = adapters.Count == 0
                ? "Kein aktiver Netzwerkadapter gefunden."
                : string.Join(Environment.NewLine, adapters.Select(a =>
                    $"{(a.IsWifi ? "WLAN" : "LAN")}: {a.Description}{Environment.NewLine}   MAC-Adresse: {a.Mac}   ·   IP: {a.Ip}"))
                  + Environment.NewLine + $"Windows-Schnellstart: {(WakeOnLan.IsFastStartupOn() ? "AN (sollte für WoL aus sein)" : "aus ✓")}";
        }
        catch (Exception ex)
        {
            WolText = ex.Message;
        }
    }

    private async void PrepareWol()
    {
        if (MessageBox.Show("Windows für Wake-on-LAN vorbereiten?\n\n• Netzwerkkarte (LAN): Wecken per „Magic Packet“ erlauben\n• Windows-Schnellstart ausschalten " +
                            "(der PC startet dann ein paar Sekunden langsamer, lässt sich aber aus „Aus“ wecken)",
                            "Wake-on-LAN", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        WolResult = "Wird eingerichtet …";
        try
        {
            WolResult = await Task.Run(WakeOnLan.PrepareWindows);
        }
        catch (Exception ex)
        {
            WolResult = $"Fehler: {ex.Message}";
        }
        RefreshWol();
    }
    public ICommand ResetCommand { get; }
    public WebViewModel Web => _web;

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Darf das Handy Knöpfe auslösen? (Handy-Ansicht muss dafür ebenfalls an sein.)</summary>
    public bool Enabled
    {
        get => _settings.DeckEnabled;
        set
        {
            _settings.DeckEnabled = value;
            _settings.Save();
            OnPropertyChanged();
            if (value && !_web.Enabled) _web.Enabled = true; // ohne Webseite kein Deck
        }
    }

    /// <summary>Speichert die Knöpfe (die Liste wird als Ganzes ersetzt – der Webserver liest sie parallel).</summary>
    public void Save()
    {
        if (_loading) return;
        _settings.DeckButtons = Buttons.Select(b => b.Model.Clone()).ToList();
        _settings.Save();
    }

    public void Move(DeckButtonViewModel b, int delta)
    {
        var i = Buttons.IndexOf(b);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Buttons.Count) return;
        Buttons.Move(i, j);
        Save();
    }

    public void Remove(DeckButtonViewModel b)
    {
        Buttons.Remove(b);
        Save();
    }

    /// <summary>Knopf am PC ausprobieren (kurze Verzögerung, damit z. B. ein anderes Fenster vorne sein kann).</summary>
    public async void Test(DeckButtonViewModel b)
    {
        Status = $"„{b.Label}“ wird in 2 Sekunden ausgelöst …";
        await Task.Delay(2000);
        var (ok, msg) = _deck.Run(b.Model);
        Status = ok ? $"✓ „{b.Label}“ ausgelöst." : $"Fehler: {msg}";
    }
}

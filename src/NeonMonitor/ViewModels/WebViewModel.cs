using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NeonMonitor.Infrastructure;
using NeonMonitor.Services.Comfort;
using QRCoder;

namespace NeonMonitor.ViewModels;

/// <summary>Einstellungen der Handy-Ansicht: ein/aus, Adresse, QR-Code, neuer Schlüssel.</summary>
public sealed class WebViewModel : ObservableObject
{
    private readonly WebDashboardService _web;
    private readonly AppSettings _settings;
    private string _status = "";
    private BitmapSource? _qr;

    public WebViewModel(WebDashboardService web, AppSettings settings)
    {
        _web = web;
        _settings = settings;
        NewKeyCommand = new RelayCommand(() =>
        {
            _web.NewToken();
            if (_web.IsRunning) _web.Start();
            Refresh();
        });
        CopyCommand = new RelayCommand(() =>
        {
            if (Url is { } u) Clipboard.SetText(u);
            Status = "Adresse kopiert.";
        });
        OpenCommand = new RelayCommand(() => { if (Url is { } u) Shell.Open(u); });

        if (_settings.WebEnabled && !_web.Start()) Status = _web.LastError ?? "";
        Refresh();
    }

    public ICommand NewKeyCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand OpenCommand { get; }

    public bool Enabled
    {
        get => _settings.WebEnabled;
        set
        {
            if (value == _settings.WebEnabled) return;
            _settings.WebEnabled = value;
            _settings.Save();
            if (value)
            {
                if (!_web.Start()) Status = $"Konnte nicht starten: {_web.LastError}";
            }
            else
            {
                _web.Disable();
            }
            OnPropertyChanged();
            Refresh();
        }
    }

    /// <summary>Erste Adresse (für QR-Code und Kopieren).</summary>
    public string? Url => _web.Urls.FirstOrDefault();

    /// <summary>Alle Adressen (falls der PC in mehreren Netzen ist).</summary>
    public string UrlsText => string.Join(Environment.NewLine, _web.Urls);

    public BitmapSource? QrCode { get => _qr; private set => SetProperty(ref _qr, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(UrlsText));
        if (_settings.WebEnabled && _web.IsRunning)
        {
            Status = Url is null ? "Läuft – aber keine Heimnetz-Adresse gefunden." : "Läuft. QR-Code mit der Handy-Kamera scannen.";
            QrCode = Url is null ? null : CreateQr(Url);
        }
        else
        {
            QrCode = null;
            if (!_settings.WebEnabled) Status = "Aus.";
        }
    }

    private static BitmapSource CreateQr(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8, [0xE4, 0xEA, 0xE8], [0x15, 0x18, 0x1A]); // hell auf dunkel
        var image = new BitmapImage();
        using var ms = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

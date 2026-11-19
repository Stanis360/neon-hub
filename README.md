# NeonMonitor

Hardware-Überwachung für Windows 11 mit Neon-Oberfläche: Live-Werte, Verlaufsdiagramme,
später FPS (per ETW, ohne Injection), Overlay, Aufzeichnung als CSV und Sitzungsvergleich.

Gebaut mit **C# / .NET 10 / WPF**. Sensoren über **LibreHardwareMonitor**, ergänzt durch
Windows-Leistungsindikatoren als Fallback.

> Stand: **Schritt 1 von 5** – Projekt-Setup und Live-Anzeige. Die Reiter für FPS, Overlay,
> Aufzeichnung und Vergleich sind vorbereitet und werden in den nächsten Schritten gefüllt.

---

## 1. Einrichtung (einmalig)

Alle Befehle in **PowerShell** oder im **Windows Terminal** ausführen.

### 1.1 .NET 10 SDK installieren

```powershell
winget install Microsoft.DotNet.SDK.10
```

Danach das Terminal **neu öffnen** und prüfen:

```powershell
dotnet --version      # sollte 10.0.x anzeigen
```

> Python wird für das Projekt nicht gebraucht.

### 1.2 PawnIO-Treiber installieren (für CPU-Sensoren)

LibreHardwareMonitor nutzt seit Version 0.9.5 den signierten Treiber **PawnIO** statt des alten
WinRing0 (den Windows Defender inzwischen als unsicher blockiert). Ohne PawnIO fehlen
CPU-Temperatur, -Takt und -Leistung.

```powershell
winget install namazso.PawnIO
```

Falls winget das Paket nicht findet: Installer von <https://pawnio.eu> herunterladen.

### 1.3 AMD-Grafiktreiber aktuell halten

Die GPU-Werte kommen über die AMD-Schnittstelle ADL, die mit **AMD Software: Adrenalin Edition**
installiert wird. Für die RX 9070 XT (RDNA 4) einen aktuellen Treiber verwenden.

### 1.4 Optional: Editor

* **Visual Studio 2026 Community** (kostenlos) mit der Workload „.NET-Desktopentwicklung“ – komfortabelster Weg, mit XAML-Vorschau.
* oder **VS Code** mit der Erweiterung „C# Dev Kit“.

Zum reinen Bauen und Starten reicht das SDK aus Schritt 1.1.

---

## 2. Code holen

```powershell
git clone https://github.com/stanis360/familyhub-password-reset.git
cd familyhub-password-reset
git checkout claude/windows-hardware-monitor-app-rqfxbi
cd NeonMonitor
```

(Alternativ auf GitHub den Branch auswählen → „Code“ → „Download ZIP“.)

---

## 3. Bauen und starten

Das Programm fordert **Adminrechte** an (nötig für die CPU-Sensoren und später für ETW/FPS).
Deshalb das Terminal **als Administrator** öffnen (Rechtsklick auf Windows Terminal →
„Als Administrator ausführen“), sonst bricht `dotnet run` mit „erfordert erhöhte Rechte“ ab.

```powershell
dotnet run --project src\NeonMonitor -c Release
```

### Als eigenständige EXE veröffentlichen

```powershell
dotnet publish src\NeonMonitor -c Release -r win-x64 --self-contained false -o publish
.\publish\NeonMonitor.exe
```

Den Ordner `publish` kannst du beliebig verschieben. Beim Start erscheint die UAC-Abfrage.
Wer die .NET-Runtime nicht separat installieren möchte, nimmt `--self-contained true`
(Ordner wird dann ca. 150 MB groß).

---

## 4. Erster Test: Sensorbericht

Unten rechts auf **„Sensorbericht“** klicken. Es öffnet sich eine Textdatei
(`Dokumente\NeonMonitor\Berichte\…`) mit **allen Rohsensoren**, die LibreHardwareMonitor und
Windows auf deinem System finden, und welcher Sensor welchem Anzeigewert zugeordnet wurde.

➡ **Bitte schick mir den Inhalt dieser Datei.** Damit kann ich prüfen, ob auf deinem
9800X3D und deiner RX 9070 XT wirklich alle Werte ankommen, und die Zuordnung gezielt anpassen
(siehe [docs/SENSOREN.md](docs/SENSOREN.md)).

---

## 5. Bedienung

| Element | Funktion |
|---|---|
| Kacheln | aktueller Wert, darunter Höchstwert seit Start. Gelb/Rot bei hohen Temperaturen. „—“ = Sensor liefert nichts |
| Diagramme | Verlauf der letzten 2 Minuten (Last grün, Temperatur cyan) |
| Animation | Hintergrundanimation an/aus – zum Spielen ggf. ausschalten |
| Intervall | Abfrage alle 0,5 / 1 / 2 Sekunden |
| Max-Werte zurücksetzen | Höchstwerte neu beginnen (z. B. vor einem Benchmark) |
| Datenordner | öffnet `Dokumente\NeonMonitor` |

Einstellungen: `%AppData%\NeonMonitor\settings.json` · Log: `%AppData%\NeonMonitor\neonmonitor.log`

---

## 6. Projektstruktur

```
NeonMonitor/
├─ NeonMonitor.sln
├─ global.json                    # SDK-Version (.NET 10)
├─ docs/SENSOREN.md               # Kompatibilität Zen 5 / RDNA 4, Alternativen (ADLX …)
└─ src/NeonMonitor/
   ├─ NeonMonitor.csproj
   ├─ app.manifest                # fordert Adminrechte an, DPI-Einstellungen
   ├─ App.xaml(.cs)               # Start, Dienste verdrahten, Fehlerbehandlung
   ├─ MainWindow.xaml(.cs)        # Fenster mit eigener Titelleiste, Navigation, Statusleiste
   ├─ Themes/
   │  ├─ Neon.xaml                # Farben, Schriften, Stile aller Steuerelemente
   │  └─ Templates.xaml           # Darstellung von Messwert-Kacheln und Laufwerken
   ├─ Controls/
   │  ├─ NeonBackground.cs        # animierter Hintergrund (Raster, Leuchtwolken, Partikel)
   │  └─ NeonChart.cs             # schlankes Neon-Liniendiagramm
   ├─ Models/
   │  └─ HardwareSnapshot.cs      # eine Messung aller Werte (später = eine CSV-Zeile)
   ├─ Services/
   │  ├─ HardwareMonitorService.cs        # Mess-Thread, fragt alle Quellen ab
   │  └─ Sensors/
   │     ├─ ISensorSource.cs              # Schnittstelle für Sensorquellen
   │     ├─ LibreHardwareSource.cs        # Hauptquelle (CPU/GPU/RAM)
   │     ├─ WindowsCounterSource.cs       # Fallback: Windows-Leistungsindikatoren
   │     ├─ PdhQuery.cs                   # Wrapper für pdh.dll
   │     └─ DriveSource.cs                # Laufwerksbelegung
   ├─ ViewModels/                 # MVVM: MainViewModel, MetricViewModel, ChartSeries …
   ├─ Views/                      # LiveView (Dashboard), Platzhalter für spätere Schritte
   └─ Infrastructure/             # Pfade, Einstellungen, Log, System-Checks
```

### Architektur in einem Satz

`HardwareMonitorService` fragt auf einem eigenen Thread nacheinander alle `ISensorSource`s ab
(LibreHardwareMonitor → Windows-Fallback → Laufwerke); jede Quelle füllt nur die Werte, die
noch fehlen. Der fertige `HardwareSnapshot` geht per Event an die Oberfläche – und ab
Schritt 4 zusätzlich an die CSV-Aufzeichnung. Eine neue Quelle (z. B. ADLX) ist eine
zusätzliche Klasse, ohne den Rest anzufassen.

---

## 7. Fahrplan

1. ✅ Projekt-Setup, Live-Werte, Diagramme, Neon-Theme
2. ⏳ FPS & Frametimes per ETW (PresentMon-Ansatz, keine Injection)
3. ⏳ Overlay (transparentes Always-on-top-Fenster, Werte wählbar)
4. ⏳ Aufzeichnung (Start/Stopp, Hotkey, CSV pro Sitzung)
5. ⏳ Vergleich (Ø FPS, 1 % Lows, Höchsttemperaturen, Diagramme)

## Lizenzen

LibreHardwareMonitorLib steht unter der MPL-2.0.

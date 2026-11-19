# NeonMonitor

Hardware-Überwachung für Windows 11 mit ruhiger, dunkler Oberfläche (Anthrazit + Mintgrün): Live-Werte, Verlaufsdiagramme,
später FPS (per ETW, ohne Injection), Overlay, Aufzeichnung als CSV und Sitzungsvergleich.

Gebaut mit **C# / .NET 10 / WPF**. Sensoren über **LibreHardwareMonitor**, ergänzt durch
Windows-Leistungsindikatoren als Fallback.

> Stand: **Schritt 2 von 5** – Live-Anzeige und FPS/Frametimes. Die Reiter für Overlay,
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

## 2. Installieren – per Doppelklick

1. Die ZIP-Datei entpacken, z. B. nach `C:\Projekte` → es entsteht `C:\Projekte\NeonMonitor`.
2. Im Ordner `NeonMonitor` **doppelt auf `Installieren.bat`** klicken und die Windows-Abfrage
   („Änderungen zulassen?“) mit **Ja** bestätigen.
   Falls Windows „Der Computer wurde durch Windows geschützt“ zeigt: **Weitere Informationen → Trotzdem ausführen**.
3. Das Skript baut das Programm, legt das Symbol **NeonMonitor** auf dem Desktop und im Startmenü an
   und startet es.

**Ab dann nur noch das Desktop-Symbol doppelklicken.**

* **Update** (neue ZIP von mir): entpacken, alten Ordner ersetzen, wieder `Installieren.bat` – Einstellungen bleiben erhalten.
  Ein laufendes NeonMonitor wird dabei automatisch beendet.
* **Entfernen:** `Deinstallieren.bat`.
* Das Programm liegt danach in `%LOCALAPPDATA%\NeonMonitor\App`.
* Startet man NeonMonitor ein zweites Mal, wird einfach das schon offene Fenster nach vorne geholt.

---

## 3. Für Entwickler: direkt aus dem Quellcode starten

Terminal **als Administrator** öffnen (das Programm fordert Adminrechte an), dann:

```powershell
cd C:\Projekte\NeonMonitor
dotnet run --project src\NeonMonitor -c Release
```

Meldung „Datei wird durch NeonMonitor gesperrt“? → Es läuft noch eine Instanz:
`taskkill /IM NeonMonitor.exe /F`, dann erneut starten.

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
| Ringe | außen Last, innen Temperatur (CPU und GPU), Werte in der Mitte |
| Wertezeilen | Wert mit Balken. Bei Temperaturen steht der Höchstwert seit Start daneben („max 81“). Gelb/Rot bei hohen Temperaturen. „—“ = Sensor liefert nichts |
| Kerntakte | effektiver Takt jedes CPU-Kerns (wie Ryzen Master/HWiNFO – im Leerlauf daher niedrig) |
| Diagramme | Verlauf der letzten 2 Minuten (Last grün, Temperatur hellgrau) |
| Animation | Hintergrundanimation an/aus – zum Spielen ggf. ausschalten |
| Intervall | Abfrage alle 0,5 / 1 / 2 Sekunden |
| Max-Werte zurücksetzen | Höchstwerte neu beginnen (z. B. vor einem Benchmark) |
| Datenordner | öffnet `Dokumente\NeonMonitor` |

Einstellungen: `%AppData%\NeonMonitor\settings.json` · Log: `%AppData%\NeonMonitor\neonmonitor.log`

---

## 6. FPS & Frametimes (Reiter „FPS“)

* Gemessen wird mit **PresentMon** (Intel, Open Source). PresentMon liest die Ereignisse mit, die
  Windows für jedes ausgegebene Bild ohnehin erzeugt (ETW). **Es wird nichts in das Spiel
  eingeschleust** – das ist dieselbe Technik, die auch CapFrameX & Co. nutzen.
* PresentMon wird beim Installieren automatisch heruntergeladen (feste Version, Prüfsumme wird kontrolliert)
  und liegt als `Tools\PresentMon.exe` neben NeonMonitor. Es läuft unsichtbar und wird mit NeonMonitor beendet.
* **Automatisch** misst das Spiel im Vordergrund und bleibt dabei, wenn du auf einem anderen Monitor
  klickst (Browser, Discord, Steam usw. werden ignoriert). Über die Knöpfe lässt sich eine Anwendung fest wählen.
* Angezeigt werden: FPS, **1 % Low** (= 1000 / 99. Perzentil der Frametimes der letzten 30 s),
  Frametime Ø und Frametime max (Ruckler), FPS-Verlauf und die letzten 300 Einzel-Frametimes.
* Die FPS stehen zusätzlich im Live-Dashboard unter dem GPU-Ring.

### Anti-Cheat (z. B. Call of Duty / Ricochet)

Die FPS-Messung ist passiv. Für CPU-Temperatur/-Leistung braucht NeonMonitor aber den signierten
Treiber **PawnIO**. Sollte ein Spiel deswegen Probleme machen: im Reiter FPS **„Sicherer Modus“**
einschalten und NeonMonitor neu starten – dann ganz ohne Treiber (CPU-Temperatur/-Leistung/-Takte fehlen,
alles andere inkl. FPS funktioniert).

---

## 7. Projektstruktur

```
NeonMonitor/
├─ NeonMonitor.sln
├─ Installieren.bat / .ps1       # Doppelklick-Installation inkl. Desktop-Symbol
├─ Deinstallieren.bat / .ps1     # Entfernen
├─ global.json                    # SDK-Version (.NET 10)
├─ docs/SENSOREN.md               # Kompatibilität Zen 5 / RDNA 4, Alternativen (ADLX …)
└─ src/NeonMonitor/
   ├─ NeonMonitor.csproj
   ├─ app.manifest                # fordert Adminrechte an, DPI-Einstellungen
   ├─ Assets/NeonMonitor.ico      # Programmsymbol
   ├─ App.xaml(.cs)               # Start, Dienste verdrahten, Fehlerbehandlung
   ├─ MainWindow.xaml(.cs)        # Fenster mit eigener Titelleiste, Navigation, Statusleiste
   ├─ Themes/
   │  ├─ Theme.xaml               # Farben, Schriften, Stile aller Steuerelemente (zum Umfärben nur hier ändern)
   │  └─ Templates.xaml           # Darstellung von Messwert-Zeilen, Kerntakten und Laufwerken
   ├─ Controls/
   │  ├─ AnimatedBackground.cs    # ruhiger animierter Hintergrund (Raster, Lichtflecken, Punkte)
   │  ├─ LineChart.cs             # schlankes Liniendiagramm
   │  └─ RingGauge.cs             # Ringanzeige (Last außen, Temperatur innen)
   ├─ Models/
   │  └─ HardwareSnapshot.cs      # eine Messung aller Werte (später = eine CSV-Zeile)
   ├─ Services/
   │  ├─ HardwareMonitorService.cs        # Mess-Thread, fragt alle Quellen ab
   │  ├─ Fps/
   │  │  ├─ PresentMonRunner.cs           # startet PresentMon unsichtbar, liest die CSV-Ausgabe
   │  │  ├─ PresentMonCsvParser.cs        # ordnet die CSV-Spalten über die Kopfzeile zu
   │  │  ├─ FrameTracker.cs               # wählt das Spiel, berechnet FPS / 1 % Low / Frametimes
   │  │  └─ FpsSource.cs                  # FPS als Sensorquelle (landet im HardwareSnapshot)
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

## 8. Fahrplan

1. ✅ Projekt-Setup, Live-Werte, Diagramme, Theme
2. ✅ FPS & Frametimes per ETW (PresentMon, keine Injection)
3. ⏳ Overlay (transparentes Always-on-top-Fenster, Werte wählbar)
4. ⏳ Aufzeichnung (Start/Stopp, Hotkey, CSV pro Sitzung)
5. ⏳ Vergleich (Ø FPS, 1 % Lows, Höchsttemperaturen, Diagramme)

## Lizenzen

Siehe [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): LibreHardwareMonitorLib (MPL-2.0), PresentMon (MIT).

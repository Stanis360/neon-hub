# NeonMonitor

Hardware-Überwachung für Windows 11 mit ruhiger, dunkler Oberfläche (Anthrazit + Mintgrün): Live-Werte, Verlaufsdiagramme,
später FPS (per ETW, ohne Injection), Overlay, Aufzeichnung als CSV und Sitzungsvergleich.

Gebaut mit **C# / .NET 10 / WPF**. Sensoren über **LibreHardwareMonitor**, ergänzt durch
Windows-Leistungsindikatoren als Fallback.

> Stand: **Version 1.1** – Live-Anzeige, FPS/Frametimes, Overlay, Aufzeichnung, Sitzungsvergleich,
> dazu Kühlung mit Lüftersteuerung, Infobereich-Symbol, Autostart, automatische Aufnahme, Benchmark,
> Warnungen, Netzwerk/Datenträger, Energie & Kosten und Spielzeit-Statistik.

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

1. Code holen – eine der beiden Varianten:
   * **ZIP:** <https://github.com/Stanis360/neon-hub> öffnen → grüner Knopf **„Code“** → **„Download ZIP“**,
     dann entpacken, z. B. nach `C:\Projekte` (es entsteht `C:\Projekte\neon-hub-main`).
   * **Git:** `git clone https://github.com/Stanis360/neon-hub.git C:\Projekte\neon-hub`
     (Update später mit `git pull` im Ordner).
2. Im entpackten Ordner **doppelt auf `Installieren.bat`** klicken und die Windows-Abfrage
   („Änderungen zulassen?“) mit **Ja** bestätigen.
   Falls Windows „Der Computer wurde durch Windows geschützt“ zeigt: **Weitere Informationen → Trotzdem ausführen**.
3. Das Skript baut das Programm, legt das Symbol **NeonMonitor** auf dem Desktop und im Startmenü an
   und startet es.

**Ab dann nur noch das Desktop-Symbol doppelklicken.**

* **Update:** neue ZIP von GitHub laden (oder `git pull`), alten Ordner ersetzen, wieder `Installieren.bat` – Einstellungen bleiben erhalten.
  Ein laufendes NeonMonitor wird dabei automatisch beendet.
* **Entfernen:** `Deinstallieren.bat`.
* Das Programm liegt danach in `%LOCALAPPDATA%\NeonMonitor\App`.
* Startet man NeonMonitor ein zweites Mal, wird einfach das schon offene Fenster nach vorne geholt.

---

## 3. Für Entwickler: direkt aus dem Quellcode starten

Terminal **als Administrator** öffnen (das Programm fordert Adminrechte an), dann:

```powershell
cd C:\Projekte\neon-hub-main
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

## 7. Overlay (Reiter „Overlay“)

* Kleines Fenster, das **immer im Vordergrund** bleibt – gedacht für einen **Nebenmonitor** neben dem Spiel.
* **Ein/aus:** Schalter im Reiter oder systemweit **Strg + Alt + O** (funktioniert auch mitten im Spiel).
* **Position:** Im Reiter für jeden Monitor eine Skizze mit vier Eckknöpfen – ein Klick setzt das Overlay
  in diese Ecke. Oder einfach mit der Maus verschieben. Beim ersten Start landet es oben rechts auf einem Nebenmonitor.
* **Fixieren:** Overlay lässt sich nicht mehr verschieben, Mausklicks gehen an das Fenster darunter.
* **Größe** (60–200 %) und **Hintergrund-Deckkraft** (0–100 %) einstellbar.
* **Werte frei wählbar:** Spielname, FPS, 1 % Low, Frametime, Frametime-Diagramm, CPU/GPU-Last, -Temperatur,
  -Leistung, -Takt, Hotspot, VRAM, RAM, Uhrzeit. Alles wird gespeichert, auch ob das Overlay offen war.

---

## 8. Aufzeichnung (Reiter „Aufzeichnung“)

* **Start/Stopp:** großer Knopf oder systemweit **Strg + Alt + R**. Während der Aufnahme steht **● REC 03:12**
  in der Titelleiste und im Overlay.
* **Spielname:** wird automatisch aus dem erkannten Spiel gefüllt (z. B. „Satisfactory“, „Dyson Sphere Program“,
  „Call of Duty“) und kann vorher überschrieben oder später im Reiter „Vergleich“ geändert werden.
* **Dateien** in `Dokumente\NeonMonitor\Sitzungen`:
  * `Spiel_JJJJ-MM-TT_HH-MM-SS.csv` – eine Zeile pro Messung mit allen Sensorwerten und FPS
  * `…-frames.csv` – jedes einzelne Bild mit Frametime (für exakte 1 % / 0,1 % Lows)
  * `….json` – Zusammenfassung für den Vergleich
* Die CSVs öffnen sich direkt in **Excel** (Semikolon, Dezimalkomma, UTF-8).
* Nach dem Stoppen zeigt der Reiter sofort Ø FPS, 1 % / 0,1 % Low und die Höchsttemperaturen.

## 9. Vergleich (Reiter „Vergleich“)

* Links alle Sitzungen (neueste oben) – anhaken, um sie zu vergleichen (bis zu 6, jede bekommt eine Farbe).
  Spielname direkt im Feld ändern (benennt die Dateien um), CSV öffnen oder löschen.
* **Tabelle:** Dauer, Ø FPS, 1 % Low, 0,1 % Low, Ø Frametime, max. CPU-/GPU-Temperatur, max. Hotspot,
  Ø Last und Leistung von CPU/GPU, max. RAM – der jeweils beste Wert ist grün hervorgehoben.
* **Diagramm:** Verlauf über die Zeit für FPS, Ruckler (längste Frametime), Temperaturen, Last oder Leistung.

Zusätzlich im Vergleich: Ø CPU-/Gehäuselüfter, **FPS pro Watt**, **Energie** (Wh) und **Stromkosten** (€) je Sitzung.

**Definitionen:** 1 % Low = 1000 / 99. Perzentil der Frametimes (99 % aller Bilder waren mindestens so schnell),
0,1 % Low entsprechend mit dem 99,9. Perzentil. Ø FPS = 1000 / mittlere Frametime.

---

## 10. Kühlung (Reiter „Kühlung“)

* **Lüfter:** alle Lüfteranschlüsse des Mainboards mit Drehzahl. Namen direkt im Feld ändern
  (z. B. „Gehäuselüfter (4×)“, wenn mehrere Lüfter an einem Anschluss hängen). Mit **CPU** / **Gehäuse** festlegen,
  welcher Anschluss in Live-Ansicht, Overlay und Aufnahme als CPU- bzw. Gehäuselüfter zählt.
  Ohne Festlegung rät NeonMonitor (Name enthält „CPU“, sonst bei ASUS/Nuvoton meist „Fan #2“).
* **Temperaturen:** Mainboard-Sensoren, SSDs (SMART) und DDR5-Module.
* **Lüftersteuerung** (standardmäßig **aus**): pro Anschluss *BIOS*, *Fest* (in 5-%-Schritten) oder *Kurve*
  (Leise / Ausgewogen / Leistung, nach CPU-Temperatur oder dem höheren Wert von CPU und GPU).
  **Sicherheitsnetz:** nie unter 20 %, Vollgas ab 90 °C CPU bzw. 105 °C GPU-Hotspot oder wenn ein
  Temperaturwert fehlt, nach unten nur langsam, beim Beenden regelt wieder das Mainboard.
  Bei einem Absturz kann ein Lüfter auf dem letzten Wert bleiben, bis NeonMonitor neu startet.
* GPU-Lüfter bleiben bewusst bei AMD Adrenalin. Mainboard-Sensoren brauchen PawnIO (im sicheren Modus aus).

## 11. Komfort

* **Infobereich-Symbol** (unten rechts): Temperaturen und FPS beim Darüberfahren, Doppelklick öffnet,
  Rechtsklick: Overlay, Aufnahme, Benchmark, Beenden. Schließen (✕) versteckt das Fenster nur (abschaltbar).
* **Autostart** (Einstellungen): über die Windows-Aufgabenplanung – startet bei der Anmeldung **ohne
  Admin-Abfrage** unsichtbar im Infobereich.
* **Automatische Aufnahme** (Einstellungen): startet 10 s nach Spielbeginn, stoppt 15 s nach Spielende.
* **Benchmark** (Strg + Alt + B): Aufnahme mit fester Dauer (30 s / 60 s / 2 min / 5 min) für faire Vergleiche.
* **Warnungen** als Windows-Benachrichtigung bei hohen Temperaturen (Grenzen einstellbar) und wenn der
  CPU-Lüfter steht, obwohl die CPU warm ist.
* **Netzwerk & Datenträger:** Empfang/Senden in Mbit/s, Lesen/Schreiben in MB/s (Live-Ansicht, Overlay, Aufnahme).
* **Energie:** CPU + GPU zusammen und FPS pro Watt live; Energie und Kosten je Sitzung (Strompreis einstellbar).
* **Statistik:** Spielzeit pro Spiel (läuft automatisch mit, auch ohne Aufnahme), Ø FPS, Summe aller Aufnahmen.

---

## 12. Projektstruktur

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
   │  ├─ MultiLineChart.cs        # Vergleichsdiagramm mit mehreren Sitzungen
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
   │  ├─ Recording/
   │  │  ├─ RecordingService.cs           # schreibt Sensor-CSV, Frame-CSV, berechnet die Zusammenfassung
   │  │  ├─ SessionStore.cs               # Sitzungen auflisten, einlesen, umbenennen, löschen
   │  │  ├─ SessionSummary.cs             # Inhalt der .json-Zusammenfassung
   │  │  └─ GameNames.cs                  # Exe-Name → Spielname
   │  ├─ Comfort/
   │  │  ├─ PlaytimeService.cs            # Spielzeit pro Spiel
   │  │  ├─ AlertService.cs               # Temperatur-/Lüfter-Warnungen
   │  │  └─ AutostartService.cs           # Autostart über die Aufgabenplanung
   │  └─ Sensors/
   │     ├─ ISensorSource.cs              # Schnittstelle für Sensorquellen
   │     ├─ LibreHardwareSource.cs        # Hauptquelle (CPU/GPU/RAM/Mainboard/SSD)
   │     ├─ FanControlEngine.cs           # Lüftersteuerung mit Sicherheitsnetz
   │     ├─ WindowsCounterSource.cs       # Fallback: Windows-Leistungsindikatoren
   │     ├─ PdhQuery.cs                   # Wrapper für pdh.dll
   │     └─ DriveSource.cs                # Laufwerksbelegung
   ├─ ViewModels/                 # MVVM: MainViewModel, MetricViewModel, ChartSeries …
   ├─ Views/                      # alle Reiter, Overlay-Fenster/-Controller, TrayIcon
   └─ Infrastructure/             # Pfade, Einstellungen, Log, System-Checks, Monitore, globale Hotkeys
```

### Architektur in einem Satz

`HardwareMonitorService` fragt auf einem eigenen Thread nacheinander alle `ISensorSource`s ab
(LibreHardwareMonitor → Windows-Fallback → Laufwerke); jede Quelle füllt nur die Werte, die
noch fehlen. Der fertige `HardwareSnapshot` geht per Event an die Oberfläche – und ab
Schritt 4 zusätzlich an die CSV-Aufzeichnung. Eine neue Quelle (z. B. ADLX) ist eine
zusätzliche Klasse, ohne den Rest anzufassen.

---

## 13. Fahrplan

1. ✅ Projekt-Setup, Live-Werte, Diagramme, Theme
2. ✅ FPS & Frametimes per ETW (PresentMon, keine Injection)
3. ✅ Overlay (Always-on-top-Fenster für den Nebenmonitor, Werte wählbar, Strg+Alt+O)
4. ✅ Aufzeichnung (Start/Stopp, Strg+Alt+R, CSV pro Sitzung mit Spielname, Datum und Uhrzeit)
5. ✅ Vergleich (Ø FPS, 1 % / 0,1 % Lows, Höchsttemperaturen, Verlaufsdiagramme)

6. ✅ Kühlung (Lüfter, Mainboard/SSD/RAM-Temperaturen, Lüftersteuerung mit Sicherheitsnetz)
7. ✅ Komfort (Infobereich, Autostart, Auto-Aufnahme, Benchmark, Warnungen, Netzwerk/Datenträger, Energie, Statistik)

Mögliche Erweiterungen: ADLX als zusätzliche GPU-Quelle (falls Sensoren fehlen), eigene Lüfterkurven mit
frei setzbaren Punkten, Export des Vergleichs als Bild.

## Lizenzen

Siehe [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): LibreHardwareMonitorLib (MPL-2.0), PresentMon (MIT).

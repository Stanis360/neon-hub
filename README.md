# NeonMonitor

Hardware-Überwachung für Windows 11 mit ruhiger, dunkler Oberfläche (Anthrazit + Mintgrün): Live-Werte, Verlaufsdiagramme,
später FPS (per ETW, ohne Injection), Overlay, Aufzeichnung als CSV und Sitzungsvergleich.

Gebaut mit **C# / .NET 10 / WPF**. Sensoren über **LibreHardwareMonitor**, ergänzt durch
Windows-Leistungsindikatoren als Fallback.

> Stand: **Version 1.5** – 5 umschaltbare Designs neu; Live-Anzeige, FPS/Frametimes, Overlay, Aufzeichnung, Sitzungsvergleich, Kühlung mit
> Lüftersteuerung, Engpass-Analyse, Latenz, Ruckler-Analyse, Ping/Paketverlust, Handy-Ansicht, Bericht als Bild,
> Langzeitverlauf, Infobereich, Autostart, automatische Updates.

---

## 1. Installation (empfohlen: fertige Version)

1. Auf **<https://github.com/Stanis360/neon-hub/releases>** beim neuesten Release
   **`NeonMonitor-x.y.z-win-x64.zip`** herunterladen.
   *(Die `…-portable.zip` enthält .NET bereits – nur nötig, wenn auf dem PC kein .NET installiert ist.)*
2. Entpacken und **`Installieren.bat`** doppelklicken, Windows-Abfrage mit **Ja** bestätigen.
   Falls „Der Computer wurde durch Windows geschützt“ erscheint: **Weitere Informationen → Trotzdem ausführen**.
   Fehlt die .NET 10 Desktop Runtime, bietet das Skript an, sie per winget zu installieren.
3. Fertig – Symbol **NeonMonitor** auf dem Desktop und im Startmenü.

**Einmalig zusätzlich** (für CPU-Temperatur/-Leistung und Mainboard-Lüfter): `winget install namazso.PawnIO`
(oder Installer von <https://pawnio.eu>). Für die GPU-Werte einen aktuellen AMD-Adrenalin-Treiber verwenden.

### Updates

NeonMonitor prüft beim Start und alle 6 Stunden, ob es auf GitHub eine neue Version gibt, und zeigt dann oben
einen Hinweis mit **„Jetzt aktualisieren“**: Download, Prüfsummen-Kontrolle (SHA-256), sauberes Beenden,
Installation und Neustart laufen automatisch. Abschaltbar unter *Einstellungen → Updates*.

### Entfernen

`Deinstallieren.bat` (liegt auch in `%LOCALAPPDATA%\NeonMonitor\App`). Einstellungen und Aufnahmen bleiben erhalten.

---

## 2. Neue Version veröffentlichen (für Entwickler)

Versionsnummer in `src/NeonMonitor/NeonMonitor.csproj` anheben, committen, dann ein Tag pushen:

```powershell
git tag v1.2.1
git push origin v1.2.1
```

GitHub Actions (`.github/workflows/release.yml`) baut auf Windows beide ZIP-Varianten, erzeugt `SHA256SUMS.txt`
und legt das Release an. Alternativ unter *Actions → Release → Run workflow* mit Versionsnummer starten.

---

## 3. Aus dem Quellcode bauen (für Entwickler)

Voraussetzung: `winget install Microsoft.DotNet.SDK.10`. Danach entweder im Quellcode-Ordner
`Installieren.bat` doppelklicken (baut und installiert) oder direkt starten – im Terminal **als Administrator**:

```powershell
cd C:\Projekte\neon-hub
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

### Engpass, Latenz, Ruckler, Verbindung (ebenfalls im Reiter „FPS“)

* **Engpass:** *GPU-Limit* (Grafikkarte ≥ 94 %), *CPU-Limit* (GPU darunter, aber ein CPU-Thread ≥ 85 %),
  *FPS-Grenze* (beides darunter – FPS-Limit, V-Sync oder Engine) oder *ausgeglichen*. Mit passendem Tipp.
  Die CPU-Gesamtlast taugt dafür nicht – entscheidend ist der am stärksten belastete Thread.
* **Latenz** (von PresentMon gemessen): *Eingabe → Bild* und *Klick → Bild* = Zeit von Maus/Tastatur bis das Bild
  auf dem Monitor ist, gemittelt über 5 s – ohne die Elektronik von Maus und Monitor.
* **Ruckler:** Bild dauert mind. doppelt so lange wie üblich *und* ≥ 8 ms länger. Anzeige als Ruckler pro Minute,
  dazu die **Frametime-Verteilung** der letzten 30 s (hoher, schmaler Balken = gleichmäßig).
* **Verbindung & Paketverlust:** jede Sekunde ein Ping zum **Router** und ins **Internet** (1.1.1.1), Auswertung über
  60 s: Verlust, Ø Ping, Jitter, bei WLAN die Signalstärke. Daraus die Einschätzung, *wo* Verlust entsteht:
  schon zum Router → Heimnetz/WLAN (LAN-Kabel hilft meist); erst danach → Anschluss/Anbieter; beides sauber → eher
  der Spielserver. Ab 3 % Verlust kommt eine Windows-Benachrichtigung.

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

Zusätzlich im Vergleich: Ruckler pro Minute, Ø Eingabe-/Klick-Latenz, Anteil GPU-/CPU-Limit, Ø Ping und
Paketverlust, Ø CPU-/Gehäuselüfter, **FPS pro Watt**, **Energie** (Wh) und **Stromkosten** (€) je Sitzung.

**Als Bild:** „Als Bild speichern“ (PNG in `Dokumente\NeonMonitor\Berichte`) oder „Bild kopieren“ – Tabelle und
Diagramm inkl. Hardware, z. B. zum Einfügen in Discord.

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

* **Designs** (Einstellungen → Design, wirksam nach Neustart per Knopf): *Graphit & Mint*, *Midnight Blue*,
  *Carbon & Orange*, *Rubin* (Adrenalin-Stil) und *Synthwave*. Gilt auch für Overlay, Hintergrund-Animation und
  die Handy-Seite. Neue Designs: einen Eintrag in `src/NeonMonitor/Themes/ThemeCatalog.cs` ergänzen.
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
* **Statistik & Langzeitverlauf:** Spielzeit pro Spiel, dazu pro Tag max. CPU/GPU/Hotspot und **Ø CPU-Temperatur
  beim Spielen** (7/30/90 Tage, mit Wochenvergleich – ideal, um die Kühlung über Wochen zu beobachten) sowie der
  **SSD-Zustand** (Lebensdauer, geschriebene Daten, Betriebsstunden).
* **Handy-Ansicht** (Einstellungen): Werte live im Browser von Handy/Tablet, per QR-Code. Nur aus dem
  Heimnetz, nur mit zufälligem Schlüssel in der Adresse; Firewall-Regel nur für „Private“ Netzwerke.
* **Handy-Deck** (Reiter „Deck“, auf dem Handy oben „Deck“): große Knöpfe wie bei einem Stream Deck.
  Aktionen: **Tastenkürzel** (z. B. Discord stumm, Screenshot, Adrenalin – Kombination einfach im Feld drücken),
  **Programm/Datei** starten (mit normalen Rechten, nicht als Admin), **Webseite** öffnen, **Medien** (Play/Pause,
  Weiter, Lautstärke, Stumm), **Text** eintippen, **NeonMonitor** (Aufnahme, Benchmark, Overlay, Fenster),
  **System** (Ausschalten/Neustart mit 15 s Vorwarnung und Rückfrage auf dem Handy, Abbrechen, Standby,
  Ruhezustand, Sperren, Bildschirme aus – „+ Energie-Knöpfe“ legt „Ausschalten“ und „Abbrechen“ an).
* **PC per Handy einschalten (Wake-on-LAN):** geht nicht über den Browser, sondern per WoL-App oder Fritzbox
  („Computer starten“). Der Reiter „Deck“ zeigt MAC-Adresse/IP, prüft den Windows-Schnellstart, richtet Windows mit einem
  Klick ein (Netzwerkkarte darf wecken, Schnellstart aus) und erklärt die BIOS-Einstellung
  (ASUS: Advanced → APM Configuration → „Power On By PCI-E“ = Enabled, „ErP Ready“ = Disabled). Braucht meist LAN-Kabel.
  Symbol (Emoji, Windows-Taste + .), Name, Farbe und Reihenfolge frei wählbar, „▶“ testet am PC.
  Sicherheit: Steuerung standardmäßig **aus**; das Handy kann nur die angelegten Knöpfe auslösen (es schickt nur
  deren Kennung, Pfade/Tasten bleiben am PC), max. 8 Auslösungen pro Sekunde.
  Hinweis: Tastenkürzel gehen an das Fenster im Vordergrund bzw. an Programme mit globalen Kürzeln; manche
  Spiele mit Anti-Cheat ignorieren künstliche Tastendrücke.

---

## 12. Projektstruktur

```
NeonMonitor/
├─ NeonMonitor.sln
├─ .github/workflows/release.yml # baut Releases automatisch (Tag v*)
├─ Installieren.bat / .ps1       # Installation aus Release-ZIP oder Quellcode, inkl. Desktop-Symbol
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
   │  ├─ Deck/                          # Handy-Deck: Knöpfe, Tastenkürzel, SendInput, Ausführung
   │  ├─ Comfort/
   │  │  ├─ PlaytimeService.cs            # Spielzeit pro Spiel
   │  │  ├─ AlertService.cs               # Temperatur-/Lüfter-Warnungen
   │  │  ├─ AutostartService.cs           # Autostart über die Aufgabenplanung
   │  │  └─ UpdateService.cs              # Update-Prüfung und -Installation über GitHub Releases
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
8. ✅ Analyse (Engpass, Latenz, Ruckler, Ping/Paketverlust), Handy-Ansicht, Bericht als Bild, Langzeitverlauf
9. ✅ Handy-Deck mit frei belegbaren Knöpfen
7. ✅ Komfort (Infobereich, Autostart, Auto-Aufnahme, Benchmark, Warnungen, Netzwerk/Datenträger, Energie, Statistik)

Mögliche Erweiterungen: ADLX als zusätzliche GPU-Quelle (falls Sensoren fehlen), eigene Lüfterkurven mit
frei setzbaren Punkten, Export des Vergleichs als Bild.

## Lizenzen

Siehe [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): LibreHardwareMonitorLib (MPL-2.0), PresentMon (MIT).

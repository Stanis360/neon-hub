# Sensoren: Kompatibilität mit Ryzen 7 9800X3D und Radeon RX 9070 XT

Verwendet wird **LibreHardwareMonitorLib 0.9.6** (aktuelle stabile Version). Ich habe das
Paket untersucht: Es bringt den **PawnIO**-Treiberzugang (statt WinRing0), das **Ryzen-SMU**-Modul
sowie die Sensornamen für Zen-5-CPUs (`Core (Tctl/Tdie)`, `CCD1 (Tdie)`, `CCDs Max (Tdie)`, …)
und für AMD-GPUs (`GPU Core`, `GPU Hot Spot`, `GPU Package`, `GPU Memory Used`, …) mit.

**Wichtig:** Auf echter Hardware geprüft ist das erst, wenn du den **Sensorbericht** schickst
(Knopf unten rechts). Die Tabelle unten ist der erwartete Stand.

## CPU – Ryzen 7 9800X3D (Zen 5, „Granite Ridge“, Familie 1Ah)

| Wert | Quelle | Erwartung | Voraussetzung |
|---|---|---|---|
| Last | LHM `CPU Total`, Fallback Windows-Zähler | ✅ | – |
| Temperatur | LHM `Core (Tctl/Tdie)` (SMU) | ✅ | PawnIO, Admin |
| Takt Ø / max | LHM `Core #1…#8` | ✅ | PawnIO, Admin |
| Leistung | LHM `Package` (RAPL-MSR) | ✅ | PawnIO, Admin |

Hinweise:
* Ohne PawnIO bleibt nur die Last (über Windows) – das Programm zeigt dann ein Warnbanner.
* Die Kerntakte von LHM sind Momentwerte; HWiNFO zeigt zusätzlich „effektive“ Takte, die bei
  Teillast niedriger sind. Für Spiele-Vergleiche ist das unkritisch.

## GPU – Radeon RX 9070 XT (RDNA 4, „Navi 48“)

| Wert | Quelle | Erwartung | Fallback |
|---|---|---|---|
| Last | LHM `GPU Core` (ADL) | ✅ | ✅ Windows `GPU Engine` (eingebaut) |
| Temperatur (Edge) | LHM `GPU Core` | ✅ wahrscheinlich | ADLX |
| Hotspot | LHM `GPU Hot Spot` | ⚠️ prüfen | ADLX |
| Kerntakt / Speichertakt | LHM `GPU Core` / `GPU Memory` | ✅ wahrscheinlich | ADLX |
| VRAM belegt / gesamt | LHM `GPU Memory Used/Total`, `D3D Dedicated Memory …` | ✅ | ✅ Windows `GPU Adapter Memory` (eingebaut) |
| Leistung | LHM `GPU Package` → `GPU Total` → `GPU PPT` | ⚠️ prüfen | ADLX |
| Lüfter | LHM `GPU Fan` | ✅ wahrscheinlich | ADLX |

Hinweise:
* Der 9800X3D hat eine **integrierte Radeon-Grafik**. LHM sieht also zwei AMD-GPUs. NeonMonitor
  wählt automatisch die mit dem meisten dedizierten Speicher (= RX 9070 XT).
* Bei neuen GPU-Generationen ist es typisch, dass einzelne Sensoren über das alte ADL anfangs
  fehlen oder anders heißen. Genau dafür ist der Sensorbericht da.
* Ob „Leistung“ die Chipleistung oder die gesamte Board-Leistung (TBP) ist, hängt vom Treiber ab.
  Am besten einmal mit dem Adrenalin-Overlay vergleichen.

## RAM und Laufwerke

| Wert | Quelle | Erwartung |
|---|---|---|
| RAM belegt / gesamt / % | LHM `Memory`, Fallback `GlobalMemoryStatusEx` | ✅ |
| Laufwerksbelegung | .NET `DriveInfo` (alle festen Laufwerke) | ✅ |

## Alternativen, falls Werte fehlen

1. **ADLX (AMD Device Library eXtension)** – *Empfehlung für fehlende GPU-Werte.*
   Offizielles, modernes AMD-SDK (Nachfolger von ADL), das auch Adrenalin selbst nutzt.
   Unterstützt RDNA 4 vollständig: GPU-Last, Edge-/Hotspot-/Speichertemperatur, Takte, VRAM,
   Board-Leistung, Lüfter. Das SDK ist C++; es liefert aber C#-Bindings (SWIG-Beispiel
   „ADLXCSharpBind“) mit. Einbau: eine kleine native Bridge-DLL + neue Klasse `AdlxSource`
   als weitere `ISensorSource`. Die DLL `amdadlx64.dll` kommt mit dem Treiber.
2. **Windows-Leistungsindikatoren** – bereits eingebaut. Liefern GPU-Last und VRAM für jede GPU,
   aber keine Temperaturen, Takte oder Leistung.
3. **HWiNFO Shared Memory** – HWiNFO hat sehr früh vollständige Zen-5-/RDNA-4-Unterstützung.
   NeonMonitor könnte dessen Shared-Memory-Schnittstelle lesen. Nachteil: HWiNFO muss parallel
   laufen und „Shared Memory Support“ aktiviert sein.
4. **FPS kommen nicht aus LHM**, sondern aus **PresentMon 2.6.0** (ETW-Ereignisse von DXGI/D3D9/DxgKrnl) –
   ohne Code in das Spiel einzuschleusen. Getestet wird das Format `--v1_metrics`
   (Spalten `TimeInSeconds`, `msBetweenPresents`), mit Fallback auf das 2.x-Format.

## Anti-Cheat

NeonMonitor schleust nichts in Spiele ein. Der einzige Kernel-Zugriff ist der signierte
PawnIO-Treiber für CPU-Sensoren. Er gilt als sicherer Ersatz für WinRing0; einzelne
Kernel-Anti-Cheats (z. B. Vanguard, FACEIT) können trotzdem empfindlich auf Treiber für
Hardwarezugriff reagieren. Dafür gibt es den **Sicheren Modus** (Reiter FPS): Dann lädt
LibreHardwareMonitor nur die GPU (über den AMD-Treiber) – CPU und RAM-Module bleiben aus, PawnIO
wird nicht benutzt. CPU-Last und RAM kommen dann über Windows.

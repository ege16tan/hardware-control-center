# Hardware-Verifikation MSI H87-G43

**Datum:** 2026-11-02 (Phase A erneut nach Treiberinstallation)
**Board:** MSI H87-G43 (MS-7816)
**CPU:** Intel Core i7-4770
**GPU:** NVIDIA GeForce GTX 1060 6GB
**Tool:** LibreHardwareMonitorLib 0.9.6, .NET 10.0.403, PawnIO 2.2.0
**Ergebnis:** ✅ Phase A (Lesen) bestanden — Super-I/O liefert 29 Sensoren und
6 Lüfterkanäle.

---

## 1. Zusammenfassung

Nach der Installation von **PawnIO 2.2.0** (offizielle, signierte Version) liest
LibreHardwareMonitor das Board vollständig:

- Der Super-I/O-Chip ist ein **Nuvoton NCT6779D** (29 Sensoren).
- **5 Lüfterkanäle** (Fan #1–#5) mit Drehzahl und Steuerwert sind sichtbar.
- Die **CPU-Kerntemperaturen** kommen jetzt über MSR (35 Sensoren, u. a.
  `CPU Package` und `Core Max`).
- Alle Geräte melden im Normalzustand keinen Fehler (`Status: (ok)`).

Alle Kanäle bleiben trotzdem `ControlCandidate` — gelesen heißt nicht verifiziert.
Der Schreibtest (Phase B) ist noch nicht durchgeführt.

---

## 2. Ursache (korrigiert gegenüber der ersten Messung)

Die erste Messung (0 Sensoren am Mainboard) wurde dem blockierten
**WinRing0**-Treiber zugeschrieben. Das war für ältere
LibreHardwareMonitor-Versionen richtig, gilt aber für 0.9.6 nicht mehr:

- Aufruf PR [#1857](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/1857)
  hat WinRing0 durch **PawnIO** ersetzt; seit **v0.9.5** gibt es keinen
  WinRing0-Code mehr in der Bibliothek.
- LibreHardwareMonitor 0.9.6 liefert die PawnIO-Module (`LpcIO.bin`,
  `IntelMSR.bin`, …) **eingebettet** mit und lädt sie erst zur Laufzeit in den
  PawnIO-Treiber. Ohne installierten Treiber bleiben alle Ring0-Sensoren leer,
  **ohne Fehlermeldung**.
- Der PawnIO-Gerätezugriff ist auf **SYSTEM und Administratoren** beschränkt
  (Security-Descriptor der Treiber-INF). HCC läuft deshalb seit dieser Änderung
  über `src/HCC.App/app.manifest` mit `requireAdministrator`.

Fachliche Einordnung: Das Board war nie defekt, es fehlte nur der Treiber.

---

## 3. Messung nach der Installation

```
Status: (ok)
--- Devices ---
MSI H87-G43 (MS-7816)      Motherboard   sensors=0    (Sensoren hängen am Super-I/O)
Nuvoton NCT6779D           SuperIO       sensors=29
Intel Core i7-4770         Cpu           sensors=35
Virtual Memory             Memory        sensors=3
Total Memory               Memory        sensors=3
NVIDIA GeForce GTX 1060    GpuNvidia     sensors=30
```

Lüfterkanäle (Messung 2026-11-02 18:01, System unter Last durch Build/Tests):

| Kanal | Drehzahl | Steuerwert (BIOS/Board) | Status im Code |
|---|---|---|---|
| Fan #1 | 0 RPM | 84,7 % | `ControlCandidate` |
| Fan #2 | 2116 RPM | 80,0 % | `ControlCandidate` |
| Fan #3 | 0 RPM | 60,0 % | `ControlCandidate` |
| Fan #4 | 1133 RPM | 100,0 % | `ControlCandidate` |
| Fan #5 | 0 RPM | 60,0 % | `ControlCandidate` |
| GPU (NVAPI) | 1454 RPM | 59,0 % | `ControlCandidate` |

Weitere gelesene Werte: `CPU Package` 64 °C, `Core Max` 63 °C,
GPU Core 81 °C, GPU Hot Spot 89,2 °C, Board-Temperaturen 21–35 °C.

**Offene Punkte aus dieser Messung:**

- Fan #1/#3/#5 zeigen 0 RPM — entweder nicht angeschlossen oder unter der
  Anlaufdrehzahl. Vor jeder Steuerung am Board prüfen.
- `Temperature #6` zeigt konstant 91 °C und ist fachlich unplausibel (kein
  zugeordneter Sensor). Nicht als Alarmquelle verwenden, bis die Zuordnung am
  Board geklärt ist.
- Die Steuerwerte sind der vom Board gemeldete Zustand; HCC hat nichts
  geschrieben.

---

## 4. Code- und Setup-Änderungen

- `src/HCC.Hardware/Monitoring/PawnIoDiagnostics.cs` — erkennt PawnIO-Version,
  Meldet fehlenden/veralteten Treiber, wenn Mainboard und Super-I/O ohne
  Sensoren bleiben; Tests in `tests/HCC.Tests/PawnIoDiagnosticsTests.cs`.
- `src/HCC.Hardware/Monitoring/HardwareMetricsProvider.cs` — der Status des
  Snapshots enthält bei fehlendem Treiber einen konkreten Handlungshinweis.
- `src/HCC.App/app.manifest` + `HCC.App.csproj` — `requireAdministrator`, weil
  der PawnIO-Gerätezugriff Adminrechte verlangt.
- `README.md`, `DEVELOPMENT_SETUP.md` — PawnIO als Voraussetzung dokumentiert.

---

## 5. Status der Kanäle

- Alle Kanäle bleiben `ControlCandidate` — kein Kanal ist `ControlVerified`.
- `LhmFanController.SetPercent` und `.Reset` werfen weiterhin
  `NotSupportedException`.
- Die UI zeigt weiterhin „Steuer-Sensor erkannt · nicht verifiziert“.

Nächster Schritt ist **Phase B** aus `LUEFTERSTEUERUNG_PLAN.md`: Schreibtest auf
einem Kanal mit Aufsicht, zweites Temperatur-Monitoring, Rückfall auf
BIOS-Automatik. Erst danach darf ein Kanal `ControlVerified` werden.

---

## 6. Referenzen

- `LUEFTERSTEUERUNG_PLAN.md` — Phasen A–D, Abschnitt 9 (Einschränkungen),
  Abschnitt 4.3 (Doku-Ablage)
- `PHASENPLAN.md` — Gesamtprojekt
- `docs/ring0-treiber-integration.md` — Treiber-Handoff
- PawnIO: <https://pawnio.eu/> ·
  [Installation](https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe)
- LibreHardwareMonitor PR #1857 (WinRing0 → PawnIO):
  <https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/1857>

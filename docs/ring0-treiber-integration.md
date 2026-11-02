# Ring0-Treiber-Integration — Stand nach Umsetzung

**Status:** ✅ Treiberzugang und Hardware-Verifikation abgeschlossen am
2026-11-02. Phase A (Lesen) und Phase B (Schreibtest mit Aufsicht) sind am
Zielboard bestanden. Offen ist Phase C: der gesperrte Schreibpfad in der App.

---

## 1. Was tatsächlich die Ursache war

Die ursprüngliche Annahme „WinRing0 ist blockiert" gilt für
LibreHardwareMonitor **0.9.6 nicht mehr**. Seit v0.9.5 nutzt die Bibliothek
**PawnIO** als einzigen Ring0-Zugang
([PR #1857](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/1857)).
Die PawnIO-Module (`LpcIO.bin`, `IntelMSR.bin`, …) liegen als eingebettete
Ressourcen in `LibreHardwareMonitorLib.dll` und werden zur Laufzeit in den
installierten PawnIO-Treiber geladen.

Fehlte der Treiber, wurden Mainboard/Super-I/O **ohne Fehlermeldung** mit
0 Sensoren gemeldet. Genau das war der beobachtete Zustand.

Zusätzlich gilt: Das PawnIO-Gerät erlaubt nur **SYSTEM und Administratoren**
(Security-Descriptor der INF), daher muss die App erhöht laufen.

---

## 2. Was umgesetzt wurde

| Schritt | Ergebnis |
|---|---|
| PawnIO gewählt | Offizielle signierte Edition 2.2.0 von <https://pawnio.eu/> |
| Installation | `PawnIO_setup.exe -install`, Dienst `PawnIO` läuft, Softwaregerät `ROOT\PAWNIO\0000` |
| Verkabelung in LHM | keine nötig — 0.9.6 unterstützt PawnIO nativ; WinRing0 ist entfernt |
| App-Rechte | `src/HCC.App/app.manifest` mit `requireAdministrator` |
| Fehlerdiagnose | `PawnIoDiagnostics` meldet fehlenden/veralteten Treiber im Snapshot-Status |
| Verifikation | `HardwareMetricsProvider.ReadCurrent()` mit Admin: Status `(ok)`, NCT6779D mit 29 Sensoren, 5 Lüfterkanäle |

Details und Messwerte: `docs/hardware-verification-h87-g43.md`.

---

## 3. Zielsystem (bestätigt)

| Komponente | Wert |
|---|---|
| Mainboard | MSI H87-G43 (MS-7816) |
| Super-I/O | **Nuvoton NCT6779D** (bestätigt, 29 Sensoren) |
| CPU | Intel Core i7-4770 (Haswell, LGA1150) |
| GPU | NVIDIA GeForce GTX 1060 6GB |
| RAM | ~32 GB |
| OS | Windows 11, x64, HVCI aktiv |
| Toolchain | .NET SDK 10.0.403, LibreHardwareMonitorLib 0.9.6, PawnIO 2.2.0 |

---

## 4. So wird der Zustand reproduziert

1. PawnIO installieren (Admin): <https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe>
   mit `-install`; Registry-Prüfung: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO` → `DisplayVersion`.
2. Diagnose erhöht ausführen (Konsole, ohne App-Start):

```csharp
using HCC.Hardware.Monitoring;

using var provider = new HardwareMetricsProvider();
var s = provider.ReadCurrent();
Console.WriteLine($"Status: {s.Status ?? "(ok)"}");
Console.WriteLine($"PawnIO: {PawnIoDiagnostics.GetInstalledVersion()?.ToString() ?? "(fehlt)"}");
foreach (var d in s.HardwareDevices ?? [])
    Console.WriteLine($"{d.Name,-40} {d.Type,-18} sensors={d.SensorCount}");
foreach (var c in s.FanChannels ?? [])
    Console.WriteLine($"{c.DisplayName,-12} {c.Rpm,6:0} RPM  Control={c.ControlPercent?.ToString("0") ?? "-",4}  {c.Capability}");
```

Ablauf: `dotnet new console` → `dotnet add reference <repo>\src\HCC.Hardware` →
Code einfügen → `dotnet build` → EXE über
`Start-Process -Verb RunAs -Wait` starten (PawnIO-Zugriff braucht Admin).

---

## 5. Was bewusst NICHT gebaut wurde

- Kein eigener `.sys`-Treiber und kein eigener PawnIO-Modul-Build — Module müssen
  signiert und upstream aufgenommen werden; LHM liefert passende Module bereits mit.
- Kein `native/`-Ordner: Es gibt kein Treiber-Binary, das die App bündeln müsste.
  Der PawnIO-Installer wird nicht mitgeliefert (GPL-Redistribution vermeiden).
- Keine Deaktivierung von Secure Boot, HVCI oder der Vulnerable-Driver-Blocklist.
- Keine Umgehung des Adminrechts: Der Gerätezugriff ist bewusst nur für
  Administratoren freigegeben.

---

## 6. Offen (nächster Schritt)

1. **Phase B ist abgeschlossen** (2026-11-02, mit Aufsicht): Alle fünf Kanäle
   akzeptierten 40 % Software-Steuerung, Fan #2 und Fan #4 reagierten an der
   Drehzahl (1261 bzw. 616 RPM), `SetDefault()` brachte alle Kanäle in die
   BIOS-Automatik zurück. Fan #1/#3/#5 blieben bei 0 RPM und sind nicht
   verifizierbar. Details: `docs/hardware-verification-h87-g43.md`.
2. **Phase C:** `LhmFanController` mit Allowlist für Fan #2/#4, Mindestwert,
   Rücksetzen in `Dispose`/`try/finally` und Tests mit Fakes. Bis dahin bleiben
   alle Kanäle im Code gesperrt.
3. **Restrisiko Absturz:** LibreHardwareMonitor setzt die Software-Steuerung
   beim Schließen nicht zurück. Ein App-Absturz lässt den letzten Wert stehen;
   die BIOS-Automatik greift erst nach Neustart. Das muss die Steuerung durch
   explizites Zurücksetzen und einen Notfall-Rückfall abdecken.
4. **Zuordnung der Anschlüsse** (Fan #2/#4 → CPUFAN/SYSFAN) im BIOS abgleichen;
   beim späteren Schreibzugriff zusätzlich die ISA-Bus-Mutex
   `\BaseNamedObjects\Access_ISABUS.HTP.Method` beachten.

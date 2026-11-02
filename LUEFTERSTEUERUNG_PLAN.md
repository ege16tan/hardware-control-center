# Lüftersteuerung für MSI H87-G43 – Plan und Umsetzungsanleitung

**Projekt:** Hardware Control Center (HCC)
**Stack:** C# / .NET 10, WPF, MVVM Toolkit, LibreHardwareMonitor 0.9.6
**Status:** Planung. Keine Schreibzugriffe implementiert.
**Zielboard:** MSI H87-G43 (ausschließlich dieses Board)

---

## 1. Grundsatz

Lüftersteuerung über Software greift direkt in die Hardware ein. Ein falscher Wert kann einen Lüfter stoppen und dadurch die CPU überhitzen lassen. Deshalb gilt die Reihenfolge:

1. **Lesen** (Erkennung und Anzeige, keine Risiken)
2. **Verifizieren** (prüfen, welche Kanäle wirklich schreibbar sind)
3. **Steuern** (erst nach bestätigtem Test auf dem Zielboard)

Solange Schritt 2 nicht bestanden ist, bleibt die Steuerung gesperrt. Die UI zeigt dann nur „nur überwacht“.

---

## 2. Ausgangslage im bestehenden Code

Diese Punkte stammen aus dem vorliegenden Quellcode und sind relevant für die Umsetzung.

| Stelle | Beobachtung | Auswirkung |
|---|---|---|
| `HardwareMetricsProvider.ReadHardware` | Sensoren ohne Einheit (`SensorType.Control`) werden per `if (unit.Length == 0) continue;` verworfen | Steuerwerte und Sollwerte sind aktuell nicht sichtbar |
| `HardwareMetricsProvider` | `IsMotherboardEnabled = true`, `IsControllerEnabled = true` | Super-I/O-Sensoren werden grundsätzlich abgefragt |
| `HardwareSensorReading` | Enthält Hardware, Name, Typ, Wert, Einheit | Fan-RPM sind als generische Messwerte vorhanden, aber ohne Zuordnung zum Anschluss |
| `MetricSnapshot` | Keine Lüfter- oder Steuerdaten | Muss erweitert oder über eigenes Modell geführt werden |
| `MetricsSampler` | Hintergrund-Polling, Ereignisse `SnapshotUpdated` und `SamplingFailed` | Lüfterdaten sollten über denselben Sampler laufen (kein zweiter Hardwarezugriff) |
| `MonitoringSettings` | JSON-Persistenz, Profile, `RefreshIntervalChanged` | Kurven-Konfiguration kann nach diesem Muster gespeichert werden |
| `README.md` / `PHASENPLAN.md` | Lüfteranschluss-Erkennung, Kurven und sichere Freigabe sind offen | Diese Punkte sind die Zielaufgaben |
| `.github/copilot-instructions.md` | Keine Entfernung von Fehlerbehandlung und Schutzmechanismen | Gilt für alle Änderungen in diesem Bereich |

**Wichtig:** Die Lüfterlogik darf nicht im Hintergrundpfad des Samplers schreiben. Schreibzugriffe gehören in einen eigenen Dienst mit klarer Zustandsmaschine (siehe Abschnitt 5).

---

## 3. Phase A – Erkennung (nur lesen)

### 3.1 Ziel
Ein Diagnosebericht, der zeigt, welche Lüfter- und Steuerkanäle das Board tatsächlich meldet.

### 3.2 Umsetzung
- Neue Klasse `FanDiscoveryService` in `HCC.Hardware/Monitoring/` (Lesen, kein Schreiben).
- Durchläuft `Computer.Hardware` inklusive `SubHardware`.
- Erfasst pro Sensor:
  - Hardware-Name und Hardware-Typ (`HardwareType`, z. B. Mainboard/SuperIO)
  - Sensorname, `SensorType` (`Fan`, `Control`, `Temperature`)
  - Aktueller Wert, sofern vorhanden
- Gruppiert Fan-Sensoren zu **Lüfter-Kanälen** anhand des Namens (z. B. „Fan #1“, „CPU Fan“, „System Fan #2“). Die Zuordnung ist nur ein Vorschlag, bis sie auf dem Board bestätigt ist.
- Ergebnis: Liste `FanChannel` mit Feldern:
  - `Id` (stabil, aus Hardware-Identifier und Sensorname)
  - `DisplayName`
  - `RpmSensorId`, optional `ControlSensorId`
  - `Capability`: `NotDetected`, `MonitorOnly`, `ControlCandidate`, `ControlVerified`

### 3.3 Diagnoseausgabe
Eine einfache Testansicht oder ein Export als Textdatei (keine Zugangsdaten, keine Pfade mit Benutzernamen), mit:
- Mainboard-Name aus LibreHardwareMonitor
- Alle Super-I/O-Chips
- Alle Fan- und Control-Sensoren mit Namen und Wert
- Hinweis, ob LHM einen Fehler beim Initialisieren gemeldet hat

**Diese Diagnose ist der wichtigste Schritt.** Ohne ihre Ausgabe ist keine Steuerung seriös möglich.

---

## 4. Phase B – Verifikation der Steuerung (Testplan auf dem Zielboard)

Diese Phase wird nur auf dem echten MSI H87-G43 ausgeführt, mit Aufsicht.

### 4.1 Vorbereitung
- Im BIOS die Lüfter-Einstellungen notieren (Smart Fan / Manual / Full Speed). Originalwerte sichern, bevor getestet wird.
- Temperatur mit einem zweiten Tool überwachen (z. B. das BIOS-Monitoring oder HWiNFO), unabhängig von HCC.
- Test nur bei kühlem System starten, bei eingeschaltetem Netzteil und ohne Last.

### 4.2 Testschritte pro Kanal
1. Prüfen, ob ein Control-Sensor existiert und einen Wert ausgibt.
2. Prüfen, ob die API den Kanal als steuerbar meldet (LHM-Control-Schnittstelle, siehe 5.2). Nicht annehmen, dass ein vorhandener Sensor steuerbar ist.
3. Auf einen **moderaten** Wert setzen (z. B. 60 %), 10 Sekunden warten, RPM prüfen.
4. Zurück auf den **Rückfallwert** (BIOS-Automatik) setzen. Prüfen, dass die RPM wieder steigen.
5. Testen, ob ein Beenden der App den Kanal zurücksetzt (Prozess beenden, RPM beobachten).
6. Testen, ob ein Absturz simuliert durch Task-Kill den Kanal zurücksetzt. Falls nicht: Ergebnis dokumentieren und Restrisiko beschreiben. Das BIOS ist in diesem Fall die Sicherheitsebene.

### 4.3 Bewertung
- **Bestanden:** Schreibwert wird übernommen, RPM reagieren, Rückfall funktioniert. Kanal erhält `ControlVerified`.
- **Nicht bestanden oder unklar:** Kanal bleibt `MonitorOnly`. Keine weitere Freigabe.
- Ergebnis in `docs/hardware-verification-h87-g43.md` festhalten (Datum, BIOS-Version, Tool-Version, Ergebnis pro Kanal).

---

## 5. Phase C – Steuerung implementieren

### 5.1 Architektur (MVVM und DI einhalten)

```
HCC.Core
  Models/FanChannel.cs              (Datenmodell, Capability-Enum)
  Models/FanCurvePoint.cs           (Temperatur, Prozent)
  Interfaces/IFanController.cs      (Lesen, Setzen, Zurücksetzen)
  Interfaces/IFanCurveProvider.cs   (optional, Temperaturquelle)

HCC.Hardware
  Monitoring/FanDiscoveryService.cs (Phase A)
  Controllers/LhmFanController.cs   (einziger Ort mit Schreibzugriff)

HCC.Infrastructure
  Configuration/FanCurveSettings.cs (JSON, Muster wie MonitoringSettings)
  Monitoring/FanCurveEngine.cs      (reine Logik, ohne Hardware)
  Monitoring/FanControlService.cs   (Zustandsmaschine, Schutz, Rückfall)

HCC.App
  ViewModels/FanControlViewModel.cs
  Views/FanControlView.xaml(.cs)
```

Die Kurvenlogik (`FanCurveEngine`) enthält **keine** Hardwarezugriffe. Dadurch ist sie vollständig testbar.

### 5.2 Schnittstelle zu LibreHardwareMonitor
- Die Control-Funktion von LHM (`ISensor.Control`, Typ `IControl`, Methoden `SetSoftware` und `SetDefault`) muss gegen **genau Version 0.9.6** geprüft werden, bevor sie verwendet wird. Die Signaturen nicht aus dem Gedächtnis übernehmen.
- Vor dem Schreiben: `Control` muss ungleich `null` sein, und der Kanal muss in Phase B als `ControlVerified` markiert sein.
- Jeder Schreibzugriff geht über `LhmFanController`. Kein anderer Code ruft `SetSoftware` auf.

### 5.3 Sicherheitsregeln (verbindlich)
1. **Mindestwert:** Ein Kanal darf nie unter einen konfigurierbaren Mindestwert fallen (Vorschlag: 30 %, nur nach Test anpassen).
2. **Maximalwert:** 100 % als harte Grenze, Werte werden geklemmt.
3. **Ungültige Eingaben:** `NaN`, `Infinity`, negative Werte oder Werte über 100 werden abgelehnt, nicht geklemmt, und zu keinem Schreibzugriff führen.
4. **Fehlender Temperatursensor:** Die Kurve wird nicht angewendet. Der Kanal fällt auf den Rückfallwert zurück (BIOS-Automatik).
5. **Messfehler oder Verbindungsabbruch:** Nach N aufeinanderfolgenden Fehlern (Vorschlag: 3) Rückfall auf BIOS-Automatik und Status `Fehler` anzeigen.
6. **Beenden der App:** `Dispose` setzt alle gesteuerten Kanäle auf den Rückfallwert zurück. Die Rücksetzung muss auch bei Ausnahmen laufen (`try/finally`).
7. **Start der App:** Standardmäßig keine Steuerung aktiv. Eine Kurve wird nur angewendet, wenn der Nutzer sie im UI explizit aktiviert hat. Nach Neustart bleibt die Steuerung aus, bis der Nutzer sie erneut aktiviert.
8. **Hysterese:** Kleine Temperaturschwankungen dürfen den Lüfter nicht ständig umschalten (z. B. 2 °C Hysterese, 3 Sekunden Mindestabstand zwischen Schreibvorgängen).
9. **Kein Schreiben im UI-Thread**, und kein Schreiben im Sampler-Hintergrund. Eigener Hintergrundpfad oder eigener Timer im `FanControlService`.
10. **Keine Administratorrechte** ohne Notwendigkeit (siehe `neu 5.txt`, Regel 7). Falls LHM Admin-Rechte braucht, muss das dokumentiert und begründet werden.

### 5.4 Zustandsmaschine pro Kanal

```
Unbekannt ──Erkennung──► Nur überwacht
Nur überwacht ──Phase-B-Freigabe──► Steuerbar (inaktiv)
Steuerbar (inaktiv) ──Nutzer aktiviert Kurve──► Aktiv
Aktiv ──Fehler / Sensor fehlt / Beenden──► Rückfall (BIOS)
Rückfall ──Nutzer deaktiviert / Fehler behoben──► Steuerbar (inaktiv)
```

Jeder Übergang in den Zustand „Rückfall“ wird mit Grund protokolliert (Serilog, siehe Abschnitt 7).

### 5.5 Lüfterkurven
- Mindestens 2, maximal 8 Punkte, aufsteigend nach Temperatur.
- Lineare Interpolation zwischen Punkten. Unterhalb des ersten Punkts: erster Prozentwert. Oberhalb des letzten: letzter Prozentwert.
- Ungültige Kurven (doppelte Temperaturen, Werte außerhalb 0–100 °C bzw. 0–100 %) werden beim Laden verworfen, mit sichtbarer Meldung, nicht still korrigiert.
- Speicherung in JSON im selben Ordner wie `settings.json` (Muster `MonitoringSettings`), mit `SaveProfile`-ähnlicher Fehlerbehandlung und Rollback.

### 5.6 Temperaturquelle
- Der Nutzer wählt pro Kanal einen Temperatursensor (z. B. CPU Package). Die Auswahl erfolgt über eine stabile Sensor-ID, nicht über einen Index.
- Ist der Sensor nicht mehr vorhanden: Rückfall, Status anzeigen.

---

## 6. Phase D – UI

### 6.1 Ansicht „Lüftersteuerung“
- Neuer Navigationspunkt in der Sidebar (Muster wie `NavigateMonitoringCommand`).
- Pro Kanal eine Karte mit:
  - Name und Anschluss
  - Aktuelle RPM (oder „–“, wenn nicht verfügbar)
  - Aktueller Prozentwert (sofern steuerbar)
  - **Statusbadge:** „Nur überwacht“, „Steuerbar (inaktiv)“, „Aktiv“, „Rückfall (BIOS)“, „Fehler“
  - Kurvenauswahl und Aktivierungsschalter

### 6.2 Verständlichkeit
- Der Badge „Nur überwacht“ muss deutlich sichtbar sein, mit einem Satz Erklärung: „Dieser Lüfter kann nur ausgelesen werden. Die Drehzahl wird von BIOS oder Mainboard bestimmt.“
- Der Aktivierungsschalter ist nur verfügbar, wenn der Kanal `ControlVerified` ist.
- Warnhinweis vor Aktivierung: Kurzer Text, dass ein falscher Wert die Kühlung beeinflusst und dass der Rückfall jederzeit möglich ist.
- Keine Farbe als einziges Unterscheidungsmerkmal (Barrierefreiheit): Badge mit Text und Symbol.

### 6.3 Texte
Alle Texte auf Deutsch, passend zum bestehenden Stil (`Profile speichern das Aktualisierungsintervall.`).

---

## 7. Logging und Fehlerdiagnose

- Serilog ist im Plan vorgesehen, aber noch nicht eingebunden (`PHASENPLAN.md`, Phase 5). Für die Lüftersteuerung sollte es vorher eingerichtet werden, damit Rückfälle nachvollziehbar sind.
- Protokolliert werden: Erkennung der Kanäle, Aktivierung, Deaktivierung, jeder Rückfall mit Grund, jeder Schreibfehler.
- Nicht protokolliert: Pfade mit Benutzernamen, Seriennummern.

---

## 8. Tests

Alle Tests laufen ohne Hardware. Die Logik wird über Interfaces und Fakes geprüft.

### 8.1 Kurvenlogik (`FanCurveEngineTests`)
- Interpolation zwischen zwei Punkten (exakter Mittelwert, Randwerte).
- Unter- und oberhalb des Kurvenbereichs.
- Genau an einem Stützpunkt.
- Kurve mit 2 Punkten (Minimum) und mit 8 Punkten (Maximum).
- Ungültig: 1 Punkt, 9 Punkte, doppelte Temperatur, nicht aufsteigend.
- Hysterese: kleine Schwankung ändert den Wert nicht, große schon.

### 8.2 Grenzwerte
- Ausgabe wird auf Mindest- und Maximalwert geklemmt.
- `NaN`, `+Infinity`, `-Infinity`, `-1`, `101` werden abgelehnt, kein Schreibaufruf (Fake zählt Aufrufe).
- Grenzen exakt: 0, 100, Mindestwert selbst.

### 8.3 Fehlerfälle (mit Fake-Controller)
- Temperatursensor fehlt → kein Schreiben, Zustand „Rückfall“.
- Drei aufeinanderfolgende Messfehler → Rückfall. Ein einzelner Fehler → kein Rückfall.
- Schreibfehler → Rückfall und Fehlerstatus.
- `Dispose` während aktiver Steuerung → Rücksetzung auf Default wird aufgerufen, auch wenn ein Schreibvorgang eine Ausnahme wirft.
- Kanal mit `ControlVerified = false` → Aktivierung wird verweigert.

### 8.4 Persistenz (`FanCurveSettingsTests`)
- Speichern und Laden einer gültigen Kurve.
- Beschädigte JSON-Datei → Defaults plus Meldung.
- Ungültige Kurve in Datei → verworfen, Meldung.
- Schreibfehler → Rollback, `LastPersistenceError` gesetzt (Muster der bestehenden Tests mit blockierender Datei).

### 8.5 Erkennung (`FanDiscoveryServiceTests`)
- Fake-Hardware mit Fan-Sensoren gruppiert korrekt.
- Keine Fan-Sensoren → leere Liste, kein Fehler.
- Control-Sensor ohne Fan-Sensor wird nicht als Kanal gewertet.

### 8.6 Testbarkeit der bestehenden Stellen
`HardwareMetricsProvider.ReadHardware` verwirft Control-Sensoren. Für Phase A ist eine kleine Änderung nötig: Control-Sensoren mit Einheit `%` übernehmen. Dafür einen Test ergänzen.

---

## 9. Bekannte Einschränkungen des MSI H87-G43 (Stand Planung)

Diese Punkte sind **nicht verifiziert**. Sie müssen in Phase A und B geprüft werden.

- **Super-I/O-Chip:** Welcher Chip auf dem Board verbaut ist und ob LibreHardwareMonitor ihn vollständig unterstützt, ist nicht bestätigt.
- **Anschlüsse:** Das Handbuch nennt zwei CPU-Lüfteranschlüsse und drei Systemlüfteranschlüsse. Welche davon Steuersignale liefern und ob sie PWM oder Spannungsregelung nutzen, ist nicht bestätigt.
- **Steuerbarkeit:** Ob jeder Anschluss über Software schreibbar ist, ist nicht bestätigt. Auch ein vorhandener Control-Sensor garantiert das nicht.
- **BIOS-Einfluss:** Die BIOS-Einstellung (Smart Fan, Manual, Full Speed) kann Software-Vorgaben überschreiben oder umgekehrt. Das Verhalten muss getestet werden.
- **Rückfallverhalten bei Absturz:** Ob ein harter Absturz den Software-Wert zurücksetzt, ist nicht geprüft. Die BIOS-Automatik bleibt in diesem Fall die Sicherheitsebene, wenn sie korrekt konfiguriert ist.
- **Temperatursensoren:** Die Verfügbarkeit eines CPU-Package-Sensors für dieses Board ist nicht bestätigt.
- **Treiber:** LibreHardwareMonitor verwendet einen Kernel-Treiber für Hardwarezugriff. Das kann auf manchen Systemen durch Virenschutz oder Sicherheitsrichtlinien blockiert werden.
- **Fernsteuerung und Tuning:** Nicht Teil dieses Plans, bleibt gesperrt (siehe `PHASENPLAN.md`).

---

## 10. Abnahmekriterien

Die Lüftersteuerung gilt erst als fertig, wenn alle Punkte erfüllt sind:

- [ ] Phase A: Diagnosebericht vom Zielboard liegt vor
- [ ] Phase B: Jeder aktivierbare Kanal ist auf dem Board getestet und dokumentiert
- [ ] Build ohne Fehler (`dotnet build`)
- [ ] Alle Tests grün (`dotnet test`), inklusive der neuen Tests aus Abschnitt 8
- [ ] App startet, Ansicht „Lüftersteuerung“ ist sichtbar
- [ ] Nicht verifizierte Kanäle zeigen „Nur überwacht“
- [ ] Beenden der App setzt aktive Kanäle zurück (auf dem Zielboard getestet)
- [ ] Fehlender Temperatursensor führt zu Rückfall (getestet)
- [ ] Keine Schreibzugriffe außerhalb von `LhmFanController`
- [ ] Dokumentation aktualisiert: `README.md`, `PHASENPLAN.md`, `docs/hardware-verification-h87-g43.md`

---

## 11. Empfohlene Reihenfolge der Commits

Gemäß der Regel „Fertig- und Push-Regel pro Feature“ in `PHASENPLAN.md`:

1. Fan-Modelle und Erkennung (nur lesen) + Tests
2. Control-Sensoren in der Hardware-Abfrage zulassen + Test
3. Diagnoseansicht / Export
4. **Pause:** Phase B auf dem Zielboard, Ergebnis dokumentieren
5. Kurvenlogik (`FanCurveEngine`) + Tests
6. Persistenz der Kurven + Tests
7. `FanControlService` mit Zustandsmaschine, Rückfall und Tests
8. `LhmFanController` (nur für `ControlVerified`-Kanäle)
9. UI-Ansicht
10. Dokumentation und Abnahme

Bei fehlgeschlagenem Build oder Test kein Push. Bei fehlgeschlagener Phase B bleibt die Steuerung gesperrt, und die Commits 1 bis 3 bleiben als reine Überwachung bestehen.

---

## 12. Offene Fragen an den Projektverantwortlichen

1. Ist das Repository für die Umsetzung zugänglich (ZIP oder Stand mit allen Dateien)?
2. Steht auf dem Zielrechner ein .NET-10-SDK zur Verfügung, damit Build und Tests lokal laufen können?
3. Wer führt Phase B auf dem echten Board durch, und ist ein zweites Überwachungstool vorhanden?
4. Welche BIOS-Version ist auf dem H87-G43 installiert?
5. Soll die Lüfterlogik nur Kurven nutzen, oder auch feste Werte ohne Temperaturbezug?

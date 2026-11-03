# To-do-Liste – Hardware Control Center

Stand: 2026-11-02. Diese Liste ist die **abarbeitbare** Ergänzung zu
[PHASENPLAN.md](PHASENPLAN.md) (Roadmap) und
[CODE_REVIEW_TODO.md](CODE_REVIEW_TODO.md) (bereits behobene Review-Punkte).
Sie enthält nur offene Arbeit, jeweils mit Priorität, Aufwand und Abnahmekriterium.

**Legende**

| Kürzel | Bedeutung |
|---|---|
| P0 | Sicherheit / Hardware – blockiert, darf nur mit Aufsicht am echten Board erfolgen |
| P1 | Nächste Produktfunktion mit sichtbarem Nutzen |
| P2 | Qualität, Architektur, Tests |
| P3 | Dokumentation und Aufräumen |
| S / M / L | Aufwand: klein (≤ 1 h) · mittel (≤ 1 Tag) · groß (mehrere Tage) |

**Fertig-Regel (aus PHASENPLAN.md):** Ein Punkt gilt erst als erledigt, wenn
`dotnet build` und `dotnet test` grün sind, die betroffene Ansicht geprüft wurde
und der Punkt hier abgehakt ist. Hardware-Punkte brauchen zusätzlich einen
Eintrag in [docs/hardware-verification-h87-g43.md](docs/hardware-verification-h87-g43.md).

---

## P0 – Sicherheit und Hardware (nur mit Aufsicht am MSI H87-G43)

- [ ] **Phase C: Schreibpfad mit Allowlist** – `LhmFanController` bekommt
      `IControl.SetSoftware`/`SetDefault` ausschließlich für die am Board
      verifizierten Kanäle (Fan #2, Fan #4), inklusive Mindestwert, Rückkehr zu
      BIOS-Automatik in `Dispose`/`try-finally` und Tests mit Fakes.
      *Stand 2026-11-03:* Sicherheitsregeln 1/5/8 aus Plan §5.3 sind in
      `FanControlService` umgesetzt und getestet (Mindestwert 30 %, Hysterese 2 °C,
      3 s Mindestabstand, Rückfall nach 3 Schreibfehlern). Offen bleiben Allowlist,
      Schreibaufruf und Regel 9 (eigener Hintergrundpfad).
      *Abnahme:* Kein unverifizierter Kanal schreibt; Simulationstests decken
      Abbruch, Fehler und Dispose ab; Doku aktualisiert. Aufwand: M
      (siehe [docs/hardware-verification-h87-g43.md](docs/hardware-verification-h87-g43.md) §6)
- [ ] **Absturzverhalten messen** – Prozess während aktiver Software-Steuerung
      hart beenden und dokumentieren, ob der Prozentwert stehen bleibt.
      Ergebnis entscheidet, ob ein Watchdog/Timer nötig ist. Aufwand: S (am Board)
- [ ] **Anschlüsse zuordnen** – klären, ob Fan #2 = `CPUFAN1/2` und Fan #4 =
      `SYSFAN*` ist (BIOS bzw. MSI Command Center abgleichen) und im
      Verifikationsbericht festhalten. Aufwand: S
- [ ] **Fan #1/#3/#5 klären** – Lüfter anschließen oder als „nicht vorhanden“
      dokumentieren; solange 0 RPM gilt bleiben sie unverifiziert. Aufwand: S
- [ ] **Unplausible Sensoren aus Alarmen nehmen** – `Temperature #6` (91 °C laut
      Verifikationsbericht) darf keine Warnquelle sein. Vorschlag: Ausschlussliste
      in `AlertSettings` (Sensor-ID, persistiert, in den Einstellungen sichtbar)
      statt Sonderfall im Code. Aufwand: M

---

## P1 – Nächste Produktfunktionen

- [ ] **Logging und Fehlerdiagnose vervollständigen** (offener Punkt aus Phase 5).
      Neuer `DiagnosticLog` in `HCC.Infrastructure` mit Rotation: Sampler-Fehler,
      Persistenzfehler, Aufzeichnungsfehler, später Aktivierung/Rückfall der
      Lüftersteuerung – ohne Benutzernamen oder Seriennummern. Dazu ein
      „Diagnose exportieren“-Knopf in den Einstellungen.
      *Abnahme:* Logdatei mit Rotation, Pfad in der UI, Tests für Rotation und
      Schreibfehler. Aufwand: M
- [x] **Aufzeichnungen verwalten** – löschen (samt Datei) und als CSV neben dem
      Sitzungsordner exportieren; die Dashboard-Leiste meldet den CSV-Pfad.
      *Offen:* Umbenennen einer Sitzung (siehe P2) sowie eine Größenanzeige.
- [ ] **Aufzeichnung entkoppeln** – Sitzungszeilen derzeit synchron im UI-Thread
      schreiben (eine Zeile pro Messung). Auf eine kleine Hintergrund-Queue mit
      Flush beim Stoppen umstellen, damit 1-Sekunden-Intervalle nicht ruckeln.
      Aufwand: S
- [x] **Temperaturquellen auf °C filtern** – die Lüfterseite bietet nur noch
      `°C`-Sensoren an, weil `FanControlService` andere Einheiten ablehnt;
      Test `TemperaturePickerOnlyOffersCelsiusSources`.
- [ ] **Alarm-Hysterese** – Warnung erst nach zwei Messungen über der Schwelle und
      erst wieder aus, wenn der Wert z. B. 2 °C darunter liegt; verhindert
      Flackern am Schwellwert. Aufwand: S
- [ ] **Autostart** (offener Punkt aus Phase 5) – `HKCU`-Run-Eintrag, in den
      Einstellungen schaltbar, Fehler sichtbar, optional minimiert starten.
      Aufwand: M
- [ ] **GPU-Auslastung über Windows-GPU-Engine-Zähler** (optionaler Punkt aus
      PHASENPLAN) – zusätzlich zu LibreHardwareMonitor, damit die Werte mit dem
      Task-Manager vergleichbar sind. Aufwand: M

---

## P2 – Qualität, Architektur und Tests

- [ ] **Sensorliste ohne Neuaufbau aktualisieren** – `MonitoringView` nutzt ein
      `ItemsControl` ohne Virtualisierung; `MonitoringViewModel` leert und füllt
      `Sensors` bei jeder Messung neu (bei ~100 Sensoren und 1-Sekunden-Intervall
      sichtbar teuer). In-Place-Update per stabiler Sensor-ID oder virtualisierte
      Liste. *Abnahme:* Weniger Neuaufbauten, Scrollposition und Auswahl bleiben
      erhalten. Aufwand: M
- [ ] **Gleiches Muster für die Lüfterseite** – `FanChannels`/`TemperatureSensors`
      werden pro Sample neu aufgebaut; die Auswahl wird über die ID
      wiederhergestellt, flackert aber. Sinnvoll, sobald die Maßnahme oben einen
      wiederverwendbaren Helfer liefert. Aufwand: S
- [ ] **`FanControlService` entscheiden** – er ist in
      `ServiceCollectionExtensions` registriert, wird aber von keiner View
      verwendet (nur Tests). Entweder in der Lüfterseite für Status/Rückfall
      verdrahten (weiterhin gesperrt) oder Registrierung entfernen, damit kein
      ungenutzter Schreibpfad im Container hängt. Aufwand: S
- [x] **Integrationstests für die Aufzeichnung** – `DashboardRecordingTests`
      prüft auf einem STA-Thread mit gepumpter Dispatcher-Queue: Start → Sample →
      Stopp → Sitzung persistiert → Diagramm zeigt die Sitzung → zurück zu Live,
      außerdem Stopp ohne laufende Aufzeichnung.
      *Offen bleibt:* dieselbe Abdeckung für `MonitoringViewModel` und das
      In-Place-Update der Sensorliste (siehe Punkt oben).
- [ ] **Aufzeichnung umbenennen** – kleiner Nachtrag zu „Aufzeichnungen
      verwalten“: Name im Index ändern, Dateiname bleibt (ID-basiert). Aufwand: S
- [ ] **Flaky Test entschärfen** – `MetricsSamplerTests.RefreshNowAsync_CoalescesConcurrentRequestsAndReadsOnWorker`
      schlägt unter Last sporadisch fehl (Timing-Annahme im Test, isoliert grün).
      Aufwand: S
- [ ] **Dateigrößen begrenzen** – Gesamtgröße von `history.jsonl` und
      `sessions/*.jsonl` deckeln (z. B. 50 MB), inklusive Aufräumstrategie und
      Test. Aufwand: S
- [ ] **Build-Hygiene** – NU1701-Warnungen aus LiveCharts/OpenTK/SkiaSharp
      bewerten (Paketupgrade oder bewusste Unterdrückung mit Begründung);
      `TreatWarningsAsErrors` und `dotnet format --verify-no-changes` in der CI
      prüfen. Aufwand: M
- [ ] **CI erweitern** – Coverage-Artefakt zum Testlauf, damit Regressionen
      sichtbar werden; optional ein Job, der `TODO.md` auf abgehakte Punkte prüft.
      Aufwand: S
- [ ] **Barrierefreiheit** – `AutomationProperties.Name`, sinnvolle
      Tab-Reihenfolge und sichtbarer Fokus für die neuen Steuerelemente
      (Aufzeichnungsleiste, ComboBoxen, Slider). Aufwand: S
- [x] **Chart-Paints beim Theme-Wechsel aktualisieren** – `DashboardViewModel`
      erzeugt Serien-, Achsen- und Legenden-Paints jetzt themenabhängig neu
      (`ThemeSettings.Changed`), inklusive hellem Chart im Hell-Theme.

---

## P3 – Dokumentation und Aufräumen

- [ ] **`Claude-Handoff/` archivieren oder entfernen** – der Ordner ist ein
      veralteter Snapshot (behauptet u. a., Aufzeichnungen seien „planned“) und
      kann Leser in die Irre führen. Aufwand: S
- [ ] **`DEEPSEEK_IMPLEMENTATION_PLAN.md` archivieren** – der Brief ist
      abgearbeitet; nach `docs/archive/` verschieben und im README verlinken.
      Aufwand: S
- [ ] **`.OLD/README.md` prüfen** – verweist noch auf die alten To-do-Dateien;
      auf `TODO.md` umstellen. Aufwand: S
- [ ] **`docs/UI_DESIGN_GUIDE.md` ergänzen** – Aufzeichnungsleiste, neue
      Status-/Indikator-Regeln und Prüfliste um „Aufzeichnung starten/stoppen,
      Sitzung anzeigen, zurück zu Live“ erweitern. Aufwand: S
- [ ] **Widerspruch im Verifikationsbericht glätten** –
      `docs/hardware-verification-h87-g43.md` nennt in der Zusammenfassung
      „6 Lüfterkanäle“, die Tabelle zeigt 5 Kanäle + GPU-Lüfter; präzisieren.
      Aufwand: S
- [x] **Monitoring-Tabelle aufräumen** – Spaltenkopf (Gerät/Typ/Sensoren) ergänzt
      und echte Singular-/Pluralformen („1 Lüfter-Drehzahlsensor“) eingeführt.
- [ ] **README aufwerten** – Screenshot des Dashboards (inkl. Aufzeichnungsleiste)
      und Vorbereitung der Release-Notes für `0.2.0` im `CHANGELOG.md`. Aufwand: S

---

## Empfohlene Reihenfolge

1. **P1 „Logging und Fehlerdiagnose“** – liefert die Grundlage für Phase C und
   macht alle späteren Fehlerbilder nachvollziehbar.
2. **P1 „Aufzeichnungen verwalten“ + „Aufzeichnung entkoppeln“** – rundet die
   neue Funktion ab, ohne Hardware anzufassen.
3. **P2 „Sensorliste ohne Neuaufbau“** – spürbarste Verbesserung im Alltag.
4. **P1 „Temperaturquellen auf °C filtern“** und **P0 „Unplausible Sensoren“** –
   direkt danach, weil beide dieselbe Sensor-Auswahl betreffen.
5. **P0 Phase C** – erst wenn 1–4 grün sind und ein Termin mit Aufsicht am Board
   steht.

---

## Erledigt am 2026-11-02 (dieser Arbeitssprint)

- [x] Aufzeichnungssitzungen mit Start/Stopp, lokaler Persistenz und auswählbarem
      Verlauf im Dashboard (`RecordingSessionStore`, `RecordingSession`)
- [x] Fehlerbehandlung für beschädigten Index, unsichere IDs, fehlende Dateien,
      ungültige Messpunkte, Speichergrenzen (20 Sitzungen) und Absturzfall
- [x] Einstellungen scrollen, statt auf kleinen Fenstern abgeschnitten zu werden
- [x] Temperaturwarnungen nur für Sensoren mit Einheit `°C`
- [x] Gemeinsame Messpunkt-Validierung (`MetricHistoryEntry.IsValid`);
      sie wird per `[JsonIgnore]` nicht mitgeschrieben, damit Verlaufs- und
      Sitzungsdateien schlank bleiben
- [x] 20 neue Tests (125 statt 105), darunter zwei Integrationstests für den
      Aufzeichnungspfad im Dashboard; Build ohne Fehler; CHANGELOG, PHASENPLAN und
      README aktualisiert
- [x] UI-Polish Stufe 1: Blau als Markenfarbe (`#5AA7FF`), neue Palette für beide
      Themes, Chip-/Caption-Styles, aktiver Navigationszustand
      (`NavigationState` + `IsXActive`), thematisierte ScrollBar, Slider, ToolTip
- [x] UI-Polish Stufe 2: Dashboard als Kartenlayout – Metrik-Karten mit
      Hardware-Chip und Takt/Temperatur, einzeilige Aufzeichnungsleiste, Chart in
      eigener `*`-Zeile, Karten für Temperaturen, Lüfter und Systemstatus sowie
      „Erkanntes System“ in der Sidebar
- [x] `DashboardOverviewBuilder` als reine Formatierungslogik (keine Hardware,
      keine Steueraussagen) mit 8 Tests; Gesamtstand 133 Tests grün
- [x] Aufzeichnungen löschen (samt Datei) und als CSV exportieren, inklusive
      Refuse-Fälle (laufende Sitzung, unbekannte ID) und Export-Test
- [x] Lüfterseite bietet nur `°C`-Temperaturquellen (Test), Monitoring-Tabelle mit
      Spaltenkopf und echten Pluralformen
- [x] Chart folgt dem Theme: Paints für Serien, Achsen und Legende werden bei
      `ThemeSettings.Changed` neu erzeugt; Gesamtstand 138 Tests grün


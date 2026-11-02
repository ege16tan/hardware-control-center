# DeepSeek Coding Brief: Lüfterkurven für MSI H87-G43

**Aufgabe:** Implementiere die nächste vollständige Software-Stufe der Lüfterfunktion direkt in diesem Repository. Liefere Codeänderungen, keine reine Analyse oder weitere Planung.

**Zielsystem:** Windows 10/11 x64, MSI H87-G43  
**Stack:** C# / .NET 10, WPF, MVVM Toolkit 8.4.0, LibreHardwareMonitor 0.9.6, xUnit  
**Projekt:** Hardware Control Center (HCC)  
**Stand dieses Briefs:** 2. November 2026

---

## 1. Arbeitsauftrag und Grenze

Arbeite im vorhandenen Repository und folge dessen Architektur. Implementiere die Schritte aus Abschnitt 5 in Code. Erfinde keine bestätigten Hardwarefähigkeiten.

Du darfst Code für eine spätere Lüftersteuerung schreiben. Während deiner Arbeit gilt jedoch:

- Starte die WPF-Anwendung nicht.
- Führe keine Abfrage, keinen Schreibzugriff und keinen Test an realer Hardware aus.
- Rufe keine Hardware-Control-API auf. Tests müssen Fakes verwenden.
- Die Anwendung darf nach deinen Änderungen keine Lüfterkurve automatisch aktivieren.
- Solange kein am MSI H87-G43 manuell geprüfter Kanal dokumentiert ist, bleiben alle Kanäle `MonitorOnly` oder `ControlCandidate`; kein Kanal darf automatisch `ControlVerified` werden.
- Kein Commit und kein Push.

Ein Build und Tests mit Fakes sind zulässig, sofern die Umgebung sie unterstützt. Falls sie nicht laufen, schreibe das Ergebnis präzise auf, statt Erfolg zu behaupten.

---

## 2. Verbindliche Projektregeln

Lies vor der Änderung diese Dateien und behandle sie als Projektkontext:

- `.github/copilot-instructions.md` – Architektur, kleine vollständige Lösungen, Validierung und Fehlerbehandlung
- `README.md` – aktueller Produktumfang
- `PHASENPLAN.md` – erledigte und offene Produktarbeit
- `CODE_REVIEW_TODO.md` – bereits behobene Fehler und Architekturentscheidungen
- `LUEFTERSTEUERUNG_PLAN.md` – detaillierter ursprünglicher Sicherheits- und Hardwareplan

Weitere Regeln:

1. Nutze die vorhandenen MVVM-, DI- und Persistenzmuster. Hardwarezugriff darf nicht in Views, ViewModels oder im UI-Thread erfolgen.
2. Die existierende `MetricsSampler`-Instanz ist der gemeinsame Messdatenstrom. Erzeuge keinen zweiten `Computer`- oder Hardware-Polling-Dienst für dieselben Sensoren.
3. Keine neue NuGet-Abhängigkeit ohne zwingenden Grund.
4. Behandele fehlende Sensoren, beschädigte JSON-Dateien, I/O-Fehler und Hardwarefehler sichtbar und ohne stilles Überschreiben gültiger Einstellungen.
5. Hardwareunterstützung darf nicht aus dem Modellnamen, einem vorhandenen Control-Sensor oder einer Namensähnlichkeit abgeleitet werden. Diese Informationen sind Hinweise, keine Bestätigung.

---

## 3. Projekt und aktuelle Funktion

Solution: `HardwareControlCenter.sln`

- `src/HCC.Core` – gemeinsame Modelle und Interfaces
- `src/HCC.Hardware` – LibreHardwareMonitor und Hardwareabfragen
- `src/HCC.Infrastructure` – Sampling, Konfiguration und Persistenz
- `src/HCC.App` – WPF-Views, ViewModels, Navigation und DI
- `tests/HCC.Tests` – automatisierte Tests

Bereits vorhanden: Dashboard, Live-Monitoring, Sensorübersicht, Messwertverlauf, gespeicherte Monitoring-Profile, Einstellungen, Hardware-Inventar und ein gemeinsamer Hintergrund-Sampler.

Auf dem aktuellen Arbeitsbranch `codex/fan-control-phase-a` wurde bereits ein **unverifizierter, rein lesender erster Schritt** begonnen:

- `HardwareSensorReading` enthält optionale stabile Sensor-ID und Hardwaretyp.
- `MetricSnapshot` kann `FanChannels` mitführen.
- `HardwareMetricsProvider` nimmt `SensorType.Control` als Prozent-Messwert auf und erzeugt Fan-Kanäle aus demselben Messzyklus.
- `FanDiscoveryService` bildet RPM-Sensoren und eindeutige Control-Kandidaten ab.
- `MonitoringView` zeigt Lüfterwerte und den Status „Nur überwacht“ bzw. „Steuer-Sensor erkannt · nicht verifiziert“.

Prüfe diesen Code, bevor du ihn änderst. Bewahre ihn auf und verbessere ihn gezielt. Der zuletzt ausgeführte WPF-Build war erfolgreich (0 Fehler, 6 bestehende NU1701-Warnungen aus Grafikpaketen); für den neuen Code gibt es noch keinen Target-Board-Test. **Nicht verifiziert:** Super-I/O-Chip des konkreten Boards, Sensorzuordnung, Schreibfähigkeit einzelner Anschlüsse, BIOS-Verhalten und Rückfall nach Absturz.

---

## 4. Ziel

HCC soll für das **MSI H87-G43** eine verständliche Lüfterseite bekommen. Die Software soll:

- erkannte Lüfterdrehzahlen und zugehörige Hardware anzeigen;
- Steuer-Sensoren nur als Kandidaten kennzeichnen;
- Lüfterkurven erstellen, validieren, visualisieren und als Profile speichern;
- Temperaturquellen anhand stabiler Sensor-IDs auswählen;
- eine Steuerungsarchitektur mit klaren Zuständen und Rückfallpfad vorbereiten;
- echte Steuerung standardmäßig deaktiviert halten und nur über ausdrücklich verifizierte Kanäle erlauben.

Die Diagnoseansicht und Kurvenkonfiguration müssen auch dann sinnvoll funktionieren, wenn das Board nur RPM-Sensoren und keine steuerbaren Kanäle meldet.

---

## 5. Konkrete Implementierungsschritte

### Schritt A – Lesende Lüfterdiagnose vervollständigen

1. Verwende weiter ausschließlich den vorhandenen `HardwareMetricsProvider` und `MetricsSampler`.
2. Stelle sicher, dass `SensorType.Fan`, `SensorType.Control` und `SensorType.Temperature` mit Einheit, Hardwaretyp und stabiler ID in den gemeinsamen Snapshot gelangen. Die ID soll auf Hardware-Identifier plus Sensortyp und Sensorname beruhen.
3. Durchlaufe weiterhin alle `SubHardware`-Einträge.
4. Verbessere `FanDiscoveryService` so, dass eine mehrdeutige Zuordnung mehrerer Control-Sensoren **kein** Kandidat wird. Namensabgleich darf nur eine Anzeigehilfe sein.
5. Zeige in der Lüfteransicht pro Kanal mindestens: Hardware, Hardwaretyp, Name, RPM, optionale Control-Kandidaten und Capability-Status.
6. Zeige klare Zustände bei leerem Ergebnis, fehlender Hardwareinitialisierung und fehlendem Temperatursensor. Für erkannte, aber nicht verifizierte Kanäle muss die Erklärung lauten, dass BIOS/Mainboard die Drehzahl bestimmt.
7. Die Diagnose muss die Mainboard-/Super-I/O-Geräte und alle Fan-, Control- und Temperatursensoren sichtbar machen. Die vorhandene Monitoring-Sensorliste darf dafür wiederverwendet werden.

### Schritt B – Reine Kurvenlogik

Implementiere eine von Hardware und WPF unabhängige `FanCurveEngine`.

- Modell `FanCurvePoint`: Temperatur in °C und Lüfterwert in Prozent.
- Erlaubt sind 2 bis 8 Punkte.
- Temperaturen müssen endlich, zwischen 0 und 100 °C und streng aufsteigend sein; doppelte Werte sind ungültig.
- Prozentwerte müssen endlich und zwischen 0 und 100 liegen.
- Ungültige Benutzereingaben werden abgelehnt und sichtbar gemeldet; sie werden nicht still korrigiert.
- Berechne zwischen Punkten linear interpolierte Prozentwerte.
- Unterhalb des ersten Punkts gilt dessen Prozentwert; oberhalb des letzten Punkts gilt dessen Prozentwert.
- Ungültige Sensorwerte (`NaN`, Unendlich, außerhalb 0–100 °C) dürfen nie zu einem Steuerwert führen.
- Optional eine konfigurierbare Hysterese und Mindestwartezeit als reine Logik implementieren. Wenn sie nicht sauber von der Interpolation zu trennen ist, diese Erweiterung auslassen und dokumentieren.

### Schritt C – Kurvenprofile speichern

Implementiere `FanCurveSettings` nach dem Muster von `MonitoringSettings`.

- Speichere Kurven lokal im Anwendungsdatenordner von HCC als eigene JSON-Datei, ohne bestehende `settings.json`-Werte zu überschreiben.
- Profile enthalten Namen, stabile Kanal-ID, ausgewählte stabile Temperatur-Sensor-ID und Kurvenpunkte.
- Steuerung ist nach Laden eines Profils immer **inaktiv**. Ein gespeichertes Profil darf niemals beim Start angewendet werden.
- Ungültige Kurven oder beschädigtes JSON werden verworfen und mit sichtbarem Fehler behandelt.
- Verwende atomisches Schreiben und Rollback bei Speicherfehlern, analog zu `MonitoringSettings`.
- Speichere keine Seriennummern, Benutzernamen in Diagnoseausgaben oder unnötigen Hardware-Fingerprints.

### Schritt D – Lüfterseite und Kurveneditor

Ergänze eine eigene „Lüftersteuerung“-Ansicht und Navigation, passend zum bestehenden Stil.

- Pro Kanal Status mit Text und Symbol: „Nur überwacht“, „Steuer-Sensor erkannt · nicht verifiziert“, „Steuerbar · verifiziert“, „Aktiv“, „Rückfall (BIOS)“ oder „Fehler“.
- Farbe darf nicht der einzige Statushinweis sein.
- Kurveneditor mit 2–8 Punkten, Temperaturquelle, Kurvenvorschau und Speichern/Laden/Löschen von Profilen.
- Kurvenvorschau darf simulierte Eingabewerte verwenden, muss aber deutlich als Vorschau gekennzeichnet sein.
- Nicht verifizierte Kanäle dürfen keine aktivierbare Steuerung anzeigen.
- Für aktuell nicht vorhandene, unbestätigte Kanäle niemals `ControlVerified` setzen.
- Keine UI-Aktion darf direkt LibreHardwareMonitor aufrufen.

### Schritt E – Sicherheitsarchitektur für spätere Steuerung

Baue die Architektur so, dass eine spätere Aktivierung kontrolliert ergänzt werden kann. Eine reale Freigabe für das MSI H87-G43 ist in dieser Aufgabe nicht belegt.

- `IFanController` in `HCC.Core`: nur klar abgegrenzte Operationen zum Setzen und Zurücksetzen definieren.
- `LhmFanController` in `HCC.Hardware/Controllers`: einziger Ort, der überhaupt LHM-Schreib-APIs enthalten darf.
- Vor Verwendung die tatsächlichen Signaturen der installierten LibreHardwareMonitor-Version **0.9.6** aus lokalen Paketmetadaten/Quellen prüfen. API-Namen und Wertebereiche nicht raten.
- Keine Schreib-API außerhalb von `LhmFanController`.
- `FanControlService` in `HCC.Infrastructure`: Zustandsmaschine und Schutzregeln, getrennt vom Sampler und UI-Thread.
- Eine Kurve wird nie automatisch gestartet. Aktivierung erfordert eine ausdrückliche Nutzeraktion.
- Jeder Schreibpfad muss zur Laufzeit sowohl einen existierenden Control-Sensor als auch den Status `ControlVerified` verlangen. Sensorerkennung oder Name allein reichen nie.
- In diesem Arbeitsstand existiert keine verifizierte Kanal-Allowlist. Deshalb muss der Code in reinem Monitoring-Modus starten und alle Schreiboperationen verweigern. Erfinde keine Verifikationsdaten.
- Fehler, fehlender Temperaturgeber, ungültige Kurve, Sensorverlust und Dispose müssen zu einem dokumentierten Rückfallpfad führen. Ein Hardware-Rückfall kann nur behauptet werden, wenn `SetDefault` der installierten API verifiziert ist.
- `NaN`, Unendlich, negative Werte und Werte über 100 % dürfen keinen Schreibaufruf erreichen. Einen Mindestwert nicht ohne dokumentierte Prüfung als hardwareweit sicher ausgeben.
- Protokolliere Aktivierungen, Rückfälle und Fehler ohne Seriennummern oder personenbezogene Pfade.

Falls die sichere LHM-Schreibschnittstelle für 0.9.6 nicht verifiziert werden kann, implementiere Interfaces und Zustandsmaschine, lasse `LhmFanController` ohne Schreibfunktion und halte alle Kanäle gesperrt. Keine simulierte Steuerung als echte ausgeben.

---

## 6. Tests und Qualitätsprüfung

Schreibe automatisierte Tests mit Fakes, die keine Hardware öffnen oder steuern.

Mindestens abdecken:

- Kurveninterpolation, exakte Stützpunkte und Verhalten außerhalb des Kurvenbereichs;
- Minimum/Maximum der Punktanzahl, doppelte/unsortierte Temperaturen und nicht endliche Werte;
- stabile Sensor-ID, Fan-/Control-Zuordnung, mehrdeutige Zuordnung und keine Control-Sensoren;
- gültiges Speichern/Laden, beschädigtes JSON und Rollback bei Schreibfehler;
- nicht verifizierter Kanal kann nie aktiviert werden;
- fehlender Temperaturgeber und Mess-/Schreibfehler können keinen normalen Kurvenwert auslösen;
- gespeichertes Profil startet deaktiviert.

Führe – wenn die Umgebung es zulässt – `dotnet build HardwareControlCenter.sln` und `dotnet test HardwareControlCenter.sln` aus. Starte die WPF-App nicht und führe keinen physischen Hardwaretest aus. Notiere die vorhandenen NU1701-Grafikpaketwarnungen getrennt von neuen Warnungen.

---

## 7. Abnahmekriterien

- Die Solution enthält eine Lüfteransicht mit RPM, Hardwaretyp, Status und Kurvenvorschau.
- Sensorabfrage läuft weiterhin über den vorhandenen gemeinsamen Sample-Stream.
- Kurvenlogik ist unabhängig von Hardware und WPF.
- Profile überstehen Neustarts, starten aber immer inaktiv.
- Kein unbestätigter Kanal erscheint steuerbar; derzeit gibt es keine bestätigten MSI-H87-G43-Kanäle.
- Kein Hardwarezugriff aus View oder ViewModel; kein neuer Hardware-Polling-Dienst.
- Kein Start der App, keine echten Hardware-Schreibzugriffe und kein Push.
- Build-/Teststatus und Einschränkungen sind im Abschluss klar aufgeführt.

### Nicht Teil der Abnahme dieser Aufgabe

Die tatsächliche Erkennung des Super-I/O-Chips, Mapping der Lüfteranschlüsse, Änderung von BIOS-Einstellungen, Schreiben realer Fan-Werte, thermische Lasttests und Test des Absturzverhaltens. Diese Schritte erfordern später den echten MSI H87-G43 mit unabhängiger Temperaturüberwachung und müssen separat dokumentiert werden.

---

## 8. Abschlussbericht von DeepSeek

Melde nach der Implementierung kurz:

1. Welche Funktionen und Dateien geändert wurden.
2. Welche Build- und Testergebnisse vorliegen.
3. Ob `LhmFanController` echte Schreib-APIs enthält oder absichtlich gesperrt blieb und warum.
4. Welche Punkte weiterhin manuell am MSI H87-G43 verifiziert werden müssen.

Beginne jetzt mit der Codeprüfung und implementiere die Aufgaben. Stelle keine Rückfragen, wenn die sichere Voreinstellung aus diesem Brief ausreicht.

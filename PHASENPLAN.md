# Hardware Control Center – Entwicklungsphasen

## Phase 1 – Grundgerüst

- [x] WPF-Projekt und Solution einrichten
- [x] Projekte für App, Core, Hardware, Infrastructure und Tests anlegen
- [x] MVVM-Architektur und Dependency Injection einrichten
- [x] WPF-Dark-Theme und moderne Styles erstellen
- [x] Sidebar und funktionierende Navigation bauen
- [x] Dashboard mit CPU-, GPU- und RAM-Karten erstellen
- [x] Karten zunächst mit simulierten Testdaten befüllen
- [x] Startfähigkeit der Anwendung prüfen

## Danach – weitere Phasen

## Konkrete To-do-Liste

### Phase 1 abschließen

- [x] Solution ohne Fehler bauen
- [x] WPF-Anwendung unter Windows starten und Fenster, Dashboard sowie Navigation prüfen
- [x] Testprojekt ausführen und Ergebnisse dokumentieren

### Phase 2 – Echtes Monitoring

- [x] LibreHardwareMonitor anbinden und Hardware-Metriken bereitstellen
- [x] CPU-, GPU-, Speicher-, Temperatur- und Lüftersensoren ermitteln
- [x] Sensorwerte alle zwei Sekunden aktualisieren und Hardwarezugriff beim Beenden schließen
- [x] Fehlende oder nicht unterstützte Sensoren in der Oberfläche verständlich anzeigen

### Phase 3 – Verlauf und Visualisierung

- [x] LiveCharts2 integrieren und CPU-, GPU- und RAM-Verlauf anzeigen
- [x] Letzte 60 Messungen im Arbeitsspeicher puffern und Diagramme aktualisieren
- [x] Verlauf mit Zeitstempeln dauerhaft speichern

### Phase 4 – Hardwareübersicht und sichere Steuerung

- [x] Erkannte Hardware und verfügbare Sensoranzahl in einer reinen Leseansicht anzeigen
- [x] Unterstützte Lüftercontroller genauer identifizieren
- [x] Lüfterkurven mit Grenzen und Schutzmechanismen implementieren
- [ ] Steuerung nur für erkannte und unterstützte Hardware freigeben

### Phase 5 – Einstellungen und Profile

- [x] Monitoring-Intervall speichern und laden
- [x] Benannte Monitoring-Profile speichern, laden und löschen
- [ ] Autostart und weitere Herstellerunterstützung ergänzen
- [ ] Logging und Fehlerdiagnose vervollständigen

## MSI Command Center – Funktionsabgleich für H87-G43

Die MSI-Dokumentation beschreibt neben Monitoring auch Profile, CPU-Frequenz/Ratio, CPU-Lüfter im Smart-/Manual-Modus, CPU-/DRAM-Spannung und -Takt, IGP-Tuning, Game Boost/OC Genie, Warnungen, Verlauf und Mobile Control. Das H87-G43 hat zwei CPU- und drei Systemlüfteranschlüsse. Daraus folgt nicht automatisch, dass jeder Anschluss aus Windows sicher steuerbar ist.

- [x] Live-Monitoring, Hardware-/Sensorübersicht und lokal gespeicherter Verlauf
- [x] Benannte Profile für die hier unterstützten Monitoring-Einstellungen
- [x] Warnschwellen für Temperaturen mit sichtbarem Alarm
- [ ] Aufzeichnungssitzungen mit Start/Stopp und auswählbarem Verlauf
- [ ] Erkennung und sichere Steuerung unterstützter Lüfteranschlüsse
- [ ] Tuningfunktionen (CPU-/IGP-Takt, Ratio, Spannung, DRAM) nur nach Board-/CPU-Prüfung und mit Schutzgrenzen; bis dahin ausdrücklich nicht freigeben
- [ ] Mobile/Fernsteuerung erst nach eigener Authentifizierungs- und Netzwerksicherheitsplanung
- [ ] Systeminformationen und Statusanzeigen mit den MSI-Ansichten abgleichen

## Optionale Funktionen

- [ ] GPU-Auslastung zusätzlich über Windows-GPU-Engine-Zähler wie im Task-Manager erfassen, um die Werte direkt vergleichen zu können

## Erledigter Arbeitssprint – Verlauf und Aktualisierung

Ziel: den Verlauf praktisch nutzbar machen und die Aktualisierung über Einstellungen steuerbar machen.

1. [x] Projektstatus und Push-Zugriff prüfen; Sprint-Branch `codex/30min-history-settings` verwenden.
2. [x] Messpunkte mit Zeitstempel lokal speichern und beim Start wieder laden; Historie auf 7.200 Einträge begrenzen.
3. [x] Diagramm mit Zeitachse und Verlauf nach Neustart prüfen; 60 Messpunkte im Dashboard anzeigen.
4. [x] Intervall (1, 2, 5 oder 10 Sekunden) in den Einstellungen auswählbar machen und für Dashboard sowie Sensorliste gemeinsam nutzen.
5. [x] Solution bauen, Tests ausführen und App starten; Diagramm und Einstellungen sichtbar prüfen.

### Nächster Feature-Schritt – Hardwareübersicht

- [x] Geräte aus der Hardwareabfrage samt Gerätetyp und Sensoranzahl an die Oberfläche melden
- [x] Erkannte Hardware und Messwerte in getrennten Abschnitten anzeigen
- [x] Aktualisierungsstatus an das eingestellte Intervall anpassen
- [x] Tests und Build erfolgreich ausführen; App-Start und Ansicht vor dem Push prüfen

### Nächster Arbeitssprint – Monitoring-Profile

1. [x] Profile mit Namen und Aktualisierungsintervall lokal speichern.
2. [x] Profile in den Einstellungen auswählen, anwenden und löschen.
3. [x] Ungültige/leere Namen und beschädigte Profildateien sicher behandeln.
4. [x] Persistenztests (9/9), Build und App-Ansicht prüfen.
5. [x] Geprüfte Profil-Dateien separat committen und auf den Sprint-Branch pushen.

### Fertig- und Push-Regel pro Feature

- Ein Feature gilt erst als fertig, wenn Build und Tests erfolgreich sind und die betroffene Ansicht startet.
- Nach jedem fertig geprüften Feature: dessen Dateien gezielt stagen, einen eigenen Commit erstellen und `git push -u origin <sprint-branch>` ausführen.
- Bei fehlgeschlagenem Build, Test oder App-Start kein Feature-Push; den letzten lokalen Feature-Commit mit `git revert <commit>` zurücknehmen und erneut prüfen.
- Nie unbeteiligte oder bereits vorhandene Änderungen in den Feature-Commit aufnehmen. Falls GitHub-Anmeldung fehlt, lokale Commits als Rücksetzpunkte behalten und den Push-Blocker melden.

### Erledigter Arbeitssprint – Ring0-Zugang (PawnIO)

1. [x] Ursache korrigiert: LibreHardwareMonitor 0.9.6 nutzt PawnIO, nicht WinRing0.
2. [x] PawnIO 2.2.0 (offizielle signierte Edition) auf dem Zielsystem installieren und Signatur prüfen.
3. [x] App-Manifest auf `requireAdministrator` stellen; PawnIO-Gerätezugriff erlaubt nur Administratoren.
4. [x] `PawnIoDiagnostics` ergänzen: fehlender/veralteter Treiber erscheint als klarer Handlungshinweis im Snapshot-Status, mit Tests.
5. [x] Phase A erneut am Board: Status `(ok)`, Nuvoton NCT6779D mit 29 Sensoren, 5 Lüfterkanäle und Control-Werte sichtbar.
6. [ ] Phase B Schreibtest (nur mit Aufsicht, siehe `LUEFTERSTEUERUNG_PLAN.md`).

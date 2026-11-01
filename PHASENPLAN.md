# Hardware Control Center – Entwicklungsphasen

## Phase 1 – Grundgerüst

- [x] WPF-Projekt und Solution einrichten
- [x] Projekte für App, Core, Hardware, Infrastructure und Tests anlegen
- [x] MVVM-Architektur und Dependency Injection einrichten
- [x] WPF-UI integrieren und ein modernes Dark Theme erstellen
- [x] Sidebar und funktionierende Navigation bauen
- [x] Dashboard mit CPU-, GPU- und RAM-Karten erstellen
- [x] Karten zunächst mit simulierten Testdaten befüllen
- [ ] Startfähigkeit der Anwendung prüfen

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
- [ ] Verlauf mit Zeitstempeln dauerhaft speichern

### Phase 4 – Hardwareübersicht und sichere Steuerung

- [x] Erkannte Hardware und verfügbare Sensoranzahl in einer reinen Leseansicht anzeigen
- [ ] Unterstützte Lüftercontroller genauer identifizieren
- [ ] Lüfterkurven mit Grenzen und Schutzmechanismen implementieren
- [ ] Steuerung nur für erkannte und unterstützte Hardware freigeben

### Phase 5 – Einstellungen und Profile

- [ ] Profile und Monitoring-Intervall speichern und laden
- [ ] Autostart und weitere Herstellerunterstützung ergänzen
- [ ] Logging und Fehlerdiagnose vervollständigen

## Optionale Funktionen

- [ ] GPU-Auslastung zusätzlich über Windows-GPU-Engine-Zähler wie im Task-Manager erfassen, um die Werte direkt vergleichen zu können

## Nächster Arbeitssprint – ca. 30 Minuten

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

### Fertig- und Push-Regel pro Feature

- Ein Feature gilt erst als fertig, wenn Build und Tests erfolgreich sind und die betroffene Ansicht startet.
- Nach jedem fertig geprüften Feature: dessen Dateien gezielt stagen, einen eigenen Commit erstellen und `git push -u origin <sprint-branch>` ausführen.
- Bei fehlgeschlagenem Build, Test oder App-Start kein Feature-Push; den letzten lokalen Feature-Commit mit `git revert <commit>` zurücknehmen und erneut prüfen.
- Nie unbeteiligte oder bereits vorhandene Änderungen in den Feature-Commit aufnehmen. Falls GitHub-Anmeldung fehlt, lokale Commits als Rücksetzpunkte behalten und den Push-Blocker melden.

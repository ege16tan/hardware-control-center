# Code-Review: To-do und Fixstatus

Stand: 2026-11-01. Review der aktuellen Branch-Version vor neuen Produktfunktionen.

## Fehlerbehandlung und Datentreue

- [x] **Hardwarefehler dürfen die Überwachung nicht dauerhaft abschalten.** Initialisierungsfehler und Fehler einzelner Messzyklen werden getrennt; zyklische Sensorfehler verhindern spätere Leseversuche nicht.
- [x] **Nicht verfügbare Messwerte als nicht verfügbar anzeigen.** CPU/GPU/RAM bekommen Verfügbarkeitsflags; fehlende Werte erscheinen als Gedankenstrich, und der Statusindikator wechselt bei Einschränkungen von Grün auf Warnfarbe.
- [x] **Schreibfehler sichtbar machen.** Einstellungen, Profile und Verlauf melden Fehler; fehlgeschlagene Profil-/Intervalländerungen werden im Speicher zurückgerollt.
- [x] **Beschädigte Profileingaben robust laden.** Ungültige Profile und Null-Einträge werden verworfen; beschädigte JSON-Dateien führen zu Standardwerten mit sichtbarer Meldung.

## Reaktionsfähigkeit und Architektur

- [x] **Hardware-I/O aus dem WPF-UI-Thread verlagern.** Ein gemeinsamer Hintergrund-Sampler aktualisiert ViewModels über den UI-Synchronisierungskontext.
- [x] **Doppelte Sensorabfragen vermeiden.** Dashboard und Monitoring abonnieren denselben Sampler; gleichzeitig ausgelöste Leseanforderungen werden zusammengeführt.

## Tests und Projektdokumentation

- [x] Tests für Sampler-Coalescing, Hintergrundthread, Wiederanlauf nach Messfehler, Konfigurationsfehler und Verlaufsspeicherfehler ergänzen.
- [x] README und Phasenplan mit tatsächlich verwendeten Paketen und vorhandenen Features abgleichen.
- [x] Build-Warnungen untersucht und dokumentiert: sie stammen transitiv aus LiveCharts 2.0.5 (`OpenTK 3.3.1`, `OpenTK.GLWpfControl 3.3.0`, `SkiaSharp.Views.WPF 3.119.0`). Build und App-Start funktionieren, die Kompatibilitätswarnungen bleiben bis zu einem separat geprüften Paketupgrade bestehen.
- [x] `.dotnet-home/` und `restore-diagnostic.log` in `.gitignore` aufnehmen.

## Review-Notizen

- Letzter bekannter Testlauf vor diesem Review: 9 Tests erfolgreich. Die Review-Runde selbst hat keine Tests ausgeführt.
- Review-Fix-Prüfung: 15 Tests erfolgreich, WPF-Build ohne Fehler; die App wurde mit echten Messwerten gestartet.
- Resthinweis: Hardwaretreiber können synchrone Leseaufrufe blockieren; Abfragen liegen nun auf einem Workerthread. Der synchrone Treiberaufruf selbst kann nicht abgebrochen werden, daher kann das saubere Beenden bei einem hängenden Treiber weiter warten.
- Hardwaresteuerung, Overclocking und Fernsteuerung sind nicht Teil dieses Fixpakets; sie bleiben gesperrt, bis Gerätekompatibilität und Schutzmechanismen geprüft sind.

# Entwicklungsumgebung

## Bevorzugtes Werkzeug

Visual Studio Code ist der bevorzugte Editor für dieses Projekt. Die Anwendung ist eine Windows-WPF-Anwendung und kann mit dem .NET SDK über das Terminal gebaut werden.

## Benötigte Werkzeuge

| Werkzeug | Zweck | Status auf diesem Rechner |
|---|---|---|
| Visual Studio Code | Code bearbeiten; bevorzugter Editor | Launcher `code` gefunden. Erweiterungen konnten nicht geprüft werden. |
| .NET 10 SDK | WPF-Projekt erstellen, bauen und testen | Installiert – `dotnet --list-sdks` meldet .NET SDK 10.0.403. |
| Git | Versionsverwaltung | Installiert; `git --version` meldet Version 2.56.0. |
| PawnIO 2.0+ | Ring0-Zugriff für Mainboard-, Lüfter- und CPU-Kerntemperatur-Sensoren (LibreHardwareMonitor 0.9.6) | Installiert am 2026-11-02 (2.2.0, signiert). HCC.App fordert beim Start Administratorrechte an. |

Empfohlene VS-Code-Erweiterung: **C# Dev Kit** (vom Nutzer als installiert bestätigt).

Visual Studio 2026 mit der Workload **.NET-Desktopentwicklung** ist eine Alternative für die Entwicklung mit der Visual-Studio-IDE, aber nicht zusätzlich erforderlich, wenn VS Code und das .NET SDK verwendet werden. Visual Studio wurde nicht im PATH oder an den üblichen Installationspfaden gefunden; die Installation konnte daher nicht bestätigt werden.

## Ergebnis der Prüfung

Der Nutzer hat die Installation von .NET SDK 10.0.403, VS Code und C# Dev Kit bestätigt. Git war bei der ersten Prüfung verfügbar. Das SDK wurde in der aktuellen Entwicklungsumgebung ebenfalls erkannt.

## Setup-Checkliste

- [x] .NET 10 SDK installieren
- [x] VS Code öffnen und die Erweiterung C# Dev Kit installieren oder deren Installation prüfen
- [x] Git verfügbar
- [x] PawnIO 2.0+ installieren (<https://pawnio.eu/>) — Voraussetzung für Mainboard- und CPU-Kerntemperatur-Sensoren; ohne Treiber bleiben diese Sensoren leer
- [x] Danach mit `dotnet --list-sdks` prüfen, dass ein .NET 10 SDK verfügbar ist

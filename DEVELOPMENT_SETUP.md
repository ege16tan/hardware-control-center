# Entwicklungsumgebung

Hardware Control Center ist eine Windows-WPF-Anwendung (.NET 10). Bauen und
Testen funktionieren ohne angeschlossene Hardware; für echte Sensoren und
Lüftertests gelten die zusätzlichen Voraussetzungen unten.

## Benötigte Werkzeuge

| Werkzeug | Zweck | Pflicht |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Bauen und Testen; `global.json` pinnt SDK 10.0.403 | ja |
| Git | Versionsverwaltung | ja |
| Visual Studio Code + C# Dev Kit oder Visual Studio 2026 (.NET-Desktopentwicklung) | Entwicklung | empfohlen |
| [PawnIO](https://pawnio.eu/) 2.0+ (signiert) | Mainboard-, Lüfter- und CPU-Kerntemperatur-Sensoren über LibreHardwareMonitor | nur für echte Hardware |

PawnIO wird **nicht** mitgeliefert. Die App erkennt den Treiber und zeigt einen
Installationshinweis, wenn er fehlt. Der Zugriff auf das PawnIO-Gerät ist auf
Administratoren beschränkt, deshalb fordert `HCC.App.exe` beim Start
Administratorrechte an.

## Erste Schritte

```powershell
git clone https://github.com/ege16tan/hardware-control-center.git
cd hardware-control-center
dotnet restore .\HardwareControlCenter.sln
dotnet build .\HardwareControlCenter.sln
dotnet test .\HardwareControlCenter.sln
```

Anwendung starten (fragt nach Administratorrechten):

```powershell
dotnet run --project .\src\HCC.App\HCC.App.csproj
```

## Prüfen

```powershell
dotnet --list-sdks        # .NET 10 SDK vorhanden?
dotnet build .\HardwareControlCenter.sln
dotnet test .\HardwareControlCenter.sln
```

Führen Sie die Tests ohne angeschlossene Hardware aus; sie verwenden Fakes.
Echte Lüfterschreibtests nur mit Aufsicht und nach dem Protokoll in
[LUEFTERSTEUERUNG_PLAN.md](LUEFTERSTEUERUNG_PLAN.md) durchführen.

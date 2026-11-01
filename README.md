# Hardware Control Center

Hardware Control Center (HCC) is a Windows desktop application for monitoring and controlling PC hardware.

## Overview

The project aims to provide a modern WPF-based dashboard for live hardware monitoring, fan control, temperature tracking, and user-defined profiles.

## Features

- CPU, GPU, RAM, and temperature monitoring
- Fan speed and RPM display
- Custom performance profiles
- Dashboard with live charts
- Modular architecture for multiple hardware vendors

## Tech Stack

- C# / .NET 10
- WPF
- WPF-UI
- MVVM Toolkit
- LibreHardwareMonitor
- LiveCharts2
- Serilog

## Project Status

The WPF dashboard includes live CPU/GPU/RAM readings and a chart of the last 60 measurements. CPU/GPU load comes from available LibreHardwareMonitor sensors, while RAM usage comes from Windows. The Monitoring page lists detected temperature, fan, clock, voltage, power, load, and memory readings. Persistent history and Task Manager-compatible GPU engine counters remain planned.

## Run and test

On Windows with the .NET 10 SDK installed, run the app from the repository folder:

```powershell
dotnet run --project .\src\HCC.App\HCC.App.csproj
dotnet test .\HardwareControlCenter.sln
```

The Dashboard updates every two seconds. The Monitoring page also has a manual refresh button.

See [PHASENPLAN.md](PHASENPLAN.md) for the development roadmap.

# Hardware Control Center

Hardware Control Center (HCC) is a Windows desktop application for monitoring PC hardware. Fan control and hardware tuning are planned and are not currently available.

## Overview

The project aims to provide a modern WPF-based dashboard for live hardware monitoring, fan control, temperature tracking, and user-defined profiles.

## Features

- Live CPU, GPU, RAM, temperature, clock, voltage, power, and fan-speed readings when supported by the hardware
- Detected hardware and sensor inventory
- Dashboard with a locally persisted performance history
- Named monitoring profiles for the refresh interval
- Fan diagnostics and a curve editor with saved, always-inactive profiles
- Configurable temperature warning threshold with a visible alarm
- Dark and light themes with instant switching and persisted selection
- Modular architecture intended to support additional hardware vendors

## Tech Stack

- C# / .NET 10
- WPF
- MVVM Toolkit
- Microsoft.Extensions.DependencyInjection
- LibreHardwareMonitor
- LiveCharts2
- System.Text.Json
- xUnit

## Requirements

- Windows 10/11 (x64)
- [PawnIO](https://pawnio.eu/) 2.0 or newer. LibreHardwareMonitor 0.9.6 reads motherboard fans, board temperatures and CPU core temperatures through this signed kernel driver. Without it those sensors stay empty and the app explains how to install it.
- Administrator rights: the PawnIO device only allows SYSTEM and Administrators, so `HCC.App.exe` requests elevation (`src/HCC.App/app.manifest`).

## Project Status

The WPF dashboard reads CPU/GPU/RAM metrics through a shared background sampler and charts the last 60 valid samples. Up to 7,200 samples are stored locally. CPU/GPU load comes from available LibreHardwareMonitor sensors, while RAM usage comes from Windows. The Monitoring page lists detected devices and their supported readings. Motherboard sensors are read when PawnIO is installed: on the MSI H87-G43 the Nuvoton NCT6779D Super-I/O reports five fan headers and board temperatures. Profiles save the monitoring interval and fan curves; a configurable temperature threshold shows a visible alarm. The theme can be switched between dark and light under Settings and is restored on startup. Real fan control remains locked until a channel is verified on the target board. Tuning, recording sessions, and Task-Manager-compatible GPU engine counters remain planned.

## Run and test

On Windows with the .NET 10 SDK installed, run the app from the repository folder:

```powershell
dotnet run --project .\src\HCC.App\HCC.App.csproj
dotnet test .\HardwareControlCenter.sln
```

The Dashboard and Monitoring page use the configured interval (1, 2, 5, or 10 seconds). Both views share the same background sample. Manual refresh is available on both pages.

See [PHASENPLAN.md](PHASENPLAN.md) for the development roadmap and [docs/UI_DESIGN_GUIDE.md](docs/UI_DESIGN_GUIDE.md) for the theme and UI conventions.

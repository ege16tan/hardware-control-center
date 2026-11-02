# Hardware Control Center

[![CI](https://github.com/ege16tan/hardware-control-center/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ege16tan/hardware-control-center/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Hardware Control Center (HCC) is a Windows desktop application for live hardware
monitoring, sensor diagnostics and carefully staged fan control. It is built
with C# / .NET 10 and WPF, reads hardware through LibreHardwareMonitor, and was
verified on an MSI H87-G43 (Nuvoton NCT6779D Super-I/O).

Fan control is **not enabled by default**. A channel may only be controlled
after it was verified on the real board (see [Safety](#safety)).

## Highlights

- Live CPU, GPU, RAM, temperature, clock, voltage, power and fan-speed readings
- Detected hardware and sensor inventory, including Super-I/O devices
- Dashboard with locally persisted performance history
- Named monitoring profiles for the refresh interval
- Fan diagnostics and a curve editor with saved, always-inactive profiles
- Configurable temperature warning threshold with a visible alarm
- Dark and light themes with instant switching and persisted selection
- Explicit driver diagnostics when PawnIO is missing or outdated

## Verified target system

| Component | Value |
|---|---|
| Mainboard | MSI H87-G43 (MS-7816) |
| Super-I/O | Nuvoton NCT6779D, 29 sensors |
| CPU | Intel Core i7-4770 |
| GPU | NVIDIA GeForce GTX 1060 6GB |
| OS | Windows 11 x64, HVCI enabled |

Phase A (reading) is passed: five fan channels with RPM and control values are
visible. Phase B (writing) was tested with supervision: Fan #2 and Fan #4
respond to software control and fall back to BIOS automation; Fan #1/#3/#5
showed no fan response. See
[hardware-verification-h87-g43.md](docs/hardware-verification-h87-g43.md).

## Requirements

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) for building from source
  (the repository pins `10.0.403` in `global.json`)
- [PawnIO](https://pawnio.eu/) 2.0 or newer. LibreHardwareMonitor 0.9.6 reads
  motherboard fans, board temperatures and CPU core temperatures through this
  signed kernel driver. Without it those sensors stay empty and the app shows
  an install hint. PawnIO is **not** bundled with HCC.
- Administrator rights: the PawnIO device only allows SYSTEM and
  Administrators, so `HCC.App.exe` requests elevation
  ([app.manifest](src/HCC.App/app.manifest)).

## Getting started

```powershell
git clone https://github.com/ege16tan/hardware-control-center.git
cd hardware-control-center
dotnet run --project .\src\HCC.App\HCC.App.csproj
```

Build and test without starting the UI:

```powershell
dotnet build .\HardwareControlCenter.sln
dotnet test .\HardwareControlCenter.sln
```

The first start asks for elevation. The dashboard and monitoring page use the
configured interval (1, 2, 5 or 10 seconds) and share one background sample.

## Project structure

```
src/HCC.App             WPF views, view models, theming, DI composition
src/HCC.Core            models and interfaces shared by all layers
src/HCC.Hardware        LibreHardwareMonitor access, fan discovery, PawnIO diagnostics
src/HCC.Infrastructure  sampling, fan-control service, configuration, persistence
tests/HCC.Tests         xUnit tests (fakes, no real hardware)
docs/                   verification reports, driver notes, UI guide
```

Hardware I/O runs on a background sampler; views and view models never touch
LibreHardwareMonitor directly. See
[CONTRIBUTING.md](CONTRIBUTING.md) for the architecture rules.

## Safety

Fan control touches real hardware and can overheat a system if used carelessly.
The project therefore follows strict stages:

1. **Read** – discovery and display only.
2. **Verify** – a channel must show a measurable RPM reaction on the target
   board, and the fallback to BIOS automation must be confirmed.
3. **Control** – only verified channels may be written.

Currently no channel is unlocked in the application code (`LhmFanController`
refuses all writes). Even after unlocking, a process crash can leave the last
software value active until the next reboot, because LibreHardwareMonitor does
not reset software control on exit. The BIOS automatic mode remains the
safety net. Do not disable Secure Boot, HVCI or the driver blocklist to work
around a missing driver.

## Documentation

- [LUEFTERSTEUERUNG_PLAN.md](LUEFTERSTEUERUNG_PLAN.md) – fan-control phases A–D
- [docs/hardware-verification-h87-g43.md](docs/hardware-verification-h87-g43.md) – measured results, Phase A/B
- [docs/ring0-treiber-integration.md](docs/ring0-treiber-integration.md) – PawnIO integration and residual risks
- [PHASENPLAN.md](PHASENPLAN.md) – project roadmap
- [DEVELOPMENT_SETUP.md](DEVELOPMENT_SETUP.md) – development environment
- [docs/UI_DESIGN_GUIDE.md](docs/UI_DESIGN_GUIDE.md) – themes and UI conventions

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) first, and
keep the hardware-safety rules intact. Security issues: see
[SECURITY.md](SECURITY.md).

## License and credits

Released under the [MIT License](LICENSE). Third-party components and their
licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

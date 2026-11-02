# Contributing to Hardware Control Center

Thanks for taking a look. This file explains how to build, test and change the
project without breaking its hardware-safety guarantees.

## Prerequisites

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download); `global.json` pins the
  SDK feature band
- [PawnIO](https://pawnio.eu/) 2.0+ and administrator rights only if you want to
  read motherboard sensors or test fan control on real hardware
- Git

## Build and test

```powershell
dotnet restore .\HardwareControlCenter.sln
dotnet build .\HardwareControlCenter.sln
dotnet test .\HardwareControlCenter.sln
```

Tests must run without real hardware: use fakes for `IMetricsProvider`,
`IFanController` and sensor data. Do not start the WPF app or write fan values
as part of automated tests.

## Architecture rules

- `HCC.Core` holds models and interfaces and depends on nothing.
- `HCC.Hardware` is the only layer that talks to LibreHardwareMonitor.
- `HCC.Infrastructure` owns sampling, fan-curve logic, configuration and the
  fan-control state machine; it must stay testable without hardware.
- `HCC.App` contains WPF views and view models. No hardware access from views or
  view models, no hardware polling outside the shared `MetricsSampler`.
- All LHM write calls belong to `LhmFanController`. No other class may call
  `ISensor.Control.SetSoftware` or `SetDefault`.
- Prefer the smallest complete solution over new abstractions. Follow the
  existing MVVM, dependency-injection and JSON persistence patterns.

## Hardware safety rules

These are not negotiable for pull requests:

- Never mark a fan channel as verified without a documented Phase B test on the
  target board (measurable RPM reaction and confirmed fallback to BIOS mode).
- Never enable fan writes by default; control must require an explicit user
  action after every application start.
- Invalid values (`NaN`, infinity, outside range) must never reach a write call.
  Clamp or reject them; document the minimum safe percentage.
- Resetting to BIOS automatic mode must run in `try`/`finally` and during
  `Dispose`. A failing reset must be visible, not silent.
- Do not weaken security measures (Secure Boot, HVCI, driver blocklists) to make
  a driver load.

## Commits and branches

- Work on a topic branch and open a pull request against `main`.
- Use [Conventional Commits](https://www.conventionalcommits.org/):
  `feat:`, `fix:`, `docs:`, `test:`, `chore:`, `refactor:`.
- Keep a pull request focused; do not mix unrelated formatting changes with
  behaviour changes.
- Update the relevant documentation (`docs/`, `PHASENPLAN.md`,
  `LUEFTERSTEUERUNG_PLAN.md`) when a change affects the verified state.

## Pull request checklist

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` succeeds
- [ ] No hardware write path was enabled without a documented board test
- [ ] Documentation updated where behaviour or verification status changed
- [ ] No secrets, local paths or personal data added

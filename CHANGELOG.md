# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Dashboard recording sessions: name, start and stop a recording of the live
  samples; every session is stored locally and can be selected and shown in the
  performance chart, with a "Live" switch back to the current stream
- Shared sample validation (`MetricHistoryEntry.IsValid`) so history and
  recordings accept exactly the same measurements
- Dashboard overview cards for temperatures, fans and system status, plus the
  detected-system card in the sidebar
- Hardware chips and clock/temperature context on the CPU, GPU and RAM cards
- Recording sessions can be deleted (including their file) and exported as CSV
  next to the sessions folder
- The hardware table on the monitoring page has a header row
- PawnIO detection and an actionable status message when the driver is missing
  or outdated
- Verified-hardware report for the MSI H87-G43 / Nuvoton NCT6779D

### Changed

- New palette for both themes: blue accent `#5AA7FF`, cooler surfaces
  (`#0B0E14`, `#121722`, `#252E3D`), warmer status colors, plus chip, navigation
  and status tokens shared by dark and light
- The sidebar marks the active page instead of looking identical on every page
- Dashboard uses a card layout: metric cards, a full-width recording strip and a
  content row with the chart in its own star row next to temperatures, fans and
  system status
- Fan page labels the sensor picker "Temperaturquelle" instead of exposing the
  internal sensor-ID wording
- Chart series, axis and legend paints follow the theme, so the light theme gets
  a light chart with dark labels instead of a dark block
- Fan page uses one selection card per fan channel (name, RPM, control value)
  with a compact per-channel curve editor; all channels including the GPU fan
  are visible without scrolling
- Settings card is sized to its content instead of stretching across the window

### Fixed

- Scroll bars, sliders and tool tips follow the theme instead of rendering in the
  default light style inside the dark theme
- The dashboard status line wraps instead of running out of the window, and the
  chart keeps a readable axis because it no longer shares a stack panel
- The fan page only offers temperature sources in °C; a curve profile could
  previously point at a sensor the controller rejects at runtime
- Fan-control safety limits from `LUEFTERSTEUERUNG_PLAN.md` §5.3 are enforced
  before any write: minimum 30 %, 2 °C hysteresis, 3 s between writes and
  fallback after three failed writes (`FanControlDefaults`), each covered by a
  test; `LhmFanController` still refuses every write
- German plurals on the monitoring page ("1 Lüfter-Drehzahlsensor" instead of
  "Sensor(en)")
- The settings page scrolls instead of clipping the warning and theme sections
  on small windows
- Temperature warnings only trigger for sensors that really report `°C`; sensors
  with other units can no longer raise a false alarm
- The settings button no longer stretches over the whole sidebar height
  (`DockPanel` last-child fill)

## [0.1.0] - 2026-11-02

### Added

- WPF dashboard with live CPU, GPU and RAM monitoring, persisted history and
  configurable refresh interval
- Hardware and sensor inventory, including Super-I/O devices and fan channels
- Fan diagnostics, fan-curve editor and saved, always-inactive curve profiles
- Temperature warning threshold with visible alarm
- Dark and light themes with persisted selection
- PawnIO driver diagnostics and administrator manifest for board sensors
- Phase A verification (29 Super-I/O sensors, five fan channels) and Phase B
  write test with supervised fallback to BIOS automation on the MSI H87-G43

### Changed

- Root cause corrected: LibreHardwareMonitor 0.9.6 uses PawnIO instead of the
  removed WinRing0 driver

### Security

- The application requests administrator rights because the PawnIO device is
  restricted to SYSTEM and Administrators

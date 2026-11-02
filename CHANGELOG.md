# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- PawnIO detection and an actionable status message when the driver is missing
  or outdated
- Verified-hardware report for the MSI H87-G43 / Nuvoton NCT6779D

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

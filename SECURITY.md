# Security Policy

## Supported versions

Only the latest commit on `main` is supported. This is a personal desktop
application, not a service.

## Reporting a vulnerability

Please do **not** open a public issue for security problems. Use GitHub's
private vulnerability reporting:

1. Open the repository's **Security** tab.
2. Click **Report a vulnerability**.
3. Describe the problem, affected version/commit, and reproduction steps.

You can expect an acknowledgement within a few days. Please give the project
time to fix the issue before publishing details.

## Scope

Relevant vulnerabilities include:

- privilege-escalation or code-execution issues in the application
- unsafe handling of the PawnIO device or kernel-driver access
- fan-control paths that could write unexpected values or fail to restore BIOS
  automation
- injection or path-traversal issues in settings/history/profile persistence

Out of scope:

- vulnerabilities in PawnIO itself – report those to the
  [PawnIO project](https://github.com/namazso/PawnIO)
- vulnerabilities in LibreHardwareMonitor – report those to the
  [LibreHardwareMonitor project](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- issues that require disabling Secure Boot, HVCI or the driver blocklist

## Hardware risk

Fan control is intentionally locked in the application code. Do not run
modified builds that write fan values without supervision; a wrong value or a
crash can leave a fan stopped until the next reboot. The BIOS automatic mode is
the safety layer.

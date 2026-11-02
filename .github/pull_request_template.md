## Summary

<!-- What changes, and why? -->

## Changes

<!-- Bullet list of the important changes. -->

## Testing

- [ ] `dotnet build HardwareControlCenter.sln`
- [ ] `dotnet test HardwareControlCenter.sln`
- [ ] Hardware test performed (describe board, channels, observations) or not applicable

## Hardware safety

- [ ] This pull request does not enable fan writes for new channels without a documented Phase B test
- [ ] Writes remain gated by verified channels and a minimum safe percentage
- [ ] Fallback to BIOS automatic mode still runs in `Dispose`/`try`/`finally`

## Checklist

- [ ] Documentation updated (`docs/`, `PHASENPLAN.md`, `LUEFTERSTEUERUNG_PLAN.md`) if verification status changed
- [ ] No secrets, local paths or personal data added
- [ ] Linked issue(s): <!-- e.g. Closes #12 -->

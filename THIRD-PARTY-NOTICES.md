# Third-Party Notices and Credits

Hardware Control Center is built on the work of these projects. Thank you!

## Runtime dependencies

| Component | License | Used for | Link |
|---|---|---|---|
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 | Hardware sensors, Super-I/O access, PawnIO modules | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| [PawnIO](https://pawnio.eu/) by namazso | GPL-2.0-or-later with IOCTL linking exception | Signed kernel driver that executes the hardware-access modules | https://github.com/namazso/PawnIO |
| [.NET](https://dotnet.microsoft.com/) | MIT | Runtime and platform | https://github.com/dotnet/runtime |
| [MVVM Toolkit](https://github.com/CommunityToolkit/dotnet) | MIT | MVVM helpers | https://github.com/CommunityToolkit/dotnet |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | MIT | Dependency injection | https://github.com/dotnet/runtime |
| [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2) | MIT | History charts | https://github.com/beto-rodriguez/LiveCharts2 |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | Rendering backend for charts | https://github.com/mono/SkiaSharp |

## Build and test dependencies

| Component | License | Used for | Link |
|---|---|---|---|
| [xUnit](https://xunit.net/) | Apache-2.0 | Unit tests | https://github.com/xunit/xunit |
| [OpenTK](https://github.com/opentk/opentk) / [GLWpfControl](https://github.com/amerkoleci/GLWpfControl) | MIT | Transitive dependencies of LiveCharts WPF | https://github.com/opentk/opentk |

## Notes

- **PawnIO is not distributed with HCC.** The application only detects the
  installed driver and shows an install hint. The driver is installed by the
  user from the official PawnIO release.
- LibreHardwareMonitor ships its PawnIO modules as embedded resources under
  LGPL-2.1-or-later; they remain part of the LibreHardwareMonitor package.
- This project is not affiliated with MSI, Nuvoton, namazso, or the
  LibreHardwareMonitor maintainers.

If you believe a credit is missing or a license is stated incorrectly, please
open an issue.

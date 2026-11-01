using System.Runtime.InteropServices;
using HCC.Core.Interfaces;
using HCC.Core.Models;
using LibreHardwareMonitor.Hardware;

namespace HCC.Hardware.Monitoring;

public sealed class HardwareMetricsProvider : IMetricsProvider, IDisposable
{
    private readonly object _sync = new();
    private string? _initializationError;
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true,
    };

    public HardwareMetricsProvider()
    {
        try { _computer.Open(); }
        catch (Exception ex) { _initializationError = ex.Message; }
    }

    public MetricSnapshot ReadCurrent()
    {
        lock (_sync)
        {
            var sensors = new List<HardwareSensorReading>();
            var devices = new List<HardwareDeviceReading>();
            double? cpuLoad = null;
            double? gpuLoad = null;
            string? gpuLoadSensorName = null;
            var gpuLoadSensorScore = 0;
            string? hardwareReadError = null;

            if (_initializationError is null)
            {
                foreach (var hardware in _computer.Hardware)
                {
                    try { ReadHardware(hardware, sensors, devices, ref cpuLoad, ref gpuLoad, ref gpuLoadSensorName, ref gpuLoadSensorScore); }
                    catch (Exception ex) { hardwareReadError ??= $"{hardware.Name}: {ex.Message}"; }
                }
            }

            var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref memory))
                return new MetricSnapshot(cpuLoad ?? 0, gpuLoad ?? 0, 0, 0, 0, sensors,
                    Status: "Windows-Speicherwerte nicht verfügbar",
                    GpuLoadSensorName: gpuLoadSensorName,
                    HardwareDevices: devices,
                    CpuAvailable: cpuLoad.HasValue,
                    GpuAvailable: gpuLoad.HasValue,
                    MemoryAvailable: false);

            var totalGb = memory.TotalPhysical / 1024d / 1024 / 1024;
            var availableGb = memory.AvailablePhysical / 1024d / 1024 / 1024;
            var usedGb = totalGb - availableGb;
            sensors.Add(new HardwareSensorReading("Arbeitsspeicher", "Auslastung", "Load", memory.MemoryLoad, "%"));
            sensors.Add(new HardwareSensorReading("Arbeitsspeicher", "Belegt", "Data", usedGb, "GB"));
            sensors.Add(new HardwareSensorReading("Arbeitsspeicher", "Verfügbar", "Data", availableGb, "GB"));
            return new MetricSnapshot(cpuLoad ?? 0, gpuLoad ?? 0, memory.MemoryLoad, usedGb, totalGb, sensors,
                Status: _initializationError is not null ? $"LibreHardwareMonitor konnte nicht gestartet werden: {_initializationError}"
                    : hardwareReadError is not null ? $"Ein Hardwaregerät konnte nicht gelesen werden: {hardwareReadError}"
                    : cpuLoad is null && gpuLoad is null ? "CPU-/GPU-Auslastung wird von dieser Hardware nicht gemeldet"
                    : cpuLoad is null || gpuLoad is null ? "Ein CPU- oder GPU-Auslastungssensor wird nicht gemeldet"
                    : null,
                GpuLoadSensorName: gpuLoadSensorName,
                HardwareDevices: devices,
                CpuAvailable: cpuLoad.HasValue,
                GpuAvailable: gpuLoad.HasValue);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            try { _computer.Close(); } catch { /* A failed initialization has nothing to close. */ }
        }
    }

    private static void ReadHardware(IHardware hardware, List<HardwareSensorReading> readings, List<HardwareDeviceReading> devices, ref double? cpuLoad, ref double? gpuLoad, ref string? gpuLoadSensorName, ref int gpuLoadSensorScore)
    {
        hardware.Update();
        devices.Add(new HardwareDeviceReading(hardware.Name, hardware.HardwareType.ToString(), hardware.Sensors.Count(s => s.Value is not null)));
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value) continue;
            var unit = sensor.SensorType switch
            {
                SensorType.Load => "%",
                SensorType.Temperature => "°C",
                SensorType.Fan => "RPM",
                SensorType.Clock => "MHz",
                SensorType.Voltage => "V",
                SensorType.Power => "W",
                _ => ""
            };
            if (unit.Length == 0) continue;
            readings.Add(new HardwareSensorReading(hardware.Name, sensor.Name, sensor.SensorType.ToString(), value, unit));
            if (sensor.SensorType != SensorType.Load) continue;
            if (hardware.HardwareType == HardwareType.Cpu && (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) || cpuLoad is null)) cpuLoad = value;
            if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            {
                var score = GetGpuLoadSensorScore(sensor.Name);
                if (score > gpuLoadSensorScore)
                {
                    gpuLoad = value;
                    gpuLoadSensorName = sensor.Name;
                    gpuLoadSensorScore = score;
                }
            }
        }
        foreach (var child in hardware.SubHardware) ReadHardware(child, readings, devices, ref cpuLoad, ref gpuLoad, ref gpuLoadSensorName, ref gpuLoadSensorScore);
    }

    private static int GetGpuLoadSensorScore(string name)
    {
        if (name.Contains("Memory", StringComparison.OrdinalIgnoreCase) || name.Contains("Video", StringComparison.OrdinalIgnoreCase) || name.Contains("Bus", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase)) return 100;
        if (name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase)) return 90;
        if (name.Contains("3D", StringComparison.OrdinalIgnoreCase)) return 80;
        if (name.Contains("Graphics", StringComparison.OrdinalIgnoreCase)) return 70;
        if (name.Contains("Total", StringComparison.OrdinalIgnoreCase)) return 60;
        return 10;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}

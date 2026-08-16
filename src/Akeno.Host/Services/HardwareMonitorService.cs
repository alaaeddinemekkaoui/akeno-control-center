using LibreHardwareMonitor.Hardware;

namespace Akeno.Host.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer? _computer;
    private readonly object _gate = new();

    public HardwareMonitorService()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };
            _computer.Open();
        }
        catch
        {
            _computer = null;
        }
    }

    public HardwareSnapshot Read()
    {
        lock (_gate)
        {
            if (_computer is null)
                return HardwareSnapshot.Fallback();

            try
            {
                float? cpuLoad = null, cpuTemp = null, gpuLoad = null, gpuTemp = null, memoryLoad = null;
                float? networkDown = null, networkUp = null;
                string? gpuName = null;

                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var sub in hardware.SubHardware) sub.Update();
                    var sensors = hardware.Sensors.Concat(hardware.SubHardware.SelectMany(x => x.Sensors));

                    switch (hardware.HardwareType)
                    {
                        case HardwareType.Cpu:
                            cpuLoad = First(sensors, SensorType.Load, "CPU Total") ?? Max(sensors, SensorType.Load);
                            cpuTemp = Max(sensors, SensorType.Temperature);
                            break;
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuIntel:
                            gpuName ??= hardware.Name;
                            gpuLoad = First(sensors, SensorType.Load, "GPU Core") ?? Max(sensors, SensorType.Load);
                            gpuTemp = Max(sensors, SensorType.Temperature);
                            break;
                        case HardwareType.Memory:
                            memoryLoad = Max(sensors, SensorType.Load);
                            break;
                        case HardwareType.Network:
                            networkDown = Math.Max(networkDown ?? 0, Max(sensors, SensorType.Throughput, "Download") ?? 0);
                            networkUp = Math.Max(networkUp ?? 0, Max(sensors, SensorType.Throughput, "Upload") ?? 0);
                            break;
                    }
                }

                return new HardwareSnapshot(
                    true,
                    Round(cpuLoad, 0), Round(cpuTemp, 0),
                    Round(gpuLoad, 0), Round(gpuTemp, 0), gpuName ?? "GPU",
                    Round(memoryLoad, 0),
                    RoundMbps(networkDown), RoundMbps(networkUp),
                    Environment.MachineName,
                    Environment.OSVersion.VersionString);
            }
            catch
            {
                return HardwareSnapshot.Fallback();
            }
        }
    }

    private static float? First(IEnumerable<ISensor> sensors, SensorType type, string contains) =>
        sensors.FirstOrDefault(s => s.SensorType == type && s.Name.Contains(contains, StringComparison.OrdinalIgnoreCase))?.Value;

    private static float? Max(IEnumerable<ISensor> sensors, SensorType type, string? contains = null)
    {
        var values = sensors
            .Where(s => s.SensorType == type && (contains is null || s.Name.Contains(contains, StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private static double Round(float? value, double fallback) => value.HasValue ? Math.Round(value.Value, 1) : fallback;
    private static double RoundMbps(float? bytesPerSecond) => bytesPerSecond.HasValue ? Math.Round(bytesPerSecond.Value * 8d / 1_000_000d, 1) : 0;

    public void Dispose()
    {
        try { _computer?.Close(); } catch { }
    }
}

public sealed record HardwareSnapshot(
    bool Available,
    double CpuUsage,
    double CpuTemperature,
    double GpuUsage,
    double GpuTemperature,
    string GpuName,
    double RamUsage,
    double DownloadMbps,
    double UploadMbps,
    string Host,
    string Os)
{
    public static HardwareSnapshot Fallback() => new(false, 34, 52, 67, 62, "GPU", 42, 0, 0, Environment.MachineName, Environment.OSVersion.VersionString);
}

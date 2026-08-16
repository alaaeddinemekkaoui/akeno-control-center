namespace Akeno.Host.Services;

public sealed class TelemetryService
{
    private readonly Random _random = new();
    public object GetSnapshot() => new
    {
        cpu = new { usage = Jitter(34, 7, 5, 95), temperature = Jitter(52, 3, 30, 90) },
        gpu = new { usage = Jitter(67, 8, 5, 99), temperature = Jitter(62, 4, 30, 95), fps = Jitter(144, 18, 30, 240) },
        ram = new { usage = Jitter(42, 4, 5, 95) },
        network = new { downMbps = Jitter(836, 90, 0, 1000), pingMs = Jitter(12, 4, 3, 100) },
        system = new { status = "Stable", host = Environment.MachineName, platform = Environment.OSVersion.Platform.ToString() }
    };
    private int Jitter(int center, int range, int min, int max) => Math.Clamp(center + _random.Next(-range, range + 1), min, max);
}

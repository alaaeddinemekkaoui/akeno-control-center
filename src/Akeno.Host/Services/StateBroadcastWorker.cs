using Akeno.Host.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Akeno.Host.Services;

public sealed class StateBroadcastWorker : BackgroundService
{
    private readonly ILogger<StateBroadcastWorker> _logger;
    private readonly ComponentEngineService _engine;
    private readonly WindowsAudioService _audio;
    private readonly HardwareMonitorService _hardware;
    private readonly IHubContext<ControlHub> _hub;

    public StateBroadcastWorker(
        ILogger<StateBroadcastWorker> logger,
        ComponentEngineService engine,
        WindowsAudioService audio,
        HardwareMonitorService hardware,
        IHubContext<ControlHub> hub)
    {
        _logger = logger;
        _engine = engine;
        _audio = audio;
        _hardware = hardware;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("State broadcast worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var audio = _audio.GetSnapshot();
                _engine.SetState("master.volume", audio.OutputVolume, audio.OutputAvailable, audio.OutputAvailable ? null : "Output device unavailable.");
                _engine.SetState("master.muted", audio.OutputMuted, audio.OutputAvailable, audio.OutputAvailable ? null : "Output device unavailable.");
                _engine.SetState("mic.volume", audio.MicVolume, audio.MicAvailable, audio.MicAvailable ? null : "Microphone unavailable.");
                _engine.SetState("mic.muted", audio.MicMuted, audio.MicAvailable, audio.MicAvailable ? null : "Microphone unavailable.");

                var hardware = _hardware.Read();
                _engine.SetState("system.cpu.usage", hardware.CpuUsage, hardware.Available, hardware.Available ? null : "CPU sensor unavailable.");
                _engine.SetState("system.cpu.temperature", hardware.CpuTemperature, hardware.Available, hardware.Available ? null : "CPU temperature sensor unavailable.");
                _engine.SetState("system.gpu.usage", hardware.GpuUsage, hardware.Available, hardware.Available ? null : "GPU sensor unavailable.");
                _engine.SetState("system.gpu.temperature", hardware.GpuTemperature, hardware.Available, hardware.Available ? null : "GPU temperature sensor unavailable.");
                _engine.SetState("system.ram.usage", hardware.RamUsage, hardware.Available, hardware.Available ? null : "RAM sensor unavailable.");
                _engine.SetState("system.network.down", hardware.DownloadMbps, hardware.Available, hardware.Available ? null : "Network sensor unavailable.");
                _engine.SetState("system.network.up", hardware.UploadMbps, hardware.Available, hardware.Available ? null : "Network sensor unavailable.");

                await _hub.Clients.All.SendAsync("componentChanged", _engine.GetStates(), stoppingToken);
                await _hub.Clients.All.SendAsync("telemetryChanged", new
                {
                    cpu = new { usage = hardware.CpuUsage, temperature = hardware.CpuTemperature },
                    gpu = new { usage = hardware.GpuUsage, temperature = hardware.GpuTemperature, name = hardware.GpuName },
                    ram = new { usage = hardware.RamUsage },
                    network = new { downMbps = hardware.DownloadMbps, upMbps = hardware.UploadMbps },
                    system = new { status = hardware.Available ? "PC CONNECTED" : "HOST OFFLINE", host = hardware.Host, platform = hardware.Os }
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "State broadcast tick failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}

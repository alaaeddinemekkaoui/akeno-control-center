using NAudio.CoreAudioApi;

namespace Akeno.Host.Services;

public sealed class WindowsAudioService : IDisposable
{
    private readonly MMDeviceEnumerator? _enumerator;

    public WindowsAudioService()
    {
        if (OperatingSystem.IsWindows())
        {
            try { _enumerator = new MMDeviceEnumerator(); }
            catch { _enumerator = null; }
        }
    }

    private MMDevice? Output()
    {
        try { return _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
        catch { return null; }
    }

    private MMDevice? Input()
    {
        try { return _enumerator?.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications); }
        catch { return null; }
    }

    public AudioSnapshot GetSnapshot()
    {
        using var output = Output();
        using var input = Input();
        try
        {
            var outputAvailable = output is not null;
            var inputAvailable = input is not null;
            return new AudioSnapshot(
                outputAvailable,
                outputAvailable ? Math.Round(output!.AudioEndpointVolume.MasterVolumeLevelScalar * 100, 0) : 72,
                outputAvailable && output!.AudioEndpointVolume.Mute,
                output?.FriendlyName ?? "Output unavailable",
                inputAvailable,
                inputAvailable ? Math.Round(input!.AudioEndpointVolume.MasterVolumeLevelScalar * 100, 0) : 70,
                inputAvailable && input!.AudioEndpointVolume.Mute,
                input?.FriendlyName ?? "Microphone unavailable");
        }
        catch
        {
            return new(false, 72, false, "Output unavailable", false, 70, false, "Microphone unavailable");
        }
    }

    public bool SetOutputVolume(double value)
    {
        using var device = Output();
        if (device is null) return false;
        try
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(Math.Clamp(value, 0, 100) / 100d);
            return true;
        }
        catch { return false; }
    }

    public bool SetOutputMute(bool muted)
    {
        using var device = Output();
        if (device is null) return false;
        try { device.AudioEndpointVolume.Mute = muted; return true; }
        catch { return false; }
    }

    public bool SetMicVolume(double value)
    {
        using var device = Input();
        if (device is null) return false;
        try
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(Math.Clamp(value, 0, 100) / 100d);
            return true;
        }
        catch { return false; }
    }

    public bool SetMicMute(bool muted)
    {
        using var device = Input();
        if (device is null) return false;
        try { device.AudioEndpointVolume.Mute = muted; return true; }
        catch { return false; }
    }

    public void Dispose() => _enumerator?.Dispose();
}

public sealed record AudioSnapshot(
    bool OutputAvailable,
    double OutputVolume,
    bool OutputMuted,
    string OutputDevice,
    bool MicAvailable,
    double MicVolume,
    bool MicMuted,
    string MicDevice);

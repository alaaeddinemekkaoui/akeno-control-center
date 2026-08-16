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

    private MMDevice? DefaultDevice()
    {
        try { return _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
        catch { return null; }
    }

    public AudioSnapshot GetSnapshot()
    {
        using var device = DefaultDevice();
        if (device is null) return new(false, 72, false, "Audio endpoint unavailable");

        try
        {
            return new(true,
                Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100, 0),
                device.AudioEndpointVolume.Mute,
                device.FriendlyName);
        }
        catch (Exception ex)
        {
            return new(false, 72, false, ex.Message);
        }
    }

    public bool SetVolume(double value)
    {
        using var device = DefaultDevice();
        if (device is null) return false;
        try
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(Math.Clamp(value, 0, 100) / 100d);
            return true;
        }
        catch { return false; }
    }

    public bool SetMute(bool muted)
    {
        using var device = DefaultDevice();
        if (device is null) return false;
        try
        {
            device.AudioEndpointVolume.Mute = muted;
            return true;
        }
        catch { return false; }
    }

    public void Dispose() => _enumerator?.Dispose();
}

public sealed record AudioSnapshot(bool Available, double Volume, bool Muted, string Device);

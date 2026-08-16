using Akeno.Host.Models;

namespace Akeno.Host.Services;

public sealed class ComponentCatalogService
{
    private static readonly ComponentDefinition[] Definitions =
    [
        new("master.volume", "Master Volume", "audio", "range", "volume-2", "System output volume.", ["slider", "plusminus", "value"], "2x1", new Dictionary<string, object> { ["min"] = 0, ["max"] = 100, ["step"] = 1, ["unit"] = "%" }),
        new("master.muted", "Master Mute", "audio", "boolean", "volume-x", "System output mute toggle.", ["toggle", "status"], "1x1"),
        new("mic.volume", "Microphone Volume", "audio", "range", "mic-2", "Default microphone input level.", ["slider", "plusminus", "value"], "2x1", new Dictionary<string, object> { ["min"] = 0, ["max"] = 100, ["step"] = 1, ["unit"] = "%" }),
        new("mic.muted", "Microphone Mute", "audio", "boolean", "mic-off", "Default microphone mute toggle.", ["toggle", "status"], "1x1"),
        new("display.brightness", "Display Brightness", "system", "range", "sun", "Display brightness when supported by hardware.", ["slider", "plusminus", "value", "presets"], "2x1", new Dictionary<string, object> { ["min"] = 0, ["max"] = 100, ["step"] = 1, ["unit"] = "%" }),
        new("system.lock", "Lock PC", "actions", "action", "lock", "Lock the current Windows session.", ["action", "hold"], "1x1"),
        new("system.sleep", "Sleep PC", "actions", "action", "moon", "Put Windows into sleep state.", ["action", "hold"], "1x1"),
        new("system.restart", "Restart PC", "actions", "action", "rotate-cw", "Restart Windows safely.", ["action", "hold-confirm"], "1x1", Dangerous: true),
        new("system.shutdown", "Shutdown PC", "actions", "action", "power", "Shutdown Windows safely.", ["action", "hold-confirm"], "1x1", Dangerous: true),
        new("stream.live", "Stream Status", "streaming", "boolean", "radio", "Live stream status flag.", ["toggle", "status"], "1x1")
    ];

    public IReadOnlyList<ComponentDefinition> List() => Definitions;
}

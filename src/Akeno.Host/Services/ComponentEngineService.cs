using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Akeno.Host.Models;

namespace Akeno.Host.Services;

public sealed class ComponentEngineService
{
    private readonly ConcurrentDictionary<string, ComponentDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ComponentState> _states = new(StringComparer.OrdinalIgnoreCase);

    public ComponentEngineService()
    {
        RegisterDefaults();
    }

    public IReadOnlyList<ComponentDefinition> GetDefinitions() => _definitions.Values.OrderBy(x => x.Category).ThenBy(x => x.Name).ToList();

    public ComponentDefinition? GetDefinition(string id) => _definitions.TryGetValue(id, out var definition) ? definition : null;

    public IReadOnlyDictionary<string, ComponentState> GetStates() => new Dictionary<string, ComponentState>(_states);

    public ComponentState? GetState(string id) => _states.TryGetValue(id, out var state) ? state : null;

    public void SetState(string componentId, object? value, bool available = true, string? error = null)
    {
        _states[componentId] = new ComponentState
        {
            ComponentId = componentId,
            Value = value,
            Available = available,
            Loading = false,
            Error = error,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }

    public Dictionary<string, object?> BuildControls()
    {
        return _states
            .Where(x => x.Key.StartsWith("master.", StringComparison.OrdinalIgnoreCase)
                     || x.Key.StartsWith("mic.", StringComparison.OrdinalIgnoreCase)
                     || x.Key.StartsWith("display.", StringComparison.OrdinalIgnoreCase)
                     || x.Key.StartsWith("stream.", StringComparison.OrdinalIgnoreCase)
                     || x.Key.StartsWith("media.", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value.Value);
    }

    private void RegisterDefaults()
    {
        RegisterRange("master.volume", "Master Volume", "System", "speaker", ["slider", "plusminus", "value", "presets"], 0, 100, 1, "%");
        RegisterBoolean("master.muted", "Master Mute", "Audio", "mute", ["toggle", "button", "indicator"]);
        RegisterRange("mic.volume", "Microphone Volume", "Audio", "mic", ["slider", "plusminus", "value", "presets"], 0, 100, 1, "%");
        RegisterBoolean("mic.muted", "Microphone Mute", "Audio", "mic-off", ["toggle", "button", "indicator"]);
        RegisterRange("display.brightness", "Display Brightness", "System", "brightness", ["slider", "plusminus", "value", "presets"], 0, 100, 5, "%");

        RegisterAction("system.lock", "Lock PC", "System", "lock", ["button", "icon", "large"]);
        RegisterAction("system.sleep", "Sleep PC", "System", "sleep", ["button", "icon", "large"]);
        RegisterAction("system.restart", "Restart PC", "System", "restart", ["button", "icon", "large"]);
        RegisterAction("system.shutdown", "Shutdown PC", "System", "shutdown", ["button", "icon", "large"]);

        RegisterNumber("system.cpu.usage", "CPU Usage", "Telemetry", "cpu", ["number", "gauge", "progress"], "%");
        RegisterNumber("system.cpu.temperature", "CPU Temperature", "Telemetry", "cpu-temp", ["number", "gauge", "progress"], "°C");
        RegisterNumber("system.gpu.usage", "GPU Usage", "Telemetry", "gpu", ["number", "gauge", "progress"], "%");
        RegisterNumber("system.gpu.temperature", "GPU Temperature", "Telemetry", "gpu-temp", ["number", "gauge", "progress"], "°C");
        RegisterNumber("system.ram.usage", "RAM Usage", "Telemetry", "ram", ["number", "gauge", "progress"], "%");
        RegisterNumber("system.network.down", "Network Download", "Telemetry", "network-down", ["number", "compact"], "Mbps");
        RegisterNumber("system.network.up", "Network Upload", "Telemetry", "network-up", ["number", "compact"], "Mbps");

        SetState("master.volume", 50d);
        SetState("master.muted", false);
        SetState("mic.volume", 70d);
        SetState("mic.muted", false);
        SetState("display.brightness", null, false, "Brightness control has not been detected yet.");
        SetState("stream.live", false);
        SetState("media.playing", false);

        SetState("system.cpu.usage", null, false, "CPU sensor unavailable.");
        SetState("system.cpu.temperature", null, false, "CPU temperature sensor unavailable.");
        SetState("system.gpu.usage", null, false, "GPU sensor unavailable.");
        SetState("system.gpu.temperature", null, false, "GPU temperature sensor unavailable.");
        SetState("system.ram.usage", null, false, "RAM sensor unavailable.");
        SetState("system.network.down", null, false, "Network sensor unavailable.");
        SetState("system.network.up", null, false, "Network sensor unavailable.");
    }

    private void RegisterRange(string id, string name, string category, string icon, IReadOnlyList<string> views, double min, double max, double step, string unit)
    {
        Register(new ComponentDefinition
        {
            Id = id,
            Name = name,
            Description = name,
            Category = category,
            Type = "RANGE",
            Icon = icon,
            SupportedViews = views,
            Capabilities = new JsonObject
            {
                ["min"] = min,
                ["max"] = max,
                ["step"] = step,
                ["unit"] = unit
            }
        });
    }

    private void RegisterBoolean(string id, string name, string category, string icon, IReadOnlyList<string> views)
    {
        Register(new ComponentDefinition
        {
            Id = id,
            Name = name,
            Description = name,
            Category = category,
            Type = "BOOLEAN",
            Icon = icon,
            SupportedViews = views,
            Capabilities = new JsonObject()
        });
    }

    private void RegisterAction(string id, string name, string category, string icon, IReadOnlyList<string> views)
    {
        Register(new ComponentDefinition
        {
            Id = id,
            Name = name,
            Description = name,
            Category = category,
            Type = "ACTION",
            Icon = icon,
            SupportedViews = views,
            Capabilities = new JsonObject
            {
                ["confirmation"] = id is "system.restart" or "system.shutdown"
            }
        });
    }

    private void RegisterNumber(string id, string name, string category, string icon, IReadOnlyList<string> views, string unit)
    {
        Register(new ComponentDefinition
        {
            Id = id,
            Name = name,
            Description = name,
            Category = category,
            Type = "NUMBER",
            Icon = icon,
            SupportedViews = views,
            Capabilities = new JsonObject
            {
                ["unit"] = unit
            }
        });
    }

    private void Register(ComponentDefinition definition)
    {
        _definitions[definition.Id] = definition;
    }
}

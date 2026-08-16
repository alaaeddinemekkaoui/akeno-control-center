using System.Collections.Concurrent;
using Akeno.Host.Models;

namespace Akeno.Host.Services;

public sealed class ControlState
{
    private readonly ConcurrentDictionary<string, object> _values = new()
    {
        ["master.volume"] = 72d,
        ["display.brightness"] = 68d,
        ["mic.muted"] = false,
        ["stream.live"] = false,
        ["media.playing"] = true
    };

    public IReadOnlyDictionary<string, object> Snapshot() => new Dictionary<string, object>(_values);

    public ControlResult Apply(string key, ControlCommand command)
    {
        if (!_values.ContainsKey(key)) return new(false, key, null, "Unknown control.");
        if (_values[key] is double current)
        {
            var next = command.Value ?? current;
            if (command.Operation == "increment") next = current + (command.Step ?? 5);
            else if (command.Operation == "decrement") next = current - (command.Step ?? 5);
            next = Math.Clamp(next, 0, 100);
            _values[key] = next;
            return new(true, key, next, null);
        }
        if (_values[key] is bool b)
        {
            var next = command.Bool ?? !b;
            _values[key] = next;
            return new(true, key, next, null);
        }
        return new(false, key, null, "Unsupported control type.");
    }

    public object TriggerAction(string key) => new { success = true, key, triggeredAt = DateTimeOffset.UtcNow, message = $"{key} triggered (demo action)" };
}

public sealed record ControlResult(bool Success, string Key, object? Value, string? Error);

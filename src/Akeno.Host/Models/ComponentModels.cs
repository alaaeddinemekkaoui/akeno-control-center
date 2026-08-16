namespace Akeno.Host.Models;

public sealed record ComponentDefinition(
    string Id,
    string Name,
    string Category,
    string Type,
    string Icon,
    string Description,
    string[] Views,
    string DefaultSize,
    Dictionary<string, object>? Capabilities = null,
    bool Dangerous = false);

public sealed record ComponentRuntimeState(
    object? Value,
    bool Available,
    bool Loading,
    string? Error,
    DateTimeOffset LastUpdated);

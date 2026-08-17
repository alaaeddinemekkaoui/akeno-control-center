using System.Text.Json.Nodes;

namespace Akeno.Host.Models;

public sealed record ComponentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public bool Available { get; init; } = true;
    public IReadOnlyList<string> SupportedViews { get; init; } = Array.Empty<string>();
    public JsonObject Capabilities { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

public sealed record ComponentState
{
    public string ComponentId { get; init; } = string.Empty;
    public object? Value { get; init; }
    public bool Available { get; init; } = true;
    public bool Loading { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ComponentAction
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public string ComponentId { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    public JsonObject Parameters { get; init; } = [];
}

public sealed record WidgetInstance
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public string ComponentId { get; init; } = string.Empty;
    public string View { get; init; } = "button";
    public string Size { get; init; } = "1x1";
    public int Position { get; init; }
    public string? TitleOverride { get; init; }
    public string? IconOverride { get; init; }
    public JsonObject Configuration { get; init; } = [];
}

public sealed record DeckPage
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public string Name { get; init; } = "Page";
    public int Order { get; init; }
    public List<WidgetInstance> Widgets { get; init; } = [];
}

public sealed record DeckLayout
{
    public List<DeckPage> Pages { get; init; } = [];
}

public sealed record PairRequest(string? Code, string? DeviceName);
public sealed record ActionCommand(bool? Confirmed, bool? HoldToConfirm);

namespace Akeno.Host.Models;

public sealed record ControlCommand
{
    public double? Value { get; init; }
    public bool? Bool { get; init; }
    public string? Operation { get; init; }
    public double? Step { get; init; }
}

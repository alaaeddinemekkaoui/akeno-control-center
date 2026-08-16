namespace Akeno.Host.Models;

public sealed record ActionCommand
{
    public bool? Confirm { get; init; }
}

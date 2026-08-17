using System.Text.Json;
using Akeno.Host.Models;

namespace Akeno.Host.Services;

public sealed class DeckLayoutService
{
    private const string LayoutKey = "deck.layout";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AkenoDbService _db;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DeckLayoutService(AkenoDbService db)
    {
        _db = db;
    }

    public async Task<DeckLayout> GetLayoutAsync(CancellationToken cancellationToken = default)
    {
        var json = await _db.GetJsonAsync(LayoutKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            var layout = DefaultLayout();
            await SaveLayoutAsync(layout, cancellationToken);
            return layout;
        }

        var parsed = JsonSerializer.Deserialize<DeckLayout>(json, JsonOptions);
        return parsed ?? DefaultLayout();
    }

    public async Task SaveLayoutAsync(DeckLayout layout, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var normalized = layout with
            {
                Pages = layout.Pages
                    .OrderBy(p => p.Order)
                    .Select((page, index) => page with
                    {
                        Order = index,
                        Widgets = page.Widgets.Select((widget, widgetIndex) => widget with { Position = widgetIndex }).ToList()
                    })
                    .ToList()
            };
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            await _db.SetJsonAsync(LayoutKey, json, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DeckPage>> GetPagesAsync(CancellationToken cancellationToken = default)
    {
        var layout = await GetLayoutAsync(cancellationToken);
        return layout.Pages.OrderBy(p => p.Order).ToList();
    }

    public async Task<DeckPage> AddPageAsync(string name, CancellationToken cancellationToken = default)
    {
        var layout = await GetLayoutAsync(cancellationToken);
        var page = new DeckPage
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = string.IsNullOrWhiteSpace(name) ? $"Page {layout.Pages.Count + 1}" : name.Trim(),
            Order = layout.Pages.Count,
            Widgets = []
        };
        layout.Pages.Add(page);
        await SaveLayoutAsync(layout, cancellationToken);
        return page;
    }

    public async Task<DeckPage?> UpdatePageAsync(string id, string? name, int? order, CancellationToken cancellationToken = default)
    {
        var layout = await GetLayoutAsync(cancellationToken);
        var page = layout.Pages.FirstOrDefault(p => p.Id == id);
        if (page is null) return null;

        var updated = page with
        {
            Name = string.IsNullOrWhiteSpace(name) ? page.Name : name.Trim(),
            Order = order ?? page.Order
        };
        layout.Pages.Remove(page);
        layout.Pages.Add(updated);
        await SaveLayoutAsync(layout, cancellationToken);
        return updated;
    }

    public async Task<bool> DeletePageAsync(string id, CancellationToken cancellationToken = default)
    {
        var layout = await GetLayoutAsync(cancellationToken);
        if (layout.Pages.Count <= 1) return false;
        var removed = layout.Pages.RemoveAll(p => p.Id == id);
        if (removed == 0) return false;
        await SaveLayoutAsync(layout, cancellationToken);
        return true;
    }

    private static DeckLayout DefaultLayout()
    {
        return new DeckLayout
        {
            Pages =
            [
                new DeckPage
                {
                    Id = "home",
                    Name = "Home",
                    Order = 0,
                    Widgets =
                    [
                        new() { Id = "vol", ComponentId = "master.volume", View = "slider", Size = "2x1", TitleOverride = "Master Volume", Position = 0 },
                        new() { Id = "mute", ComponentId = "master.muted", View = "toggle", Size = "1x1", TitleOverride = "Mute", Position = 1 },
                        new() { Id = "mic", ComponentId = "mic.muted", View = "toggle", Size = "1x1", TitleOverride = "Microphone", Position = 2 },
                        new() { Id = "bright", ComponentId = "display.brightness", View = "plusminus", Size = "2x1", TitleOverride = "Brightness", Position = 3 }
                    ]
                },
                new DeckPage
                {
                    Id = "system",
                    Name = "System",
                    Order = 1,
                    Widgets =
                    [
                        new() { Id = "lock", ComponentId = "system.lock", View = "button", Size = "1x1", TitleOverride = "Lock", Position = 0 },
                        new() { Id = "sleep", ComponentId = "system.sleep", View = "button", Size = "1x1", TitleOverride = "Sleep", Position = 1 },
                        new() { Id = "restart", ComponentId = "system.restart", View = "button", Size = "1x1", TitleOverride = "Restart", Position = 2 },
                        new() { Id = "shutdown", ComponentId = "system.shutdown", View = "button", Size = "1x1", TitleOverride = "Shutdown", Position = 3 }
                    ]
                }
            ]
        };
    }
}

public sealed record CreatePageRequest(string? Name);
public sealed record UpdatePageRequest(string? Name, int? Order);

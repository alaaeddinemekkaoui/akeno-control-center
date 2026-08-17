using System.Text.Json;

namespace Akeno.Host.Services;

public sealed class SettingsService
{
    private const string SettingsKey = "host.settings";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AkenoDbService _db;

    public SettingsService(AkenoDbService db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, object>> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await _db.GetJsonAsync(SettingsKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            var defaults = DefaultSettings();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions) ?? DefaultSettings();
    }

    public async Task SaveAsync(Dictionary<string, object> settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await _db.SetJsonAsync(SettingsKey, json, cancellationToken);
    }

    private static Dictionary<string, object> DefaultSettings() => new()
    {
        ["general.theme"] = "akeno-noir",
        ["network.port"] = 5077,
        ["security.requirePairing"] = false,
        ["deck.mobileColumns"] = 4,
        ["deck.desktopColumns"] = 8
    };
}

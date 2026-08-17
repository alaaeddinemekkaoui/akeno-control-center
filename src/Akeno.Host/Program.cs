using System.Text.Json;
using Akeno.Host.Hubs;
using Akeno.Host.Models;
using Akeno.Host.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("AKENO_URLS") ?? "http://0.0.0.0:5077");

builder.Services.AddSignalR();
builder.Services.AddSingleton<WindowsAudioService>();
builder.Services.AddSingleton<HardwareMonitorService>();
builder.Services.AddSingleton<WindowsControlService>();
builder.Services.AddSingleton<AkenoDbService>();
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<ComponentEngineService>();
builder.Services.AddSingleton<DeckLayoutService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddHostedService<StateBroadcastWorker>();

var app = builder.Build();
var requirePairing = string.Equals(Environment.GetEnvironmentVariable("AKENO_REQUIRE_PAIRING"), "true", StringComparison.OrdinalIgnoreCase);
var pairing = app.Services.GetRequiredService<PairingService>();

Console.WriteLine("\nAKENO CONTROL CENTER");
Console.WriteLine("LAN URL: http://<YOUR-PC-IP>:5077");
Console.WriteLine($"Pairing: {(requirePairing ? "REQUIRED" : "optional")}");
if (requirePairing) Console.WriteLine($"Pairing code: {pairing.PairingCode}");
Console.WriteLine();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    ok = true,
    name = "AKENO Control Center",
    version = "2.0.0",
    platform = Environment.OSVersion.VersionString,
    pairingRequired = requirePairing,
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/api/config", async (DeckLayoutService layoutService) =>
{
    var layout = await layoutService.GetLayoutAsync();
    return Results.Ok(new
    {
        pairingRequired = requirePairing,
        isWindows = OperatingSystem.IsWindows(),
        app = "AKENO Control Center",
        deckPages = layout.Pages.Count
    });
});

app.MapGet("/api/state", async (ComponentEngineService engine, WindowsAudioService audio, HardwareMonitorService hardware, WindowsControlService windows) =>
{
    await RefreshComponentState(engine, audio, hardware, windows);
    return Results.Ok(BuildState(engine, hardware));
});

app.MapGet("/api/components", (ComponentEngineService engine) => Results.Ok(new
{
    definitions = engine.GetDefinitions(),
    states = engine.GetStates().Values
}));

app.MapGet("/api/components/{id}", (string id, ComponentEngineService engine) =>
{
    var definition = engine.GetDefinition(id);
    if (definition is null) return Results.NotFound(new { error = "Component not found." });
    return Results.Ok(new
    {
        definition,
        state = engine.GetState(id)
    });
});

app.MapPost("/api/control/{id}", async (HttpContext http, string id, ControlCommand command,
    ComponentEngineService engine, WindowsAudioService audio, WindowsControlService windows,
    PairingService pairingService, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();

    switch (id)
    {
        case "master.volume":
        {
            var current = ToDouble(engine.GetState(id)?.Value, 50);
            var value = ResolveRange(current, command);
            var native = audio.SetOutputVolume(value);
            engine.SetState(id, value, native, native ? null : "Output device unavailable.");
            await hub.Clients.All.SendAsync("componentChanged", engine.GetStates());
            return Results.Ok(new { success = native, id, value, available = native });
        }
        case "master.muted":
        {
            var current = ToBool(engine.GetState(id)?.Value, false);
            var value = command.Bool ?? !current;
            var native = audio.SetOutputMute(value);
            engine.SetState(id, value, native, native ? null : "Output device unavailable.");
            await hub.Clients.All.SendAsync("componentChanged", engine.GetStates());
            return Results.Ok(new { success = native, id, value, available = native });
        }
        case "mic.volume":
        {
            var current = ToDouble(engine.GetState(id)?.Value, 70);
            var value = ResolveRange(current, command);
            var native = audio.SetMicVolume(value);
            engine.SetState(id, value, native, native ? null : "Microphone unavailable.");
            await hub.Clients.All.SendAsync("componentChanged", engine.GetStates());
            return Results.Ok(new { success = native, id, value, available = native });
        }
        case "mic.muted":
        {
            var current = ToBool(engine.GetState(id)?.Value, false);
            var value = command.Bool ?? !current;
            var native = audio.SetMicMute(value);
            engine.SetState(id, value, native, native ? null : "Microphone unavailable.");
            await hub.Clients.All.SendAsync("componentChanged", engine.GetStates());
            return Results.Ok(new { success = native, id, value, available = native });
        }
        case "display.brightness":
        {
            var current = ToDouble(engine.GetState(id)?.Value, 50);
            var value = ResolveRange(current, command);
            var result = await windows.SetBrightnessAsync(value);
            engine.SetState(id, result.Success ? value : null, result.Success, result.Success ? null : result.Message);
            await hub.Clients.All.SendAsync("componentChanged", engine.GetStates());
            return Results.Ok(new { success = result.Success, id, value = result.Success ? value : null, error = result.Success ? null : result.Message });
        }
        default:
        {
            return Results.BadRequest(new { success = false, id, error = "Unknown control." });
        }
    }
});

app.MapPost("/api/action/{id}", async (HttpContext http, string id, ActionCommand command,
    PairingService pairingService, WindowsControlService windows, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();

    if (id is "system.restart" or "system.shutdown")
    {
        var confirmed = command.Confirmed == true || command.HoldToConfirm == true;
        if (!confirmed)
        {
            return Results.BadRequest(new
            {
                success = false,
                id,
                error = "Confirmation is required for restart/shutdown."
            });
        }
    }

    var result = await windows.RunActionAsync(id);
    await hub.Clients.All.SendAsync("hostStatus", new { action = id, success = result.Success, message = result.Message });
    return Results.Ok(new { success = result.Success, id, message = result.Message });
});

app.MapGet("/api/pages", async (DeckLayoutService layoutService) => Results.Ok(await layoutService.GetPagesAsync()));

app.MapPost("/api/pages", async (HttpContext http, CreatePageRequest request, DeckLayoutService layoutService,
    PairingService pairingService, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();
    var page = await layoutService.AddPageAsync(request.Name ?? "Page");
    await hub.Clients.All.SendAsync("pageChanged", page);
    return Results.Ok(page);
});

app.MapPut("/api/pages/{id}", async (HttpContext http, string id, UpdatePageRequest request, DeckLayoutService layoutService,
    PairingService pairingService, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();
    var page = await layoutService.UpdatePageAsync(id, request.Name, request.Order);
    if (page is null) return Results.NotFound(new { error = "Page not found." });
    await hub.Clients.All.SendAsync("pageChanged", page);
    return Results.Ok(page);
});

app.MapDelete("/api/pages/{id}", async (HttpContext http, string id, DeckLayoutService layoutService,
    PairingService pairingService, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();
    var deleted = await layoutService.DeletePageAsync(id);
    if (!deleted) return Results.BadRequest(new { success = false, error = "Page not found or cannot remove the last page." });
    await hub.Clients.All.SendAsync("pageChanged", new { deletedPageId = id });
    return Results.Ok(new { success = true, id });
});

app.MapGet("/api/layout", async (DeckLayoutService layoutService) => Results.Ok(await layoutService.GetLayoutAsync()));

app.MapPut("/api/layout", async (HttpContext http, DeckLayout layout, DeckLayoutService layoutService,
    PairingService pairingService, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();
    await layoutService.SaveLayoutAsync(layout);
    await hub.Clients.All.SendAsync("pageChanged", layout);
    return Results.Ok(layout);
});

app.MapGet("/api/settings", async (SettingsService settingsService) => Results.Ok(await settingsService.GetAsync()));

app.MapPut("/api/settings", async (HttpContext http, Dictionary<string, object> settings,
    SettingsService settingsService, PairingService pairingService, IHubContext<ControlHub> hub) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();
    await settingsService.SaveAsync(settings);
    await hub.Clients.All.SendAsync("integrationChanged", settings);
    return Results.Ok(settings);
});

app.MapPost("/api/pair", async (PairRequest request, PairingService pairingService) =>
{
    try
    {
        var token = await pairingService.CreateTokenAsync(request.Code ?? string.Empty, request.DeviceName);
        return Results.Ok(new { token, expiresInDays = 30 });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapGet("/api/clients", async (PairingService pairingService) =>
{
    var clients = await pairingService.GetClientsAsync();
    return Results.Ok(clients.Select(c => new
    {
        token = c.Token,
        deviceName = c.DeviceName,
        lastConnected = c.LastConnected,
        expiresAt = c.ExpiresAt
    }));
});

app.MapDelete("/api/clients/{token}", async (HttpContext http, string token, PairingService pairingService) =>
{
    if (requirePairing && !await AuthorizedAsync(http, pairingService)) return Results.Unauthorized();
    await pairingService.RevokeAsync(token);
    return Results.Ok(new { success = true });
});

app.MapGet("/api/events", async (HttpContext http, ComponentEngineService engine, HardwareMonitorService hardware, CancellationToken cancellationToken) =>
{
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.Connection = "keep-alive";
    http.Response.ContentType = "text/event-stream";

    while (!cancellationToken.IsCancellationRequested)
    {
        var payload = JsonSerializer.Serialize(BuildState(engine, hardware));
        await http.Response.WriteAsync($"event: streamChanged\n", cancellationToken);
        await http.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
        try { await Task.Delay(1000, cancellationToken); }
        catch (TaskCanceledException) { break; }
    }
});

app.MapHub<ControlHub>("/hubs/control");

app.MapFallbackToFile("index.html");
app.Run();

static async Task RefreshComponentState(ComponentEngineService engine, WindowsAudioService audio, HardwareMonitorService hardware, WindowsControlService windows)
{
    var a = audio.GetSnapshot();
    engine.SetState("master.volume", a.OutputVolume, a.OutputAvailable, a.OutputAvailable ? null : "Output device unavailable.");
    engine.SetState("master.muted", a.OutputMuted, a.OutputAvailable, a.OutputAvailable ? null : "Output device unavailable.");
    engine.SetState("mic.volume", a.MicVolume, a.MicAvailable, a.MicAvailable ? null : "Microphone unavailable.");
    engine.SetState("mic.muted", a.MicMuted, a.MicAvailable, a.MicAvailable ? null : "Microphone unavailable.");

    var brightness = await windows.TryReadBrightnessAsync();
    engine.SetState("display.brightness", brightness.Value, brightness.Available, brightness.Error);

    var h = hardware.Read();
    engine.SetState("system.cpu.usage", h.CpuUsage, h.Available, h.Available ? null : "CPU sensor unavailable.");
    engine.SetState("system.cpu.temperature", h.CpuTemperature, h.Available, h.Available ? null : "CPU temperature sensor unavailable.");
    engine.SetState("system.gpu.usage", h.GpuUsage, h.Available, h.Available ? null : "GPU sensor unavailable.");
    engine.SetState("system.gpu.temperature", h.GpuTemperature, h.Available, h.Available ? null : "GPU temperature sensor unavailable.");
    engine.SetState("system.ram.usage", h.RamUsage, h.Available, h.Available ? null : "RAM sensor unavailable.");
    engine.SetState("system.network.down", h.DownloadMbps, h.Available, h.Available ? null : "Network sensor unavailable.");
    engine.SetState("system.network.up", h.UploadMbps, h.Available, h.Available ? null : "Network sensor unavailable.");
}

static object BuildState(ComponentEngineService engine, HardwareMonitorService hardware)
{
    var h = hardware.Read();
    return new
    {
        controls = engine.BuildControls(),
        components = engine.GetStates().Values,
        telemetry = new
        {
            cpu = new { usage = h.CpuUsage, temperature = h.CpuTemperature },
            gpu = new { usage = h.GpuUsage, temperature = h.GpuTemperature, fps = 0d, name = h.GpuName },
            ram = new { usage = h.RamUsage },
            network = new { downMbps = h.DownloadMbps, upMbps = h.UploadMbps, pingMs = 0d },
            system = new { status = h.Available ? "PC CONNECTED" : "HOST OFFLINE", host = h.Host, platform = h.Os }
        },
        hostStatus = h.Available ? "PC CONNECTED" : "HOST OFFLINE",
        serverTime = DateTimeOffset.UtcNow
    };
}

static double ResolveRange(double current, ControlCommand command)
{
    var value = command.Value ?? current;
    if (command.Operation == "increment") value = current + (command.Step ?? 5);
    if (command.Operation == "decrement") value = current - (command.Step ?? 5);
    return Math.Clamp(value, 0, 100);
}

static double ToDouble(object? value, double fallback)
{
    return value switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        _ => fallback
    };
}

static bool ToBool(object? value, bool fallback)
{
    return value is bool b ? b : fallback;
}

static async Task<bool> AuthorizedAsync(HttpContext http, PairingService pairing)
{
    var auth = http.Request.Headers.Authorization.ToString();
    var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : null;
    return await pairing.IsValidAsync(token);
}

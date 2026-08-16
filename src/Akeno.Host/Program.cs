using System.Text.Json;
using Akeno.Host.Models;
using Akeno.Host.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("AKENO_URLS") ?? "http://0.0.0.0:5077");

builder.Services.AddSingleton<ControlState>();
builder.Services.AddSingleton<WindowsAudioService>();
builder.Services.AddSingleton<HardwareMonitorService>();
builder.Services.AddSingleton<WindowsControlService>();
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<ComponentCatalogService>();

var app = builder.Build();
var requirePairing = string.Equals(Environment.GetEnvironmentVariable("AKENO_REQUIRE_PAIRING"), "true", StringComparison.OrdinalIgnoreCase);
var pairing = app.Services.GetRequiredService<PairingService>();

Console.WriteLine("\nAKENO CONTROL CENTER");
Console.WriteLine($"LAN URL: http://<YOUR-PC-IP>:5077");
Console.WriteLine($"Pairing: {(requirePairing ? "REQUIRED" : "optional")}");
if (requirePairing) Console.WriteLine($"Pairing code: {pairing.PairingCode}");
Console.WriteLine();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    ok = true,
    name = "AKENO Control Center",
    version = "1.0.0-agent",
    platform = Environment.OSVersion.VersionString,
    pairingRequired = requirePairing,
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/api/state", (ControlState state, WindowsAudioService audio, HardwareMonitorService hardware, ComponentCatalogService catalog) =>
{
    return Results.Ok(BuildState(state, audio, hardware, catalog));
});

app.MapGet("/api/config", () => Results.Ok(new
{
    pairingRequired = requirePairing,
    isWindows = OperatingSystem.IsWindows(),
    app = "AKENO Control Center"
}));

app.MapGet("/api/components", (ControlState state, WindowsAudioService audio, ComponentCatalogService catalog) =>
{
    var audioSnapshot = audio.GetSnapshot();
    var values = state.Snapshot();

    var items = catalog.List().Select(def =>
    {
        values.TryGetValue(def.Id, out var value);
        var available = true;
        string? error = null;

        if (def.Id == "display.brightness" && !OperatingSystem.IsWindows())
        {
            available = false;
            error = "Not supported on this platform";
        }
        else if (def.Id.StartsWith("master.", StringComparison.Ordinal) && !audioSnapshot.OutputAvailable)
        {
            available = false;
            error = "Output device unavailable";
        }
        else if (def.Id.StartsWith("mic.", StringComparison.Ordinal) && !audioSnapshot.MicAvailable)
        {
            available = false;
            error = "Microphone unavailable";
        }

        return new
        {
            id = def.Id,
            name = def.Name,
            category = def.Category,
            type = def.Type,
            icon = def.Icon,
            description = def.Description,
            capabilities = def.Capabilities,
            views = def.Views,
            defaultSize = def.DefaultSize,
            dangerous = def.Dangerous,
            state = new ComponentRuntimeState(value, available, false, error, DateTimeOffset.UtcNow)
        };
    });

    return Results.Ok(items);
});

app.MapPost("/api/pair", (PairRequest request, PairingService pairingService) =>
{
    try
    {
        var token = pairingService.CreateToken(request.Code ?? string.Empty);
        return Results.Ok(new { token, expiresInDays = 30 });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/control/{key}", async (HttpContext http, string key, ControlCommand command,
    ControlState state, WindowsAudioService audio, WindowsControlService windows, PairingService pairingService) =>
{
    if (requirePairing && !Authorized(http, pairingService)) return Results.Unauthorized();

    switch (key)
    {
        case "master.volume":
        {
            var value = ResolveRange(state, key, command);
            var native = audio.SetOutputVolume(value);
            state.Apply(key, new ControlCommand { Value = value });
            return Results.Ok(new { success = true, key, value, native });
        }
        case "master.muted":
        {
            var value = command.Bool ?? !audio.GetSnapshot().OutputMuted;
            var native = audio.SetOutputMute(value);
            return Results.Ok(new { success = true, key, value, native });
        }
        case "mic.muted":
        {
            var value = command.Bool ?? !audio.GetSnapshot().MicMuted;
            var native = audio.SetMicMute(value);
            state.Apply(key, new ControlCommand { Bool = value });
            return Results.Ok(new { success = true, key, value, native });
        }
        case "mic.volume":
        {
            var value = Math.Clamp(command.Value ?? 70, 0, 100);
            var native = audio.SetMicVolume(value);
            return Results.Ok(new { success = true, key, value, native });
        }
        case "display.brightness":
        {
            var value = ResolveRange(state, key, command);
            var result = await windows.SetBrightnessAsync(value);
            state.Apply(key, new ControlCommand { Value = value });
            return Results.Ok(new { success = result.Success, key, value, native = result.Success, message = result.Message });
        }
        default:
        {
            var result = state.Apply(key, command);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }
    }
});

app.MapPost("/api/action/{key}", async (HttpContext http, string key, ActionCommand? command, ControlState state,
    WindowsControlService windows, PairingService pairingService) =>
{
    if (requirePairing && !Authorized(http, pairingService)) return Results.Unauthorized();

    var requiresConfirmation = string.Equals(key, "system.restart", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(key, "system.shutdown", StringComparison.OrdinalIgnoreCase);
    if (requiresConfirmation && command?.Confirm != true)
    {
        return Results.BadRequest(new
        {
            success = false,
            key,
            error = "Confirmation required for dangerous action."
        });
    }

    if (key.StartsWith("system.", StringComparison.OrdinalIgnoreCase))
    {
        var result = await windows.RunActionAsync(key);
        return Results.Ok(new { success = result.Success, key, message = result.Message });
    }

    return Results.Ok(state.TriggerAction(key));
});

// Server-Sent Events endpoint: lightweight live sync for every phone/second screen.
app.MapGet("/api/events", async (HttpContext http, ControlState state, WindowsAudioService audio,
    HardwareMonitorService hardware, ComponentCatalogService catalog, CancellationToken cancellationToken) =>
{
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.Connection = "keep-alive";
    http.Response.ContentType = "text/event-stream";

    while (!cancellationToken.IsCancellationRequested)
    {
        var payload = JsonSerializer.Serialize(BuildState(state, audio, hardware, catalog));
        await http.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
        try { await Task.Delay(1000, cancellationToken); }
        catch (TaskCanceledException) { break; }
    }
});

app.MapFallbackToFile("index.html");
app.Run();

static object BuildState(ControlState state, WindowsAudioService audio, HardwareMonitorService hardware, ComponentCatalogService catalog)
{
    var a = audio.GetSnapshot();
    var h = hardware.Read();
    var controls = state.Snapshot().ToDictionary(k => k.Key, v => v.Value);
    if (a.OutputAvailable) controls["master.volume"] = a.OutputVolume;
    controls["master.muted"] = a.OutputMuted;
    if (a.MicAvailable) controls["mic.muted"] = a.MicMuted;
    controls["mic.volume"] = a.MicVolume;

    var timestamp = DateTimeOffset.UtcNow;
    var components = catalog.List().ToDictionary(
        d => d.Id,
        d =>
        {
            controls.TryGetValue(d.Id, out var value);
            var available = true;
            string? error = null;

            if (d.Id == "display.brightness" && !OperatingSystem.IsWindows())
            {
                available = false;
                error = "Not supported on this platform";
            }
            else if (d.Id.StartsWith("master.", StringComparison.Ordinal) && !a.OutputAvailable)
            {
                available = false;
                error = "Output device unavailable";
            }
            else if (d.Id.StartsWith("mic.", StringComparison.Ordinal) && !a.MicAvailable)
            {
                available = false;
                error = "Microphone unavailable";
            }

            return new ComponentRuntimeState(value, available, false, error, timestamp);
        });

    return new
    {
        controls,
        components,
        audio = a,
        telemetry = new
        {
            cpu = new { usage = h.CpuUsage, temperature = h.CpuTemperature },
            gpu = new { usage = h.GpuUsage, temperature = h.GpuTemperature, fps = 0d, name = h.GpuName },
            ram = new { usage = h.RamUsage },
            network = new { downMbps = h.DownloadMbps, upMbps = h.UploadMbps, pingMs = 0d },
            system = new { status = h.Available ? "Live" : "Fallback", host = h.Host, platform = h.Os }
        },
        serverTime = timestamp
    };
}

static double ResolveRange(ControlState state, string key, ControlCommand command)
{
    var current = state.Snapshot().TryGetValue(key, out var raw) && raw is double d ? d : 50d;
    var value = command.Value ?? current;
    if (command.Operation == "increment") value = current + (command.Step ?? 5);
    if (command.Operation == "decrement") value = current - (command.Step ?? 5);
    return Math.Clamp(value, 0, 100);
}

static bool Authorized(HttpContext http, PairingService pairing)
{
    var auth = http.Request.Headers.Authorization.ToString();
    var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : null;
    return pairing.IsValid(token);
}

public sealed record PairRequest(string? Code);

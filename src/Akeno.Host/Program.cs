using Akeno.Host.Models;
using Akeno.Host.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5077");
builder.Services.AddSingleton<ControlState>();
builder.Services.AddSingleton<TelemetryService>();
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { ok = true, name = "AKENO Control Center", mode = "LAN Host", time = DateTimeOffset.UtcNow }));
app.MapGet("/api/state", (ControlState state, TelemetryService telemetry) => Results.Ok(new { controls = state.Snapshot(), telemetry = telemetry.GetSnapshot(), serverTime = DateTimeOffset.UtcNow }));
app.MapPost("/api/control/{key}", (string key, ControlCommand command, ControlState state) => { var result = state.Apply(key, command); return result.Success ? Results.Ok(result) : Results.BadRequest(result); });
app.MapPost("/api/action/{key}", (string key, ControlState state) => Results.Ok(state.TriggerAction(key)));
app.MapFallbackToFile("index.html");
app.Run();

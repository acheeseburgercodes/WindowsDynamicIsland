using DynamicIsland.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace DynamicIsland.Services;

// A loopback-only bridge lets the React Native Windows companion edit the
// running WPF island without touching its dock metadata or racing JSON writes.
public sealed class SettingsBridgeService : IAsyncDisposable
{
    public const int Port = 38691;
    private WebApplication? _app;

    public async Task StartAsync(Func<Task<IslandSettingsSnapshot>> read,
        Func<IslandSettingsSnapshot, Task> update)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.ListenLocalhost(Port));
        var app = builder.Build();
        app.MapGet("/api/settings", async (HttpContext context) =>
        {
            if (!IsCompanionRequest(context)) return Results.StatusCode(403);
            return Results.Ok(await read());
        });
        app.MapPut("/api/settings", async (HttpContext context) =>
        {
            if (!IsCompanionRequest(context)) return Results.StatusCode(403);
            var settings = await context.Request.ReadFromJsonAsync<IslandSettingsSnapshot>();
            if (settings is null) return Results.BadRequest();
            try
            {
                await update(settings);
                return Results.Ok(await read());
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
        await app.StartAsync();
        _app = app;
    }

    private static bool IsCompanionRequest(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address) &&
        !context.Request.Headers.ContainsKey("Origin") &&
        context.Request.Headers["X-DynamicIsland-Settings"] == "1";

    public async ValueTask DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
    }
}

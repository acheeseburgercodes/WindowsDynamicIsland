using System.Net.Http;
using System.Text.Json;
using DynamicIsland.Models;

namespace DynamicIsland.Services;

public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public WeatherService()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsDynamicIsland/0.1");
    }

    public Task<WeatherSnapshot> GetCurrentAsync(string? location, CancellationToken cancellationToken = default) =>
        GetAsync(string.IsNullOrWhiteSpace(location)
            ? string.Empty
            : Uri.EscapeDataString(location.Trim()), cancellationToken);

    public Task<WeatherSnapshot> GetCurrentAtAsync(double latitude, double longitude,
        CancellationToken cancellationToken = default) =>
        GetAsync(FormattableString.Invariant($"{latitude:0.####},{longitude:0.####}"), cancellationToken);

    private async Task<WeatherSnapshot> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync($"https://wttr.in/{path}?format=j1", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var current = root.GetProperty("current_condition")[0];
        var area = root.GetProperty("nearest_area")[0];
        var areaName = ReadFirstValue(area, "areaName");
        var region = ReadFirstValue(area, "region");
        var resolvedLocation = string.IsNullOrWhiteSpace(region) ? areaName : $"{areaName}, {region}";

        return new WeatherSnapshot(
            resolvedLocation,
            ReadInt(current, "temp_C"),
            ReadInt(current, "FeelsLikeC"),
            ReadFirstValue(current, "weatherDesc"),
            ReadInt(current, "humidity"),
            ReadInt(current, "windspeedKmph"));
    }

    private static int ReadInt(JsonElement element, string property) =>
        int.TryParse(element.GetProperty(property).GetString(), out var value) ? value : 0;

    private static string ReadFirstValue(JsonElement element, string property)
    {
        var collection = element.GetProperty(property);
        return collection.GetArrayLength() > 0 && collection[0].TryGetProperty("value", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    public void Dispose() => _client.Dispose();
}

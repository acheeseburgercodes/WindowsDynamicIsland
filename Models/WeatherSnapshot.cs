namespace DynamicIsland.Models;

public sealed record WeatherSnapshot(
    string Location,
    int TemperatureC,
    int FeelsLikeC,
    string Condition,
    int HumidityPercent,
    int WindKph);

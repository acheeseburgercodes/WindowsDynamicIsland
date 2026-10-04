namespace DynamicIsland.Models;

public sealed record MediaSnapshot(
    bool HasSession,
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    bool CanPlayPause,
    bool CanPrevious,
    bool CanNext,
    byte[]? Thumbnail)
{
    public string SourceAppUserModelId { get; init; } = string.Empty;
    public bool CanSeek { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public TimeSpan Position { get; init; }
    public DateTimeOffset TimelineUpdatedAt { get; init; }

    public static MediaSnapshot Empty { get; } = new(
        false,
        "Nothing playing",
        "Start music in any supported app",
        string.Empty,
        false,
        false,
        false,
        false,
        null);
}

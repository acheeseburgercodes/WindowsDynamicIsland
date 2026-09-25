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

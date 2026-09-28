namespace DynamicIsland.Models;

public sealed class RunningApplicationInfo
{
    public required int ProcessId { get; init; }

    public required nint WindowHandle { get; init; }

    public required string DisplayName { get; init; }
}

namespace DynamicIsland.Models;

public enum InstalledApplicationKind
{
    StartMenuShortcut,
    PackagedApplication
}

public sealed class InstalledApplicationInfo
{
    public required string Name { get; init; }

    public required string LaunchTarget { get; init; }

    public required InstalledApplicationKind Kind { get; init; }
}

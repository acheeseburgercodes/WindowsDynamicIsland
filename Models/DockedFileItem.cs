namespace DynamicIsland.Models;

public sealed class DockedFileItem
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public string Extension { get; init; } = string.Empty;
}

namespace DynamicIsland.Models;

public sealed class AppConfiguration
{
    public double SurfaceOpacity { get; set; } = 0.86;

    public List<DockedFileItem> DockedFiles { get; set; } = [];
}

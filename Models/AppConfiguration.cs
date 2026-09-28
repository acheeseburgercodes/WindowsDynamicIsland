using DynamicIsland.Core;

namespace DynamicIsland.Models;

public sealed class AppConfiguration
{
    public int ConfigurationVersion { get; set; }

    public double SurfaceOpacity { get; set; } = 0.42;

    public IslandEdge Edge { get; set; } = IslandEdge.Top;

    public IslandAlignment Alignment { get; set; } = IslandAlignment.Center;

    public int HorizontalOffset { get; set; }

    public int VerticalOffset { get; set; } = 8;

    public IslandLogoStyle LogoStyle { get; set; } = IslandLogoStyle.BrandMark;

    public string AccentColor { get; set; } = "#FF6ED6A9";

    public IslandTheme Theme { get; set; } = IslandTheme.Dark;

    public List<DockedFileItem> DockedFiles { get; set; } = [];

    public List<AppLauncherItem> Applications { get; set; } = [];
}

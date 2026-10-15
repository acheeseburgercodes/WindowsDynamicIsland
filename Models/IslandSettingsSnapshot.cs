namespace DynamicIsland.Models;

public sealed class IslandSettingsSnapshot
{
    public double SurfaceOpacity { get; set; }
    public int Edge { get; set; }
    public int Alignment { get; set; }
    public int HorizontalOffset { get; set; }
    public int VerticalOffset { get; set; }
    public int Theme { get; set; }
    public int SurfaceStyle { get; set; }
    public string AccentColor { get; set; } = "#FF6ED6A9";
    public string GradientStartColor { get; set; } = "#FF334654";
    public string GradientEndColor { get; set; } = "#FF161821";
    public int GradientDirection { get; set; }
    public int LogoStyle { get; set; }
    public string TempVaultRoot { get; set; } = @"D:\TempVault\storage";
    public bool TempVaultAutoCopy { get; set; }
    public bool TempVaultConnected { get; set; }
}

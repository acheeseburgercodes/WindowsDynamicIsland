namespace DynamicIsland.Models;

public sealed record HardwareSnapshot(
    double CpuPercent,
    double MemoryPercent,
    double UsedMemoryGb,
    double TotalMemoryGb,
    double StoragePercent,
    double UsedStorageGb,
    double TotalStorageGb,
    int? BatteryPercent,
    bool IsPluggedIn,
    double DownloadMbps,
    double UploadMbps);

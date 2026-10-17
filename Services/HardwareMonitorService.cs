using System.IO;
using System.Runtime.InteropServices;
using DynamicIsland.Models;

namespace DynamicIsland.Services;

public sealed class HardwareMonitorService
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasCpuSample;

    public HardwareSnapshot Capture()
    {
        var cpu = ReadCpuPercent();
        var memory = ReadMemory();
        var storage = ReadStorage();
        var power = ReadPower();
        return new HardwareSnapshot(cpu, memory.Percent, memory.UsedGb, memory.TotalGb,
            storage.Percent, storage.UsedGb, storage.TotalGb, power.Percent, power.IsPluggedIn);
    }

    private double ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime)) return 0;
        var idle = ToUInt64(idleTime);
        var kernel = ToUInt64(kernelTime);
        var user = ToUInt64(userTime);
        if (!_hasCpuSample)
        {
            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
            _hasCpuSample = true;
            return 0;
        }

        var idleDelta = idle - _previousIdle;
        var totalDelta = (kernel - _previousKernel) + (user - _previousUser);
        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;
        return totalDelta == 0 ? 0 : Math.Clamp(100d * (totalDelta - idleDelta) / totalDelta, 0, 100);
    }

    private static (double Percent, double UsedGb, double TotalGb) ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0) return (0, 0, 0);
        var total = ToGb(status.TotalPhysical);
        var used = ToGb(status.TotalPhysical - status.AvailablePhysical);
        return (Math.Clamp(used / total * 100, 0, 100), used, total);
    }

    private static (double Percent, double UsedGb, double TotalGb) ReadStorage()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            var total = ToGb((ulong)drive.TotalSize);
            var used = ToGb((ulong)(drive.TotalSize - drive.AvailableFreeSpace));
            return total <= 0 ? (0, 0, 0) : (Math.Clamp(used / total * 100, 0, 100), used, total);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static (int? Percent, bool IsPluggedIn) ReadPower()
    {
        if (!GetSystemPowerStatus(out var status)) return (null, false);
        int? percent = status.BatteryLifePercent == byte.MaxValue ? null : status.BatteryLifePercent;
        return (percent, status.AcLineStatus == 1);
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;
    private static double ToGb(ulong bytes) => bytes / 1024d / 1024d / 1024d;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low; public uint High; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}

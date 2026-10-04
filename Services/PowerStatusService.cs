using System.Runtime.InteropServices;

namespace DynamicIsland.Services;

internal static class PowerStatusService
{
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
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    public static bool IsCharging()
    {
        return GetSystemPowerStatus(out var status) &&
               status.AcLineStatus == 1 &&
               status.BatteryFlag != 255 &&
               (status.BatteryFlag & 128) == 0 &&
               (status.BatteryFlag & 8) != 0;
    }
}

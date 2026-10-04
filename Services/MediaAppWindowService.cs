using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicIsland.Services;

public static class MediaAppWindowService
{
    public static (bool Opened, string Message) Open(string sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
            return (false, "This media session did not identify its player app.");

        nint bestWindow = 0;
        var bestScore = 0;
        _ = EnumWindows((window, unusedParameter) =>
        {
            if (!IsWindowVisible(window) || GetWindowTextLength(window) == 0 ||
                GetWindow(window, 4) != 0)
                return true;

            _ = GetWindowThreadProcessId(window, out var processId);
            if (processId == 0 || processId == (uint)Environment.ProcessId) return true;

            var score = MatchProcess((int)processId, sourceAppUserModelId);
            if (score > bestScore)
            {
                bestWindow = window;
                bestScore = score;
            }
            return true;
        }, 0);

        if (bestWindow != 0)
        {
            if (IsIconic(bestWindow)) _ = ShowWindow(bestWindow, 9);
            _ = BringWindowToTop(bestWindow);
            var activated = SetForegroundWindow(bestWindow) || GetForegroundWindow() == bestWindow;
            return activated
                ? (true, "Player window opened.")
                : (false, "Windows kept the player in the background; select it from the taskbar.");
        }

        // Packaged players may have no desktop window until activated. Shell activation
        // normally reuses the application's existing instance.
        if (sourceAppUserModelId.Contains('!'))
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo($"shell:AppsFolder\\{sourceAppUserModelId}")
                {
                    UseShellExecute = true
                });
                return (true, "Player app activated.");
            }
            catch (Exception exception)
            {
                return (false, $"Could not open the player: {exception.Message}");
            }
        }

        return (false, "No open window matched this media session's player.");
    }

    private static int MatchProcess(int processId, string sourceId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var appId = GetProcessAppId(processId);
            if (!string.IsNullOrWhiteSpace(appId) &&
                string.Equals(appId, sourceId, StringComparison.OrdinalIgnoreCase))
                return 100;

            string? executablePath = null;
            try { executablePath = process.MainModule?.FileName; }
            catch { /* Protected processes can still expose their name or app ID. */ }

            if (!string.IsNullOrWhiteSpace(executablePath) &&
                string.Equals(executablePath, sourceId, StringComparison.OrdinalIgnoreCase))
                return 90;

            var sourceFileName = Path.GetFileNameWithoutExtension(sourceId);
            if (string.Equals(process.ProcessName, sourceFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(process.ProcessName, sourceId, StringComparison.OrdinalIgnoreCase))
                return 70;
        }
        catch
        {
            // The process may have exited during enumeration.
        }
        return 0;
    }

    private static string? GetProcessAppId(int processId)
    {
        var process = OpenProcess(0x1000, false, processId);
        if (process == 0) return null;
        try
        {
            uint length = 0;
            if (GetApplicationUserModelId(process, ref length, null) != 122 || length is 0 or > 512)
                return null;
            var result = new StringBuilder((int)length);
            return GetApplicationUserModelId(process, ref length, result) == 0
                ? result.ToString() : null;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? appId);
}

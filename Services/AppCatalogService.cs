using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DynamicIsland.Models;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace DynamicIsland.Services;

public sealed class AppCatalogService
{
    private readonly Dictionary<string, AppListEntry> _packageEntries = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<InstalledApplicationInfo>> DiscoverAsync()
    {
        var applications = await Task.Run(DiscoverStartMenuShortcuts);
        AddRunningExecutableApplications(applications);
        try
        {
            var packageManager = new PackageManager();
            foreach (var package in packageManager.FindPackagesForUser(string.Empty))
            {
                IReadOnlyList<AppListEntry> entries;
                try
                {
                    entries = await package.GetAppListEntriesAsync();
                }
                catch
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    var name = entry.DisplayInfo.DisplayName;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(entry.AppUserModelId))
                    {
                        continue;
                    }

                    _packageEntries[entry.AppUserModelId] = entry;
                    applications.Add(new InstalledApplicationInfo
                    {
                        Name = name,
                        LaunchTarget = entry.AppUserModelId,
                        Kind = InstalledApplicationKind.PackagedApplication
                    });
                }
            }
        }
        catch
        {
            // Package enumeration can be restricted on managed systems; shortcuts remain available.
        }

        return applications
            .GroupBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<RunningApplicationInfo> DiscoverRunningWindows(int excludedProcessId)
    {
        var results = new List<RunningApplicationInfo>();
        _ = EnumWindows((handle, unusedParameter) =>
        {
            if (!IsWindowVisible(handle) || GetWindowTextLength(handle) == 0)
            {
                return true;
            }

            _ = GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0 || processId == excludedProcessId)
            {
                return true;
            }

            var title = new StringBuilder(GetWindowTextLength(handle) + 1);
            _ = GetWindowText(handle, title, title.Capacity);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                results.Add(new RunningApplicationInfo
                {
                    ProcessId = (int)processId,
                    WindowHandle = handle,
                    DisplayName = $"{title} — {process.ProcessName}"
                });
            }
            catch
            {
                // The window's process exited while it was being enumerated.
            }

            return true;
        }, 0);

        return results
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<nint> LaunchAndFindWindowAsync(InstalledApplicationInfo application)
    {
        var existing = EnumerateWindows();
        var existingHandles = existing.Select(item => item.Handle).ToHashSet();

        try
        {
            if (application.Kind == InstalledApplicationKind.PackagedApplication &&
                _packageEntries.TryGetValue(application.LaunchTarget, out var entry))
            {
                if (!await entry.LaunchAsync())
                {
                    return 0;
                }
            }
            else
            {
                _ = Process.Start(new ProcessStartInfo(application.LaunchTarget) { UseShellExecute = true });
            }
        }
        catch
        {
            return 0;
        }

        for (var attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(150);
            var windows = EnumerateWindows();
            var match = windows.FirstOrDefault(item =>
                item.Title.Contains(application.Name, StringComparison.CurrentCultureIgnoreCase));
            if (match.Handle != 0)
            {
                return match.Handle;
            }

            match = windows.FirstOrDefault(item => !existingHandles.Contains(item.Handle));
            if (match.Handle != 0)
            {
                return match.Handle;
            }
        }

        return 0;
    }

    private static List<InstalledApplicationInfo> DiscoverStartMenuShortcuts()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };
        var results = new List<InstalledApplicationInfo>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    results.Add(new InstalledApplicationInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        LaunchTarget = path,
                        Kind = InstalledApplicationKind.StartMenuShortcut
                    });
                }
            }
            catch
            {
                // Skip inaccessible Start menu folders.
            }
        }

        return results;
    }

    private static void AddRunningExecutableApplications(List<InstalledApplicationInfo> applications)
    {
        using var currentProcess = Process.GetCurrentProcess();
        var currentSessionId = currentProcess.SessionId;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != currentSessionId ||
                        string.IsNullOrWhiteSpace(process.MainModule?.FileName))
                    {
                        continue;
                    }

                    var path = process.MainModule.FileName;
                    var productName = process.MainModule.FileVersionInfo.ProductName;
                    var name = string.IsNullOrWhiteSpace(productName)
                        ? process.ProcessName
                        : productName;
                    name = name.Replace(".Root", string.Empty, StringComparison.OrdinalIgnoreCase);
                    applications.Add(new InstalledApplicationInfo
                    {
                        Name = name,
                        LaunchTarget = path,
                        Kind = InstalledApplicationKind.StartMenuShortcut
                    });
                }
                catch
                {
                    // Protected system processes are not launchable catalog entries.
                }
            }
        }
    }

    private static List<(nint Handle, string Title)> EnumerateWindows()
    {
        var windows = new List<(nint, string)>();
        _ = EnumWindows((handle, unusedParameter) =>
        {
            if (!IsWindowVisible(handle) || GetWindowTextLength(handle) == 0)
            {
                return true;
            }

            var title = new StringBuilder(GetWindowTextLength(handle) + 1);
            _ = GetWindowText(handle, title, title.Capacity);
            if (!string.IsNullOrWhiteSpace(title.ToString()))
            {
                windows.Add((handle, title.ToString()));
            }

            return true;
        }, 0);
        return windows;
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}

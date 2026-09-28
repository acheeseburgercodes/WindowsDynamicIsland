using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicIsland.Services;

public sealed class VirtualDesktopService
{
    public const string IslandAppId = "ChatGPT.DynamicIsland";

    // The documented desktop manager can move a window, but cannot keep it on
    // every desktop. Explorer's pinned-app service provides that behavior.
    public bool TryPinIslandToAllDesktops()
    {
        object? shell = null;
        object? pinnedAppsObject = null;
        try
        {
            var shellType = Type.GetTypeFromCLSID(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239"));
            if (shellType is null) return false;

            shell = Activator.CreateInstance(shellType);
            if (shell is not IServiceProvider shellServices) return false;

            var pinnedAppsId = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
            var pinnedInterfaceId = typeof(IVirtualDesktopPinnedApps).GUID;
            shellServices.QueryService(ref pinnedAppsId, ref pinnedInterfaceId, out pinnedAppsObject);
            if (pinnedAppsObject is not IVirtualDesktopPinnedApps pinnedApps) return false;

            if (!pinnedApps.IsAppIdPinned(IslandAppId))
            {
                pinnedApps.PinAppID(IslandAppId);
            }

            return pinnedApps.IsAppIdPinned(IslandAppId);
        }
        catch (Exception)
        {
            // Explorer's internal pinning interface is unavailable on some
            // Windows builds or under restrictive application-control policies.
            return false;
        }
        finally
        {
            if (pinnedAppsObject is not null && Marshal.IsComObject(pinnedAppsObject))
                _ = Marshal.ReleaseComObject(pinnedAppsObject);
            if (shell is not null && Marshal.IsComObject(shell))
                _ = Marshal.ReleaseComObject(shell);
        }
    }

    public bool MoveToIslandDesktop(nint applicationWindow, Window islandWindow)
    {
        if (applicationWindow == 0)
        {
            return false;
        }

        IVirtualDesktopManager? manager = null;
        try
        {
            manager = (IVirtualDesktopManager)(object)new VirtualDesktopManagerCom();
            var islandHandle = new WindowInteropHelper(islandWindow).Handle;
            if (islandHandle == 0 || manager.GetWindowDesktopId(islandHandle, out var desktopId) != 0)
            {
                return false;
            }

            return manager.MoveWindowToDesktop(applicationWindow, ref desktopId) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (manager is not null && Marshal.IsComObject(manager))
            {
                _ = Marshal.FinalReleaseComObject(manager);
            }
        }
    }

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        void QueryService(ref Guid serviceId, ref Guid interfaceId,
            [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport]
    [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopPinnedApps
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsAppIdPinned([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void PinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void UnpinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
    }

    [ComImport]
    [Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
    private sealed class VirtualDesktopManagerCom
    {
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(nint topLevelWindow, ref Guid desktopId);
    }
}

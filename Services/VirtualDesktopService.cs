using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicIsland.Services;

public sealed class VirtualDesktopService
{
    public const string IslandAppId = "ChatGPT.DynamicIsland";

    // Pin the actual window view. Pinning only the AppUserModelID is not enough
    // for this taskbar-hidden WPF window on some Windows 11 builds.
    public bool TryPinIslandToAllDesktops(Window islandWindow)
    {
        object? shell = null;
        object? pinnedAppsObject = null;
        object? viewsObject = null;
        nint view = 0;
        try
        {
            var islandHandle = new WindowInteropHelper(islandWindow).Handle;
            if (islandHandle == 0) return false;

            var shellType = Type.GetTypeFromCLSID(new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239"));
            if (shellType is null) return false;

            shell = Activator.CreateInstance(shellType);
            if (shell is not IServiceProvider shellServices) return false;

            var pinnedAppsId = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
            var pinnedInterfaceId = typeof(IVirtualDesktopPinnedApps).GUID;
            shellServices.QueryService(ref pinnedAppsId, ref pinnedInterfaceId, out pinnedAppsObject);
            if (pinnedAppsObject is not IVirtualDesktopPinnedApps pinnedApps) return false;

            var viewsId = typeof(IApplicationViewCollection).GUID;
            shellServices.QueryService(ref viewsId, ref viewsId, out viewsObject);
            if (viewsObject is not IApplicationViewCollection views ||
                views.GetViewForHwnd(islandHandle, out view) != 0 || view == 0)
                return false;

            if (!pinnedApps.IsViewPinned(view))
                pinnedApps.PinView(view);

            return pinnedApps.IsViewPinned(view);
        }
        catch (Exception)
        {
            // Explorer's internal pinning interface is unavailable on some
            // Windows builds or under restrictive application-control policies.
            return false;
        }
        finally
        {
            if (view != 0)
                _ = Marshal.Release(view);
            if (viewsObject is not null && Marshal.IsComObject(viewsObject))
                _ = Marshal.ReleaseComObject(viewsObject);
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
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsViewPinned(nint view);
        void PinView(nint view);
        void UnpinView(nint view);
    }

    [ComImport]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationViewCollection
    {
        [PreserveSig]
        int GetViews(out nint views);
        [PreserveSig]
        int GetViewsByZOrder(out nint views);
        [PreserveSig]
        int GetViewsByAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] string appId, out nint views);
        [PreserveSig]
        int GetViewForHwnd(nint window, out nint view);
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

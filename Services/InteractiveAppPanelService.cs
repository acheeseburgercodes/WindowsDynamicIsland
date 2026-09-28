using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace DynamicIsland.Services;

public sealed class InteractiveAppPanelService
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const nint WsCaption = 0x00C00000;
    private const nint WsThickFrame = 0x00040000;
    private const nint WsSysMenu = 0x00080000;
    private const nint WsMinimizeBox = 0x00020000;
    private const nint WsMaximizeBox = 0x00010000;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoSendChanging = 0x0400;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNotTopmost = new(-2);

    private nint _window;
    private nint _style;
    private nint _extendedStyle;
    private NativeRect _originalBounds;
    private bool _wasTopmost;
    private bool _isSuspended;

    public bool IsAttached => _window != 0 && IsWindow(_window);
    public string? LastError { get; private set; }

    public bool Attach(nint window, FrameworkElement viewport)
    {
        Detach();
        LastError = null;
        if (window == 0 || !IsWindow(window))
        {
            return false;
        }

        _window = window;
        _style = GetWindowLongPtr(window, GwlStyle);
        _extendedStyle = GetWindowLongPtr(window, GwlExStyle);
        _wasTopmost = (_extendedStyle.ToInt64() & 0x00000008L) != 0;
        _ = GetWindowRect(window, out _originalBounds);

        var panelStyle = _style & ~(WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
        _ = ShowWindow(window, 9);
        Marshal.SetLastPInvokeError(0);
        var previousStyle = SetWindowLongPtr(window, GwlStyle, panelStyle);
        if (previousStyle == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            LastError = "Windows refused access to this app's window. Check that both apps run with the same permissions.";
            Detach();
            return false;
        }
        _isSuspended = false;
        UpdateBounds(viewport);
        if (LastError is not null)
        {
            Detach();
            return false;
        }
        return true;
    }

    public void UpdateBounds(FrameworkElement viewport)
    {
        if (!IsAttached || _isSuspended || !viewport.IsVisible ||
            viewport.ActualWidth < 1 || viewport.ActualHeight < 1)
        {
            return;
        }

        var topLeft = viewport.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(viewport);
        var width = Math.Max(1, (int)Math.Round(viewport.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Round(viewport.ActualHeight * dpi.DpiScaleY));
        var positioned = SetWindowPos(
            _window,
            HwndTopmost,
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            width,
            height,
            SwpFrameChanged | SwpShowWindow | SwpNoActivate | SwpNoSendChanging);
        if (!positioned)
            LastError = "Windows could not position this application in the panel.";
    }

    public void Suspend()
    {
        if (!IsAttached || _isSuspended)
        {
            return;
        }

        _ = ShowWindow(_window, 0);
        _isSuspended = true;
    }

    public void Resume(FrameworkElement viewport)
    {
        if (!IsAttached)
        {
            return;
        }

        // Position while hidden, then reveal at the panel location without activating.
        _isSuspended = false;
        UpdateBounds(viewport);
    }

    public void Detach()
    {
        if (!IsAttached)
        {
            _window = 0;
            return;
        }

        var window = _window;
        _window = 0;
        _isSuspended = false;
        _ = SetWindowLongPtr(window, GwlStyle, _style);
        _ = SetWindowLongPtr(window, GwlExStyle, _extendedStyle);
        _ = SetWindowPos(
            window,
            _wasTopmost ? HwndTopmost : HwndNotTopmost,
            _originalBounds.Left,
            _originalBounds.Top,
            Math.Max(320, _originalBounds.Right - _originalBounds.Left),
            Math.Max(240, _originalBounds.Bottom - _originalBounds.Top),
            SwpFrameChanged | SwpShowWindow);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

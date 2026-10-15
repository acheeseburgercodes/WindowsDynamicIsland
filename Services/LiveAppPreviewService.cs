using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynamicIsland.Services;

/// <summary>
/// Displays a live DWM thumbnail for windows that cannot be resized into the interactive overlay.
/// The source stays visible to DWM, but is parked outside the virtual screen until detached.
/// </summary>
public sealed class LiveAppPreviewService
{
    private const uint TnpRectDestination = 0x00000001;
    private const uint TnpVisible = 0x00000008;
    private const uint TnpSourceClientAreaOnly = 0x00000010;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpHideWindow = 0x0080;
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExNoActivate = 0x08000000;

    private Window? _host;
    private nint _source;
    private nint _thumbnail;
    private NativeRect _originalBounds;
    private bool _wasMinimized;
    private NativeRect _lastDestination;
    private bool _hasThumbnailLayout;

    public bool IsAttached => _source != 0 && IsWindow(_source);
    public string? LastError { get; private set; }

    public bool Attach(nint source, Window owner, FrameworkElement viewport, bool startSuspended = false)
    {
        Detach();
        LastError = null;
        if (source == 0 || !IsWindow(source) || !GetWindowRect(source, out _originalBounds))
        {
            LastError = "The application window is no longer available.";
            return false;
        }

        _source = source;
        _wasMinimized = IsIconic(source);
        _hasThumbnailLayout = false;
        try
        {
            // Park the source in one hidden move before it can visibly shrink or slide.
            // DWM still needs it shown afterward, but it is then already off-screen.
            var parkedX = (int)SystemParameters.VirtualScreenLeft -
                          Math.Max(800, _originalBounds.Right - _originalBounds.Left) - 100;
            var parkedY = (int)SystemParameters.VirtualScreenTop;
            if (!SetWindowPos(source, 0, parkedX, parkedY, 0, 0,
                    SwpHideWindow | SwpNoSize | SwpNoZOrder | SwpNoActivate))
            {
                LastError = "Windows would not move the application out of view for preview.";
                Detach();
                return false;
            }
            _ = ShowWindow(source, _wasMinimized ? 9 : 4);

            _host = new Window
            {
                Owner = owner,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Background = Brushes.Black,
                Content = new Grid { Background = Brushes.Black },
                Width = Math.Max(1, viewport.ActualWidth),
                Height = Math.Max(1, viewport.ActualHeight),
                WindowStartupLocation = WindowStartupLocation.Manual
            };
            PositionHost(viewport);
            var hostHandle = new WindowInteropHelper(_host).EnsureHandle();
            var extendedStyle = GetWindowLongPtr(hostHandle, GwlExStyle).ToInt64();
            _ = SetWindowLongPtr(hostHandle, GwlExStyle,
                new nint(extendedStyle | WsExTransparent | WsExNoActivate));

            var result = DwmRegisterThumbnail(hostHandle, source, out _thumbnail);
            if (result < 0 || _thumbnail == 0)
            {
                LastError = $"Windows could not create a live preview (0x{result:X8}).";
                Detach();
                return false;
            }

            UpdateBounds(viewport);
            if (LastError is not null)
            {
                Detach();
                return false;
            }
            if (!startSuspended)
            {
                _host.Show();
                UpdateBounds(viewport, force: true);
            }
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Could not show the live preview: {exception.Message}";
            Detach();
            return false;
        }
    }

    public void UpdateBounds(FrameworkElement viewport, bool force = false)
    {
        if (_host is null || _thumbnail == 0 || !viewport.IsVisible ||
            viewport.ActualWidth < 1 || viewport.ActualHeight < 1)
            return;

        var hostChanged = PositionHost(viewport);
        var dpi = VisualTreeHelper.GetDpi(_host);
        var width = Math.Max(1, (int)Math.Round(_host.Width * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Round(_host.Height * dpi.DpiScaleY));
        var destination = new NativeRect { Left = 0, Top = 0, Right = width, Bottom = height };
        if (DwmQueryThumbnailSourceSize(_thumbnail, out var sourceSize) >= 0 &&
            sourceSize.Width > 0 && sourceSize.Height > 0)
        {
            var ratio = Math.Min((double)width / sourceSize.Width, (double)height / sourceSize.Height);
            var fittedWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * ratio));
            var fittedHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * ratio));
            destination.Left = (width - fittedWidth) / 2;
            destination.Top = (height - fittedHeight) / 2;
            destination.Right = destination.Left + fittedWidth;
            destination.Bottom = destination.Top + fittedHeight;
        }

        if (!force && !hostChanged && _hasThumbnailLayout &&
            destination.Left == _lastDestination.Left && destination.Top == _lastDestination.Top &&
            destination.Right == _lastDestination.Right && destination.Bottom == _lastDestination.Bottom)
            return;

        var properties = new ThumbnailProperties
        {
            Flags = TnpRectDestination | TnpVisible | TnpSourceClientAreaOnly,
            Destination = destination,
            Visible = true,
            SourceClientAreaOnly = true
        };
        var result = DwmUpdateThumbnailProperties(_thumbnail, ref properties);
        if (result < 0)
            LastError = $"Windows could not update the live preview (0x{result:X8}).";
        else
        {
            _lastDestination = destination;
            _hasThumbnailLayout = true;
        }
    }

    public void Suspend()
    {
        if (_host?.IsVisible == true)
            _host.Hide();
    }

    public void Resume(FrameworkElement viewport)
    {
        if (!IsAttached || _host is null) return;
        if (!_host.IsVisible)
        {
            PositionHost(viewport);
            _host.Show();
            UpdateBounds(viewport, force: true);
        }
        else
            UpdateBounds(viewport);
    }

    public void Detach()
    {
        if (_thumbnail != 0)
        {
            _ = DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = 0;
        }

        _host?.Close();
        _host = null;
        if (_source != 0 && IsWindow(_source))
        {
            _ = SetWindowPos(_source, 0, _originalBounds.Left, _originalBounds.Top, 0, 0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpShowWindow);
            if (_wasMinimized)
                _ = ShowWindow(_source, 6);
        }
        _source = 0;
        _wasMinimized = false;
        _hasThumbnailLayout = false;
    }

    private bool PositionHost(FrameworkElement viewport)
    {
        if (_host is null) return false;
        var point = viewport.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(viewport);
        var left = point.X / dpi.DpiScaleX;
        var top = point.Y / dpi.DpiScaleY;
        var width = Math.Max(1, viewport.ActualWidth);
        var height = Math.Max(1, viewport.ActualHeight);
        var changed = !double.IsFinite(_host.Left) || !double.IsFinite(_host.Top) ||
                      Math.Abs(_host.Left - left) > 0.5 || Math.Abs(_host.Top - top) > 0.5 ||
                      Math.Abs(_host.Width - width) > 0.5 || Math.Abs(_host.Height - height) > 0.5;
        if (!changed) return false;
        _host.Left = left;
        _host.Top = top;
        _host.Width = width;
        _host.Height = height;
        return true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(nint thumbnail);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);

    [DllImport("dwmapi.dll")]
    private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out NativeSize size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThumbnailProperties
    {
        public uint Flags;
        public NativeRect Destination;
        public NativeRect Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }
}

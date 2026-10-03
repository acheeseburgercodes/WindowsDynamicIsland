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
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExNoActivate = 0x08000000;

    private Window? _host;
    private nint _source;
    private nint _thumbnail;
    private NativeRect _originalBounds;
    private bool _wasMinimized;

    public bool IsAttached => _source != 0 && IsWindow(_source);
    public string? LastError { get; private set; }

    public bool Attach(nint source, Window owner, FrameworkElement viewport)
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
        try
        {
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
            _host.Show();
            var hostHandle = new WindowInteropHelper(_host).Handle;
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

            // DWM requires a visible source. Keep it on the current virtual desktop but off screen.
            var parkedX = (int)SystemParameters.VirtualScreenLeft -
                          Math.Max(800, _originalBounds.Right - _originalBounds.Left) - 100;
            var parkedY = (int)SystemParameters.VirtualScreenTop;
            if (!SetWindowPos(source, 0, parkedX, parkedY, 0, 0,
                    SwpNoSize | SwpNoZOrder | SwpNoActivate))
            {
                LastError = "Windows would not move the application out of view for preview.";
                Detach();
                return false;
            }

            if (_wasMinimized)
                _ = ShowWindow(source, 9);

            UpdateBounds(viewport);
            if (LastError is not null)
            {
                Detach();
                return false;
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

    public void UpdateBounds(FrameworkElement viewport)
    {
        if (_host is null || _thumbnail == 0 || !viewport.IsVisible ||
            viewport.ActualWidth < 1 || viewport.ActualHeight < 1)
            return;

        PositionHost(viewport);
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
    }

    public void Suspend() => _host?.Hide();

    public void Resume(FrameworkElement viewport)
    {
        if (!IsAttached || _host is null) return;
        PositionHost(viewport);
        _host.Show();
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
                SwpNoSize | SwpNoZOrder | SwpNoActivate);
            if (_wasMinimized)
                _ = ShowWindow(_source, 6);
        }
        _source = 0;
        _wasMinimized = false;
    }

    private void PositionHost(FrameworkElement viewport)
    {
        if (_host is null) return;
        var point = viewport.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(viewport);
        _host.Left = point.X / dpi.DpiScaleX;
        _host.Top = point.Y / dpi.DpiScaleY;
        _host.Width = Math.Max(1, viewport.ActualWidth);
        _host.Height = Math.Max(1, viewport.ActualHeight);
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

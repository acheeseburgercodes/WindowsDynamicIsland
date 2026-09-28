using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DynamicIsland.Core;

namespace DynamicIsland.Services;

public sealed class WindowService
{
    private const int DwmwaBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;

    private readonly Window _window;

    public WindowService(Window window)
    {
        _window = window;
    }

    public IslandEdge Edge { get; set; } = IslandEdge.Top;

    public IslandAlignment Alignment { get; set; } = IslandAlignment.Center;

    public int HorizontalOffset { get; set; }

    public int VerticalOffset { get; set; } = 8;

    public void Configure()
    {
        _window.SourceInitialized += (_, _) => { RemoveNativeBorder(); EnsureTopmost(); };
        _window.Topmost = true;
        _window.ShowInTaskbar = false;
        _window.ResizeMode = ResizeMode.NoResize;
    }

    public void RemoveNativeBorder()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == 0)
        {
            return;
        }

        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            hwnd,
            DwmwaBorderColor,
            ref borderColor,
            Marshal.SizeOf<uint>());
    }

    public void EnsureTopmost()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == 0) return;
        // Raise without taking focus from the app receiving keyboard input.
        _ = SetWindowPos(hwnd, new nint(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    public void ResizeAndPosition(double width, double height)
    {
        var workArea = SystemParameters.WorkArea;
        var anchor = GetAnchorPosition(workArea, width, height);

        _window.Width = width;
        _window.Height = height;
        _window.Left = Math.Clamp(
            anchor.X + HorizontalOffset,
            workArea.Left,
            workArea.Right - width);
        _window.Top = Math.Clamp(
            anchor.Y + VerticalOffset,
            workArea.Top,
            workArea.Bottom - height);
    }

    public (int HorizontalOffset, int VerticalOffset) CaptureDraggedPosition(double width, double height)
    {
        var anchor = GetAnchorPosition(SystemParameters.WorkArea, width, height);
        HorizontalOffset = (int)Math.Round(_window.Left - anchor.X);
        VerticalOffset = (int)Math.Round(_window.Top - anchor.Y);
        return (HorizontalOffset, VerticalOffset);
    }

    private Point GetAnchorPosition(Rect workArea, double width, double height)
    {
        const double margin = 8;
        var horizontalStart = workArea.Left + margin;
        var horizontalCenter = workArea.Left + ((workArea.Width - width) / 2);
        var horizontalEnd = workArea.Right - width - margin;
        var verticalStart = workArea.Top + margin;
        var verticalCenter = workArea.Top + ((workArea.Height - height) / 2);
        var verticalEnd = workArea.Bottom - height - margin;

        var x = Edge switch
        {
            IslandEdge.Left => horizontalStart,
            IslandEdge.Right => horizontalEnd,
            _ => Alignment switch
            {
                IslandAlignment.Start => horizontalStart,
                IslandAlignment.End => horizontalEnd,
                _ => horizontalCenter
            }
        };

        var y = Edge switch
        {
            IslandEdge.Top => verticalStart,
            _ => Alignment switch
            {
                IslandAlignment.Start => verticalStart,
                IslandAlignment.End => verticalEnd,
                _ => verticalCenter
            }
        };

        return new Point(x, y);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int attribute,
        ref uint attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y,
        int width, int height, uint flags);
}

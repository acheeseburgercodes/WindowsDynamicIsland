using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace DynamicIsland.Services;

public sealed class WindowService
{
    private readonly Window _window;
    private readonly AppWindow _appWindow;

    public WindowService(Window window)
    {
        _window = window;
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
    }

    public int TopOffset { get; set; } = 8;

    public void Configure(int width, int height)
    {
        _window.ExtendsContentIntoTitleBar = true;
        _window.SetTitleBar(null);

        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        _appWindow.SetPresenter(presenter);

        ResizeAndCenter(width, height);
    }

    public void ResizeAndCenter(int width, int height)
    {
        var workArea = DisplayArea.Primary.WorkArea;
        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = workArea.Y + TopOffset;
        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }
}

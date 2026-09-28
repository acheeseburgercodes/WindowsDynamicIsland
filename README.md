# Dynamic Island for Windows

A small, always-on-top WPF island for Windows 11. Its per-pixel-transparent window avoids the known WinUI transparent-window outline while preserving native Windows media, file, and clipboard integration.

## Features

- Active Windows media-session preview with previous, play/pause, and next controls
- Durable file shelf with drag-in and drag-out, file picker, clipboard paste, open, copy, and remove actions
- Global island drop target: files can be dropped on the idle pill and are copied into app-managed storage
- Editable Windows text clipboard view with copy and paste controls
- Persistent app runner with normal launch and interactive running-window selection
- Interactive panel mode that places the real application surface inside the island and restores it on detach
- Suspended panel sessions stay hidden when the island collapses and return only inside the Apps panel
- Installed-app catalog sourced from Start menu shortcuts, packaged apps, and launchable running executables
- One-session virtual-desktop following: the same app window moves to the island's desktop without relaunching or duplicating data
- The island itself is pinned across Windows virtual desktops, so one running instance remains accessible after switching desktops
- Adjustable translucent island surface with locally persisted opacity, native accent color picker, and presets
- Persisted dark and light appearance modes
- Top, left, or right screen anchoring with start/center/end alignment and X/Y calibration
- Three-stage interaction: mini idle pill, compact hover preview, and explicit full expansion
- Click-and-hold dragging with saved screen position, automatic collapse, and an explicit Exit control
- Per-pixel-transparent, borderless top-center window with top-center-origin eased scale transitions

Docked files are independent copies stored under `%LOCALAPPDATA%\DynamicIsland\FileDock`. They remain there across restarts and do not depend on the original source. Removing an item from the shelf deletes only this managed copy.

Drag a row from the Files tab into a browser or File Explorer. The island hands Windows a standard file-drop list with the path of the persistent copy and stays open until the drag finishes. If a website does not accept the drop, use **Open stored folder** and select that file with the site's upload button.

## Requirements

- Windows 11
- Visual Studio 2022 or later with the **.NET desktop development** workload
- .NET 10 SDK

The app uses the .NET Windows Desktop runtime and does not require the Windows App SDK runtime.

## Build and run

The GitHub Releases page provides a self-contained Windows x64 executable and a portable ZIP containing the same executable. Extract the ZIP before running it. These release downloads do not require a separate .NET installation. They are currently unsigned, so Windows Smart App Control may block them until a trusted signed build is available.

Open `DynamicIsland.csproj` in Visual Studio, select `x64`, then build and run. From a Developer PowerShell with the .NET 10 SDK installed:

```powershell
dotnet restore
dotnet build -c Release -p:Platform=x64
dotnet run -c Release -p:Platform=x64
```

Start media in an app that publishes a Windows system media session (for example Spotify or a browser playing supported audio/video). The island updates automatically.

## Structure

- `Core/IslandManager.cs` owns the extensible island state machine.
- `Services/WindowService.cs` owns the transparent borderless WPF window, DWM border suppression, and screen positioning.
- `Services/MediaSessionService.cs` owns event-driven Windows media-session discovery and playback commands.
- `Services/ConfigurationService.cs` persists appearance, placement, apps, and shelf metadata under local app data.
- `Services/FileDockStorageService.cs` owns the durable managed copies used by the file shelf.
- `Services/ClipboardService.cs` integrates text and normal Windows file clipboard formats.
- `Models/MediaSnapshot.cs` is the UI-independent media model.
- `MainWindow.xaml` and its code-behind render and animate the island.

The Apps panel uses the application's actual window for direct mouse and keyboard input. All in-panel launch actions use this path; clicking the panel never activates a separate thumbnail view. The island maintains its topmost position without taking keyboard focus, and the active application surface is stacked above its panel. Collapse hides the application until the panel is reopened. Open externally restores the desktop window. Windows secure desktop and exclusive full-screen surfaces are outside ordinary topmost ordering.

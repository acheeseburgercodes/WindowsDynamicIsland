# Dynamic Island for Windows

A small, always-on-top WPF island for Windows 11. Its per-pixel-transparent window avoids the known WinUI transparent-window outline while preserving native Windows media, file, and clipboard integration.

The application source is licensed under the [MIT License](LICENSE). Self-contained downloads also include the .NET runtime's license and third-party notices; see [third-party notices](THIRD-PARTY-NOTICES.md).

## Features

- Active Windows media-session preview with previous, play/pause, and next controls
- Durable file shelf with drag-in and drag-out, file picker, clipboard paste, open, copy, and remove actions
- Global island drop target: files can be dropped on the idle pill and are copied into app-managed storage
- Editable Windows text clipboard view with copy and paste controls
- Persistent app runner with normal launch and interactive running-window selection
- Interactive panel mode for compatible desktop windows, with a live preview fallback for WhatsApp
- Suspended panel sessions stay hidden when the island collapses and return only inside the Apps panel
- Installed-app catalog sourced from Start menu shortcuts, packaged apps, and launchable running executables
- One-session virtual-desktop following: the same app window moves to the island's desktop without relaunching or duplicating data
- The island's actual window view is pinned across Windows virtual desktops, so one running instance remains accessible after switching desktops
- Adjustable translucent island surface with locally persisted opacity, native accent color picker, and presets
- Optional two-color gradient surface with hex or picker controls and selectable direction
- Persisted dark and light appearance modes
- Top, left, or right screen anchoring with start/center/end alignment and X/Y calibration
- Three-stage interaction: mini idle pill, compact hover preview, and animated full expansion
- Fade-and-slide animations when switching the full island's tabs
- Optional custom picture for the minimized mini logo, copied into local app storage
- Resolution-independent vector icons for navigation, playback, and the music-logo option
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

For a signed Release build, install an RSA code-signing certificate from a trusted certificate provider in `Cert:\CurrentUser\My`, then provide its thumbprint:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:CodeSigningCertificateThumbprint=YOUR_CERTIFICATE_THUMBPRINT
```

The opt-in build target signs and verifies both the Release build executable and the published executable using `signtool.exe` from the Windows SDK. It does not create certificates or alter Windows trust stores. A locally self-signed certificate is not sufficient for Smart App Control; Microsoft requires a certificate issued by a trusted provider. `Unblock-File` only removes the Mark of the Web and cannot resolve a Code Integrity policy block.

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

The Apps panel uses the actual window for direct mouse and keyboard input where supported. WhatsApp's packaged window instead uses a live Windows DWM preview because it cannot reliably be resized into the interactive overlay. That preview is view-only; **Open externally** restores its window for interaction. Collapse hides either kind of panel session until the Apps panel is reopened. Windows secure desktop and exclusive full-screen surfaces are outside ordinary topmost ordering.

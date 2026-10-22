# Dynamic Island for Windows

A small, always-on-top WPF island for Windows 11. Its per-pixel-transparent window avoids the known WinUI transparent-window outline while preserving native Windows media, file, and clipboard integration.

The application source is licensed under the [MIT License](LICENSE). Self-contained downloads also include the .NET runtime's license and third-party notices; see [third-party notices](THIRD-PARTY-NOTICES.md).

Project policies: [code signing](docs/CODE_SIGNING_POLICY.md), [SignPath Foundation onboarding](docs/SIGNPATH_ONBOARDING.md), and [privacy](docs/PRIVACY.md).

## Features

- Album artwork, live timeline and seeking where supported, media controls, and a button to open the source player
- Compact dashboard rail with wheel-driven vertical switching between media, weather, system, activity, controls, focus, files, clipboard, apps, and settings
- On-demand weather conditions from wttr.in; a **Use my location** button asks the Windows location service for a device fix and falls back to wttr.in's approximate network location when access is unavailable
- Live CPU, memory, primary-storage, battery/power, and network-throughput telemetry without a background collector when the System or Controls dashboard is closed
- Activity stack for playback, focus sessions, persistent files, and real file-dock copy progress
- Quick controls for native output volume and microphone mute, plus direct Windows surfaces for audio, Wi-Fi, display, Bluetooth, Focus Assist, and screen capture
- Pomodoro-style focus timer with 5, 15, and 25 minute presets and a compact top-edge countdown state
- Persistent task checklist with per-item completion, delete, and clear-completed; tasks are stored with the island's own configuration
- Notepad panel with multi-line notes, click-to-edit, delete, and the same on-device configuration storage
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
- Independently customizable solid background color, including the expanded atmospheric shading
- Optional two-color gradient surface with hex or picker controls and selectable direction
- Persisted dark and light appearance modes
- Top, left, or right screen anchoring with start/center/end alignment and X/Y calibration
- Three-stage interaction: nearly invisible 2px idle handle, compact hover preview, and animated full expansion
- Fade-and-slide animations when switching the full island's tabs
- Optional custom picture for the minimized mini logo, copied into local app storage
- Photo crop-and-rotate editor before saving a custom mini logo
- Liquid-metal border on the mini pill and compact music preview, fluid-style native dropdowns, and a ticked opacity slider
- Resolution-independent vector icons for navigation, playback, and the music-logo option
- Click-and-hold dragging with saved screen position, automatic collapse, and an explicit Exit control
- Per-pixel-transparent, borderless top-center window with top-center-origin eased scale transitions
- Separate React Native Windows + TypeScript control center for placement, appearance, and TempVault storage
- Optional copy-to-TempVault action on dock files; auto-copy can be enabled in the control center

Docked files are independent copies stored under `%LOCALAPPDATA%\DynamicIsland\FileDock`. They remain there across restarts and do not depend on the original source. Removing an item from the shelf deletes only this managed copy.

Drag a row from the Files tab into a browser or File Explorer. The island hands Windows a standard file-drop list with the path of the persistent copy and stays open until the drag finishes. If a website does not accept the drop, use **Open stored folder** and select that file with the site's upload button.

## Install

Download `WindowsDynamicIsland-v0.3.0-win-x64-setup.exe` from the GitHub Releases page and run it. The installer adds the island and its Control Center to the Start menu, can launch the island at sign-in, and registers **Windows Dynamic Island** in Windows Settings under **Apps > Installed apps** and in Control Panel under **Programs and Features**. Use either Windows surface to uninstall it normally.

The installer is self-contained, so running the island does not require a separate .NET installation. The included Control Center is installed beside the island and is opened from the island's Settings panel or its Start-menu shortcut.

## Development requirements

- Windows 11
- Visual Studio 2022 or later with the **.NET desktop development** workload
- .NET 10 SDK

The app uses the .NET Windows Desktop runtime and does not require the Windows App SDK runtime.

## Build and run

GitHub Releases provides a self-contained Windows x64 installer. Release downloads do not require a separate .NET installation.

For a signed Release build, install an RSA code-signing certificate from a trusted certificate provider in `Cert:\CurrentUser\My`, then provide its thumbprint:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:CodeSigningCertificateThumbprint=YOUR_CERTIFICATE_THUMBPRINT
```

The opt-in build target signs and verifies both the Release build executable and the published executable using `signtool.exe` from the Windows SDK. It does not create certificates or alter Windows trust stores. A locally self-signed certificate is not sufficient for Smart App Control; Microsoft requires a certificate issued by a trusted provider. `Unblock-File` only removes the Mark of the Web and cannot resolve a Code Integrity policy block.

To create the complete installer locally, install Inno Setup 6 and run:

```powershell
.\tools\Build-Installer.ps1 -Version 0.3.0
```

Pass `-CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT` to sign the published app and installer. The resulting setup executable and SHA-256 file are written to `artifacts\installer`.

Open `DynamicIsland.csproj` in Visual Studio, select `x64`, then build and run. From a Developer PowerShell with the .NET 10 SDK installed:

```powershell
dotnet restore
dotnet build -c Release -p:Platform=x64
dotnet run -c Release -p:Platform=x64
```

Start media in an app that publishes a Windows system media session (for example Spotify or a browser playing supported audio/video). The island updates automatically.

The default top-center placement is a screen-edge notch. Hover to reveal the media strip and click the notch or strip to open the full panel. Clicking outside returns it to the smallest state. Use the compact left rail or the mouse wheel at a dashboard edge to move vertically between panels. Every dashboard, including Apps, uses the configured compact panel size. A new track briefly reveals the media strip. The Settings tab includes a notification-animation test.

The companion source and Windows project are in [`IslandSettings`](IslandSettings/README.md). Building its Windows package requires Visual Studio with C++ and Windows SDK components in addition to the .NET SDK. Start the island before opening the companion, then use **Open control center** in Settings or launch IslandSettings from Start. Its fixed-size window edits placement, panel dimensions, appearance, the cropped mini-logo picture, notifications, and TempVault through a loopback-only bridge. The default TempVault root is `D:\TempVault\storage`; an existing `Inbox` is required before exports are enabled.
If the companion is not installed, **Open control center** reveals the island's built-in position and appearance controls instead of opening an unregistered Windows link.

System-wide notification reading requires an MSIX-packaged build declaring the User Notification Listener capability and a separate permission grant from the user. The portable EXE cannot access other apps' notifications; its Settings tab reports this and disables the permission button. Notification content is not written to the island's configuration.

The `Packaging` manifest defines an optional MSIX with that capability. Run `tools/Build-IslandMsix.ps1 -CertificateThumbprint <thumbprint>` to publish the self-contained .NET 10 app, create the MSIX with MakeAppx, and sign it. The certificate must be a trusted code-signing certificate with subject `CN=LOQ`. The packaged app asks for Windows notification access only when **Enable alerts** is clicked in Settings.

## Structure

- `Core/IslandManager.cs` owns the extensible island state machine.
- `Services/WindowService.cs` owns the transparent borderless WPF window, DWM border suppression, and screen positioning.
- `Services/MediaSessionService.cs` owns event-driven Windows media-session discovery and playback commands.
- `Services/WeatherService.cs` loads current conditions only when requested.
- `Services/HardwareMonitorService.cs` samples local CPU, memory, storage, and power state.
- `Services/ConfigurationService.cs` persists appearance, placement, apps, and shelf metadata under local app data.
- `Services/FileDockStorageService.cs` owns the durable managed copies used by the file shelf.
- `Services/ClipboardService.cs` integrates text and normal Windows file clipboard formats.
- `Models/MediaSnapshot.cs` is the UI-independent media model.
- `MainWindow.xaml` and its code-behind render and animate the island.

The Apps panel uses the actual window for direct mouse and keyboard input where supported. WhatsApp's packaged window instead uses a live Windows DWM preview because it cannot reliably be resized into the interactive overlay. That preview is view-only; **Open externally** restores its window for interaction. Collapse hides either kind of panel session until the Apps panel is reopened. Windows secure desktop and exclusive full-screen surfaces are outside ordinary topmost ordering.

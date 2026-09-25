# Dynamic Island for Windows

A small, always-on-top WinUI 3 island for Windows 11. It expands on hover to show the active Windows media session, album artwork, and previous/play-pause/next controls.

## Requirements

- Windows 11
- Visual Studio 2022 or later with **.NET desktop development** and **Windows application development** workloads
- .NET 10 SDK

The project uses a self-contained Windows App SDK deployment, so users do not need to install the Windows App Runtime separately.

## Build and run

Open `DynamicIsland.csproj` in Visual Studio, select `x64`, then build and run. From a Developer PowerShell with the .NET 10 SDK installed:

```powershell
dotnet restore
dotnet build -c Release -p:Platform=x64
dotnet run -c Release -p:Platform=x64
```

Start media in an app that publishes a Windows system media session (for example Spotify or a browser playing supported audio/video). The island updates automatically.

## Structure

- `Core/IslandManager.cs` owns the extensible island state machine.
- `Services/WindowService.cs` owns borderless, always-on-top window sizing and primary-display positioning.
- `Services/MediaSessionService.cs` owns event-driven Windows media-session discovery and playback commands.
- `Models/MediaSnapshot.cs` is the UI-independent media model.
- `MainWindow.xaml` and its code-behind render and animate the island.

The next interaction milestone can add click-outside detection, configurable positioning, keyboard accessibility, and persisted animation preferences.

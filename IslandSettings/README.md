# Island Control Center

React Native Windows + TypeScript companion for the WPF Dynamic Island.

## Build

Install [Visual Studio 2022 Community](https://aka.ms/vs/17/release/vs_community.exe) (or use **Modify** in Visual Studio Installer if it is already installed). Include **Desktop development with C++**, **MSVC v143 x64/x86**, **C++ (v143) Universal Windows Platform tools**, **MSIX Packaging Tools**, and **Windows 11 SDK 10.0.22621.0**. The .NET SDK alone cannot compile `windows/IslandSettings.sln`.

```powershell
cd IslandSettings
npm ci
npx tsc --noEmit
npx react-native run-windows --release
```

For a packaged Release build, restore the solution in Visual Studio, then build it as **Release | x64**. The resulting MSIX is under `windows/IslandSettings.Package/AppPackages`. A local MSIX must be signed with a code-signing certificate whose subject matches the manifest publisher (`CN=LOQ`) and whose root is trusted by Windows before `Add-AppxPackage` can install it. Trusting a self-signed certificate machine-wide is a security-sensitive local development step; do not use the local test certificate for public releases. Developer Mode is an alternative for unsigned local deployment.

Run the WPF island first. The companion connects to its loopback-only settings endpoint at `127.0.0.1:38691`. The packaged app registers `dynamicisland-settings:` so **Open control center** in the island can launch it. If you distribute an unpackaged build, install its complete output beside `DynamicIsland.exe` (not just the companion EXE).

## TempVault

Set the root to your TempVault storage directory (normally `D:\TempVault\storage`), which must already contain `Inbox`. The island sends a **copy** of each selected dock file through that Inbox. TempVault owns that copy after it is sorted; the island's durable dock copy remains until you remove it. Auto-copy is optional and off by default.

The companion only edits live configuration fields through the local bridge; it never writes the island's configuration JSON or dock metadata directly.

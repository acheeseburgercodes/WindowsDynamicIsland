# Island Control Center

React Native Windows + TypeScript companion for the WPF Dynamic Island.

## Build

Install Visual Studio 2022 with the Desktop development with C++ and Universal Windows Platform workloads, plus a current Windows SDK. This is required for the generated `windows/IslandSettings.sln`; the .NET SDK alone cannot compile it.

```powershell
cd IslandSettings
npm ci
npx tsc --noEmit
npx react-native run-windows --release
```

Run the WPF island first. The companion connects to its loopback-only settings endpoint at `127.0.0.1:38691`. The packaged app registers `dynamicisland-settings:` so **Open control center** in the island can launch it. If you distribute an unpackaged build, install its complete output beside `DynamicIsland.exe` (not just the companion EXE).

## TempVault

Set the root to your TempVault storage directory (normally `D:\TempVault\storage`), which must already contain `Inbox`. The island sends a **copy** of each selected dock file through that Inbox. TempVault owns that copy after it is sorted; the island's durable dock copy remains until you remove it. Auto-copy is optional and off by default.

The companion only edits live configuration fields through the local bridge; it never writes the island's configuration JSON or dock metadata directly.

# Contributing

Use the MIT license for contributions and keep upstream notices. Submit a
focused pull request against main with the behavior changed, reason, and tests.
Contributions from outside trusted committers need maintainer review.

## Build

Use Windows 11, .NET 10 SDK, Node.js 22.11 or newer, Visual Studio 2022 C++
desktop and .NET desktop workloads, the Windows SDK, and Inno Setup 6.
Run npm ci in IslandSettings before building the Windows solution.
See README.md and docs/RELEASING.md for the complete commands.

## Checks

Run dotnet build -c Release -p:Platform=x64 and
dotnet run --project .checks/PanelChecks.csproj -c Release.
In IslandSettings, run npm test -- --runInBand and npx tsc --noEmit.
For build or installer changes, run tools/Build-Installer.ps1 from a clean
checkout and review the artifact inventory. Never use signing secrets in PR CI.

Document new network traffic, data storage, permissions, or system changes in
PRIVACY.md and the installer disclosure. Keep external services opt-in.
Do not submit generated binaries, node_modules, personal configuration,
credentials, or unrelated Windows troubleshooting scripts.

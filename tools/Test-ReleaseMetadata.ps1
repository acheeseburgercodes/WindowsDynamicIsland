[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$StageDirectory,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$manifest = Get-Content -LiteralPath (Join-Path $StageDirectory 'build.json') -Raw | ConvertFrom-Json
if ($manifest.version -ne $Version) { throw 'Staged version differs from requested version.' }
foreach ($relative in @('app\DynamicIsland.exe', 'control-center\IslandSettings.exe')) {
    $file = Get-Item -LiteralPath (Join-Path $StageDirectory $relative)
    $info = $file.VersionInfo
    if ($info.ProductName -ne 'Windows Dynamic Island' -or $info.ProductVersion -ne $Version) {
        throw "Unexpected metadata in $relative : product=$($info.ProductName), version=$($info.ProductVersion)"
    }
}
foreach ($relative in @('legal\DOTNET-LICENSE.txt', 'legal\DOTNET-THIRD-PARTY-NOTICES.txt', 'legal\dependency-inventory.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $StageDirectory $relative))) { throw "Missing $relative" }
}
Write-Host "Release metadata verified: $Version"

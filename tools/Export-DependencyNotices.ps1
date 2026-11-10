[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$records = [System.Collections.Generic.List[object]]::new()

function Copy-Notices([string]$Directory, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Directory)) { throw "Dependency directory missing: $Directory" }
    $files = @(Get-ChildItem -LiteralPath $Directory -File |
        Where-Object { $_.Name -match '^(licen[sc]e|copying|notice|third.?party|copyright)([.\-_]|$)' })
    if ($files.Count) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        $files | Copy-Item -Destination $Destination -Force
    }
    return @($files.Name)
}

# Include development dependencies as well: Metro/Babel can contribute runtime
# helpers to the JS bundle. This inventory is conservative, not a legal verdict.
$lock = Get-Content -LiteralPath (Join-Path $root 'IslandSettings\package-lock.json') -Raw | ConvertFrom-Json
foreach ($entry in $lock.packages.PSObject.Properties) {
    if (-not $entry.Name) { continue }
    $metadata = $entry.Value
    $license = if ($metadata.PSObject.Properties['license']) { $metadata.license } else { 'REVIEW REQUIRED' }
    $folder = Join-Path (Join-Path $root 'IslandSettings') $entry.Name
    $destination = Join-Path $OutputDirectory ('npm\' + $entry.Name.Replace('/', '\'))
    $notices = @(Copy-Notices $folder $destination)
    $records.Add(@{ ecosystem='npm'; path=$entry.Name; version=$metadata.version; license=$license; notices=$notices })
}

$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$seen = @{}
foreach ($lockPath in @(
    'IslandSettings\windows\IslandSettings\packages.lock.json',
    'IslandSettings\windows\IslandSettings.Package\packages.lock.json'
)) {
    $nativeLock = Get-Content -LiteralPath (Join-Path $root $lockPath) -Raw | ConvertFrom-Json
    foreach ($framework in $nativeLock.dependencies.PSObject.Properties) {
        foreach ($entry in $framework.Value.PSObject.Properties) {
            $key = ($entry.Name + '/' + $entry.Value.resolved).ToLowerInvariant()
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true
            $folder = Join-Path $nugetRoot $key
            $notices = @(Copy-Notices $folder (Join-Path $OutputDirectory ('nuget\' + $key)))
            $spec = Get-ChildItem -LiteralPath $folder -Filter '*.nuspec' | Select-Object -First 1
            $license = 'REVIEW REQUIRED'
            if ($spec) {
                [xml]$xml = Get-Content -LiteralPath $spec.FullName -Raw
                $node = $xml.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='license']")
                if ($node) {
                    $license = $node.InnerText
                    if ($node.GetAttribute('type') -eq 'file') {
                        $licensePath = [IO.Path]::GetFullPath((Join-Path $folder $license))
                        if (-not $licensePath.StartsWith([IO.Path]::GetFullPath($folder).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
                            throw "Unsafe license path in $key"
                        }
                        $dest = Join-Path $OutputDirectory ('nuget\' + $key)
                        New-Item -ItemType Directory -Path $dest -Force | Out-Null
                        Copy-Item -LiteralPath $licensePath -Destination $dest -Force
                    }
                }
            }
            $records.Add(@{ ecosystem='nuget'; package=$entry.Name; version=$entry.Value.resolved; license=$license; notices=$notices })
        }
    }
}
$records | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'dependency-inventory.json') -Encoding utf8
Write-Host "Collected notices and inventory for $($records.Count) dependency entries; maintainer license review still required."

param(
    [string]$CertificateThumbprint = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactRoot = Join-Path $projectRoot 'artifacts\msix'
$stage = Join-Path $artifactRoot ('staging-' + [guid]::NewGuid().ToString('N'))
$packagePath = Join-Path $artifactRoot 'WindowsDynamicIsland_0.1.2.0_x64.msix'
$sdkBin = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'

New-Item -ItemType Directory -Path $stage -Force | Out-Null
& dotnet publish (Join-Path $projectRoot 'DynamicIsland.csproj') -c Release -r win-x64 `
    --self-contained true -p:Platform=x64 -o $stage
if ($LASTEXITCODE -ne 0) { throw 'The island publish failed.' }

Copy-Item -LiteralPath (Join-Path $projectRoot 'Packaging\Images') -Destination (Join-Path $stage 'Images') -Recurse
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Packaging\Package.appxmanifest') -Raw
$manifest = $manifest.Replace('$targetnametoken$.exe', 'DynamicIsland.exe')
$manifest = $manifest.Replace('$targetentrypoint$', 'Windows.FullTrustApplication')
[System.IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest)

$packOutput = & (Join-Path $sdkBin 'makeappx.exe') pack /d $stage /p $packagePath /o 2>&1
if ($LASTEXITCODE -ne 0) { throw 'The island MSIX pack failed.' }
Write-Output ($packOutput | Select-Object -Last 2)

if ($CertificateThumbprint) {
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq $CertificateThumbprint
    if (!$certificate -or $certificate.Subject -ne 'CN=LOQ') {
        throw 'The signing certificate must exist in CurrentUser\My and have publisher CN=LOQ.'
    }
    & (Join-Path $sdkBin 'signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint /s My $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'The island MSIX signing failed.' }
}

Write-Output $packagePath

[CmdletBinding()]
param(
    [string]$Version = "0.2.0",
    [ValidateSet("x64")]
    [string]$Architecture = "x64",
    [string]$CertificateThumbprint
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot "artifacts"
$stageRoot = Join-Path $artifactsRoot "installer-stage"
$appStage = Join-Path $stageRoot "app"
$controlCenterStage = Join-Path $stageRoot "control-center"
$legalStage = Join-Path $stageRoot "legal"
$installerOutput = Join-Path $artifactsRoot "installer"
$installerScript = Join-Path $repoRoot "installer\WindowsDynamicIsland.iss"

function Reset-ArtifactDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
    $resolvedTarget = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    if (-not $resolvedTarget.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a directory outside the repository artifacts folder: $resolvedTarget"
    }

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw "Visual Studio Installer's vswhere.exe was not found. Install Visual Studio 2022 with Desktop development with C++."
    }

    $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    if (-not $path) {
        throw "MSBuild was not found. Install the Visual Studio Desktop development with C++ workload."
    }
    return $path
}

function Find-InnoCompiler {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    $path = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $path) {
        throw "Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup --exact"
    }
    return $path
}

Reset-ArtifactDirectory -Path $stageRoot
Reset-ArtifactDirectory -Path $installerOutput
New-Item -ItemType Directory -Path $appStage, $controlCenterStage, $legalStage -Force | Out-Null

$publishArguments = @(
    "publish", (Join-Path $repoRoot "DynamicIsland.csproj"),
    "-c", "Release",
    "-r", "win-$Architecture",
    "--self-contained", "true",
    "-o", $appStage,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:Version=$Version"
)
if ($CertificateThumbprint) {
    $publishArguments += "-p:CodeSigningCertificateThumbprint=$CertificateThumbprint"
}

Write-Host "Publishing the island..."
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$msbuild = Find-MSBuild
$solution = Join-Path $repoRoot "IslandSettings\windows\IslandSettings.sln"
Write-Host "Building the Control Center..."
& $msbuild $solution /m /restore /p:Configuration=Release /p:Platform=$Architecture /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Control Center build failed with exit code $LASTEXITCODE." }

$controlCenterOutput = Join-Path $repoRoot "IslandSettings\windows\$Architecture\Release"
if (-not (Test-Path -LiteralPath (Join-Path $controlCenterOutput "IslandSettings.exe"))) {
    throw "The Control Center output was not found at $controlCenterOutput."
}

Get-ChildItem -LiteralPath $controlCenterOutput -File |
    Where-Object { $_.Extension -notin ".pdb", ".map" } |
    Copy-Item -Destination $controlCenterStage -Force
Copy-Item -LiteralPath (Join-Path $controlCenterOutput "Bundle") -Destination $controlCenterStage -Recurse -Force

$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
$dotnetLicense = Join-Path $dotnetRoot "LICENSE.txt"
$dotnetNotices = Join-Path $dotnetRoot "ThirdPartyNotices.txt"
if (Test-Path -LiteralPath $dotnetLicense) {
    Copy-Item -LiteralPath $dotnetLicense -Destination (Join-Path $legalStage "DOTNET-LICENSE.txt") -Force
}
if (Test-Path -LiteralPath $dotnetNotices) {
    Copy-Item -LiteralPath $dotnetNotices -Destination (Join-Path $legalStage "DOTNET-THIRD-PARTY-NOTICES.txt") -Force
}

$iscc = Find-InnoCompiler
Write-Host "Compiling the Windows installer..."
& $iscc "/DMyAppVersion=$Version" "/DRepoRoot=$repoRoot" "/DStageDir=$stageRoot" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

$installer = Join-Path $installerOutput "WindowsDynamicIsland-v$Version-win-x64-setup.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "The expected installer was not produced: $installer"
}

if ($CertificateThumbprint) {
    Write-Host "Signing the installer..."
    & (Join-Path $PSScriptRoot "Sign-Executable.ps1") -FilePath $installer -CertificateThumbprint $CertificateThumbprint
}

$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
$hashFile = "$installer.sha256"
Set-Content -LiteralPath $hashFile -Value "$hash  $(Split-Path -Leaf $installer)" -Encoding ascii

Write-Host "Installer: $installer"
Write-Host "SHA-256:  $hash"

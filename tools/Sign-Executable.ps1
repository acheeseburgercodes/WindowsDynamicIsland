param(
    [Parameter(Mandatory = $true)]
    [string] $FilePath,

    [Parameter(Mandatory = $true)]
    [string] $CertificateThumbprint,

    [string] $SignToolPath,

    [string] $TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$executable = (Resolve-Path -LiteralPath $FilePath).Path
$thumbprint = $CertificateThumbprint -replace '\s', ''
if ($thumbprint -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'CertificateThumbprint must be a 40-character SHA-1 certificate thumbprint.'
}

$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$thumbprint" -ErrorAction SilentlyContinue
if (-not $certificate -or -not $certificate.HasPrivateKey) {
    throw 'The specified certificate and private key must be available in Cert:\CurrentUser\My.'
}
if ($certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
    throw 'The code-signing certificate is not currently valid.'
}
$codeSigningOid = '1.3.6.1.5.5.7.3.3'
if (-not @($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $codeSigningOid }).Count) {
    throw 'The specified certificate does not have the Code Signing enhanced key usage.'
}

if ($SignToolPath) {
    $signTool = (Resolve-Path -LiteralPath $SignToolPath).Path
} else {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) {
        $signTool = $command.Source
    } else {
        $sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
        $signTool = Get-ChildItem -LiteralPath $sdkBin -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
    }
}
if (-not $signTool) {
    throw 'signtool.exe was not found. Install the Windows SDK or pass -SignToolPath.'
}

& $signTool sign /sha1 $thumbprint /s My /fd SHA256 /tr $TimestampUrl /td SHA256 $executable
if ($LASTEXITCODE -ne 0) { throw "SignTool signing failed with exit code $LASTEXITCODE." }

& $signTool verify /pa /v $executable
if ($LASTEXITCODE -ne 0) { throw "SignTool verification failed with exit code $LASTEXITCODE." }

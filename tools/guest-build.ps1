# Run in an interactive PowerShell session inside the Windows 11 ARM guest.
# Downloads only the official Microsoft SDK ZIP and verifies its published SHA-512.
param(
    [string]$Source = '\\vmware-host\Shared Folders\VelaSource',
    [string]$OfflineSdkZip = '',
    [string]$Exchange = '\\vmware-host\Shared Folders\VelaBuildExchange'
)
$ErrorActionPreference = 'Stop'
$checkout = Join-Path $env:USERPROFILE 'source\Vela'
$sdkDirectory = Join-Path $env:LOCALAPPDATA 'VelaBuild\dotnet'
New-Item -ItemType Directory -Force $checkout, $sdkDirectory | Out-Null
if ($Source.EndsWith('.zip')) { Expand-Archive $Source -DestinationPath $checkout -Force }
else {
    robocopy $Source $checkout /E /XD .git bin obj artifacts .vs /R:1 /W:1
    if ($LASTEXITCODE -gt 7) { throw 'Could not copy the source checkout from the read-only shared folder.' }
}
if (-not (Test-Path (Join-Path $sdkDirectory 'dotnet.exe'))) {
    if ($OfflineSdkZip) {
        $expected = (Get-Content "$OfflineSdkZip.sha512" -Raw).Trim()
        if ((Get-FileHash $OfflineSdkZip -Algorithm SHA512).Hash -ne $expected) { throw 'Offline SDK checksum verification failed.' }
        Expand-Archive $OfflineSdkZip -DestinationPath $sdkDirectory -Force
    }
    else {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $release = $metadata.releases | Where-Object { $_.sdk.version -eq $metadata.'latest-sdk' } | Select-Object -First 1
    $archive = $release.sdk.files | Where-Object { $_.rid -eq 'win-arm64' -and $_.name.EndsWith('.zip') } | Select-Object -First 1
    if (-not $archive -or -not $archive.url.StartsWith('https://builds.dotnet.microsoft.com/')) { throw 'Unexpected SDK archive metadata.' }
    $zip = Join-Path $env:TEMP 'vela-dotnet-sdk.zip'
    Invoke-WebRequest $archive.url -OutFile $zip
    if ((Get-FileHash $zip -Algorithm SHA512).Hash -ne $archive.hash) { throw 'SDK checksum verification failed.' }
    Expand-Archive $zip -DestinationPath $sdkDirectory -Force
    }
}
$env:DOTNET_ROOT = $sdkDirectory
$env:PATH = "$sdkDirectory;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
Push-Location $checkout
try {
    dotnet --info
    & ./tools/build.ps1 -Architecture arm64
    if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
    if (Test-Path $exchange) {
        robocopy (Join-Path $checkout 'artifacts\Vela-arm64') (Join-Path $exchange 'Vela-arm64') /E /R:1 /W:1
        if ($LASTEXITCODE -gt 7) { throw 'Build completed, but the result could not be copied to the exchange folder.' }
    }
    Write-Host "Ready: $checkout\artifacts\Vela-arm64\Vela.Windows.exe"
}
finally { Pop-Location }

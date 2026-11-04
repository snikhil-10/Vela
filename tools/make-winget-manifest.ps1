param(
    [Parameter(Mandatory)][string]$X64Url,
    [string]$Arm64Url,
    [string]$Version = '0.2.2'
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be a numeric major.minor.patch release version.' }
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "artifacts/winget/$Version"
$installers = @(@{ Architecture='x64'; Url=$X64Url })
if ($Arm64Url) { $installers += @{ Architecture='arm64'; Url=$Arm64Url } }
foreach ($installer in $installers) {
    $uri = $null
    if (-not [Uri]::TryCreate($installer.Url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment -or $installer.Url -match '[\r\n]') {
        throw 'Installer URLs must be absolute HTTPS URLs without credentials or fragments.'
    }
    $path = Join-Path $root "artifacts/installers/Vela-Setup-$Version-$($installer.Architecture).exe"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Installer not found: $path" }
    $installer.Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
function QuoteYaml([string]$value) { return "'" + $value.Replace("'", "''") + "'" }
$installerLines = @(
    '# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json',
    '# Generated from locally built installers. Verify the URLs serve these exact files before submission.',
    'PackageIdentifier: snikhil10.Vela',
    "PackageVersion: $Version",
    'MinimumOSVersion: 10.0.22621.0',
    'InstallerType: inno',
    'Scope: user',
    'ProductCode: Vela.Utilities.Setup_is1',
    'Dependencies:',
    '  PackageDependencies:',
    '    - PackageIdentifier: Microsoft.VCRedist.2015+.x64',
    'Installers:'
)
foreach ($installer in $installers) {
    $installerLines += '  - Architecture: ' + $installer.Architecture
    $installerLines += '    InstallerUrl: ' + (QuoteYaml $installer.Url)
    $installerLines += '    InstallerSha256: ' + $installer.Hash
}
$installerLines += @('ManifestType: installer','ManifestVersion: 1.12.0')
$versionLines = @('# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json', 'PackageIdentifier: snikhil10.Vela', "PackageVersion: $Version", 'DefaultLocale: en-US', 'ManifestType: version', 'ManifestVersion: 1.12.0')
$localeLines = @(
    '# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json',
    'PackageIdentifier: snikhil10.Vela', "PackageVersion: $Version", 'PackageLocale: en-US',
    'Publisher: snikhil-10', 'PublisherUrl: https://github.com/snikhil-10',
    'PackageName: Vela for Windows', 'PackageUrl: https://github.com/snikhil-10/Vela',
    'License: GPL-3.0-or-later', 'LicenseUrl: https://github.com/snikhil-10/Vela/blob/main/LICENSE',
    'ShortDescription: A development preview of a Windows 11 tray app for everyday workflows.',
    'ManifestType: defaultLocale', 'ManifestVersion: 1.12.0'
)
New-Item -ItemType Directory -Path $out -Force | Out-Null
foreach ($item in @(
    @{ Name='snikhil10.Vela.yaml'; Lines=$versionLines },
    @{ Name='snikhil10.Vela.installer.yaml'; Lines=$installerLines },
    @{ Name='snikhil10.Vela.locale.en-US.yaml'; Lines=$localeLines }
)) {
    [IO.File]::WriteAllText((Join-Path $out $item.Name), (($item.Lines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
}
Write-Output $out

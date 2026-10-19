param(
    [ValidateSet('x64','arm64')][string]$Architecture = 'x64',
    [switch]$Run
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -and $env:OS -ne 'Windows_NT') { throw 'The WinUI XAML compiler requires Windows. Run this script inside your Windows VM.' }
$repo = Split-Path $PSScriptRoot -Parent
$platform = if ($Architecture -eq 'arm64') { 'ARM64' } else { 'x64' }
$output = Join-Path $repo "artifacts/Vela-$Architecture"
Push-Location $repo
try {
    $executable = Join-Path $output 'Vela.Windows.exe'
    if (Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable }) {
        throw 'Exit Vela through Settings / General / Exit before rebuilding this output folder. No files were changed.'
    }
    $sourceManifest = Join-Path $repo 'build-source-manifest.json'
    $publishedManifest = Join-Path $output 'compiled-source-manifest.json'
    if (Test-Path $publishedManifest) { Remove-Item $publishedManifest }
    if (Test-Path $sourceManifest) {
        $snapshot = Get-Content $sourceManifest -Raw | ConvertFrom-Json
        foreach ($file in $snapshot.files.PSObject.Properties) {
            if ((Get-FileHash $file.Name -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.Value) {
                throw "Source mismatch $($file.Name)"
            }
        }
        Write-Host "Verified build snapshot $($snapshot.snapshotId)"
    }
    dotnet run --project tests/Vela.Core.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core regression checks failed.' }
    dotnet publish src/Vela.Windows/Vela.Windows.csproj -c Release -p:Platform=$platform -r "win-$Architecture" --self-contained true -o $output
    if ($LASTEXITCODE -ne 0) { throw 'The Windows build failed.' }
    dotnet publish src/Vela.OcrWorker/Vela.OcrWorker.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $output 'Ocr')
    if ($LASTEXITCODE -ne 0) { throw 'The OCR worker build failed.' }
    Copy-Item tools/install-portable.cmd (Join-Path $output 'Install.cmd')
    Copy-Item tools/uninstall-portable.cmd (Join-Path $output 'Uninstall.cmd')
    Copy-Item LICENSE, NOTICE.md $output
    Copy-Item docs/windows-feature-audit.md $output
    Copy-Item docs/feature-audit.json, docs/verification.md, docs/hardware-support.md $output
    Copy-Item docs/portable-readme.md (Join-Path $output 'README.md')
    if (Test-Path $sourceManifest) { Copy-Item $sourceManifest $publishedManifest }
    Write-Host "Built $output/Vela.Windows.exe"
    if ($Run) { & (Join-Path $output 'Vela.Windows.exe') }
}
finally { Pop-Location }

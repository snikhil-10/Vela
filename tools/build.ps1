param(
    [ValidateSet('x64','arm64')][string]$Architecture = 'x64',
    [switch]$Run,
    [string]$Root=(Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -and $env:OS -ne 'Windows_NT') { throw 'The WinUI XAML compiler requires Windows. Run this script inside your Windows VM.' }
$repo = [IO.Path]::GetFullPath($Root)
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
    & ([scriptblock]::Create((Get-Content -LiteralPath (Join-Path $repo 'tools/source-manifest.ps1') -Raw))) -Mode Verify -Root $repo
    dotnet run --project tests/Vela.Core.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core regression checks failed.' }
    if (Test-Path $publishedManifest) { Remove-Item -LiteralPath $publishedManifest }
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
    New-Item -ItemType Directory -Path (Join-Path $output 'docs'),(Join-Path $output 'validation') -Force | Out-Null
    Get-ChildItem -LiteralPath docs -File | Copy-Item -Destination (Join-Path $output 'docs') -Force
    Get-ChildItem -LiteralPath validation -File -Filter '*.json' | Copy-Item -Destination (Join-Path $output 'validation') -Force
    if (Test-Path $sourceManifest) { Copy-Item $sourceManifest $publishedManifest }
    $taskSnapshot=Get-Content -LiteralPath $sourceManifest -Raw | ConvertFrom-Json
    $taskSdk=(& dotnet --version).Trim()
    if($LASTEXITCODE -ne 0) { throw 'Could not record the SDK version.' }
    $taskProvenance=[ordered]@{
        sourceSnapshot=$taskSnapshot.snapshotId
        sourceInputs=@($taskSnapshot.files.PSObject.Properties).Count
        sdk=$taskSdk
        architecture=$Architecture
        builtAt=[DateTimeOffset]::Now.ToString('o')
        unreleasedPreview=$true
        coreChecks='passed'
        runtimeChecks='Run check-windows and desktop checks separately; a build does not establish runtime support.'
        compiledManifestSha256=(Get-FileHash -LiteralPath $publishedManifest -Algorithm SHA256).Hash.ToLowerInvariant()
        appDllSha256=(Get-FileHash -LiteralPath (Join-Path $output 'Vela.Windows.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
        ocrWorkerArchitecture='x64'
        ocrWorkerExeSha256=(Get-FileHash -LiteralPath (Join-Path $output 'Ocr/Vela.OcrWorker.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    [IO.File]::WriteAllText((Join-Path $output 'build-provenance.json'),(($taskProvenance | ConvertTo-Json -Depth 10).Replace("`r`n","`n")+"`n"),[Text.UTF8Encoding]::new($false))
    Write-Host "Built $output/Vela.Windows.exe"
    if ($Run) { & (Join-Path $output 'Vela.Windows.exe') }
}
finally { Pop-Location }

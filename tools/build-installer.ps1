param(
    [ValidateSet('x64','arm64')][string]$Architecture = 'x64',
    [string]$Compiler = $env:VELA_ISCC
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$payload = Join-Path $root "artifacts/Vela-$Architecture"
$output = Join-Path $root 'artifacts/installers'
if (-not $Compiler) { $Compiler = Join-Path $root 'artifacts/build-tools/InnoSetup-6.7.4/ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compiler -PathType Leaf)) { throw 'Inno Setup 6 is required. Set VELA_ISCC to the full path to ISCC.exe.' }
& ([scriptblock]::Create((Get-Content -LiteralPath (Join-Path $root 'tools/source-manifest.ps1') -Raw))) -Mode Verify -Root $root
foreach ($file in @('Vela.Windows.exe','Vela.Windows.dll','Vela.Windows.pri','Ocr/Vela.OcrWorker.exe','compiled-source-manifest.json','build-provenance.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Build the complete $Architecture app before packaging: missing $file." }
}
if ((Get-FileHash (Join-Path $root 'build-source-manifest.json')).Hash -ne (Get-FileHash (Join-Path $payload 'compiled-source-manifest.json')).Hash) {
    throw 'The app build is from a different source snapshot. Rebuild it before packaging.'
}
$provenance = Get-Content -LiteralPath (Join-Path $payload 'build-provenance.json') -Raw | ConvertFrom-Json
if ($provenance.architecture -ne $Architecture -or
    $provenance.appDllSha256 -ne (Get-FileHash (Join-Path $payload 'Vela.Windows.dll') -Algorithm SHA256).Hash -or
    $provenance.ocrWorkerExeSha256 -ne (Get-FileHash (Join-Path $payload 'Ocr/Vela.OcrWorker.exe') -Algorithm SHA256).Hash) {
    throw 'The app or OCR worker does not match its build record. Rebuild before packaging.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $Compiler "/DPayloadDir=$payload" "/DOutputDir=$output" "/DBuildArch=$Architecture" '/DAppVersion=0.2.2' (Join-Path $root 'tools/installer/Vela.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Get-FileHash (Join-Path $output "Vela-Setup-0.2.2-$Architecture.exe") -Algorithm SHA256

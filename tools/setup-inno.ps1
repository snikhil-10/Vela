$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root 'artifacts/build-tools/InnoSetup-6.7.4'
$compiler = Join-Path $directory 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) { Write-Output $compiler; return }
$download = Join-Path $root 'artifacts/build-tools/innosetup-6.7.4.exe'
New-Item -ItemType Directory -Path (Split-Path $download) -Force | Out-Null
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_4/innosetup-6.7.4.exe' -OutFile $download
$hash = (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($hash -ne 'A549929319F9120716677C685535CA4671E4B937B5FDC87E2585F32D8EB30E8C' -or
    $signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|,\s*)CN=Pyrsys B\.V\.(,|$)') {
    throw 'Inno Setup download failed its pinned hash or publisher signature check.'
}
$arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',('/DIR="' + $directory + '"'))
$process = Start-Process -FilePath $download -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw "Inno Setup installation failed with exit code $($process.ExitCode)." }
Write-Output $compiler

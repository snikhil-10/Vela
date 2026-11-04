param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$setup = (Resolve-Path -LiteralPath $Installer).Path
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Vela.Utilities.Setup_is1'
$legacyKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Vela.Utilities'
if ((Test-Path $key) -or (Test-Path $legacyKey) -or (Get-Process Vela.Windows -ErrorAction SilentlyContinue)) {
    throw 'An existing Vela installation or running Vela prevents this isolated installer check.'
}
$parent = Join-Path ([IO.Path]::GetTempPath()) ('Vela-Installer-Test-' + [guid]::NewGuid().ToString('N'))
$target = Join-Path $parent 'installed'
$profile = Join-Path $env:LOCALAPPDATA 'Vela/settings.json'
$before = if (Test-Path -LiteralPath $profile) { (Get-FileHash -LiteralPath $profile).Hash } else { 'absent' }
New-Item -ItemType Directory -Path $parent | Out-Null
$arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="' + $target + '"'))
try {
    $result = Start-Process -FilePath $setup -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($result.ExitCode -ne 0) { throw "Silent install failed: $($result.ExitCode)." }
    foreach ($file in @('Vela.Windows.exe','Vela.Windows.dll','Vela.Windows.pri','Ocr/Vela.OcrWorker.exe','README.md','NOTICE.md','LICENSE','unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $target $file) -PathType Leaf)) { throw "Missing installed file: $file" }
    }
    foreach ($file in @('docs','validation','feature-audit.json','Install.cmd','Uninstall.cmd','verification.md','windows-feature-audit.md')) {
        if (Test-Path -LiteralPath (Join-Path $target $file)) { throw "Unexpected installed file or directory: $file" }
    }
    $markdown = @(Get-ChildItem -LiteralPath $target -Filter '*.md' -File | Select-Object -ExpandProperty Name)
    if ($markdown.Count -ne 2 -or 'README.md' -notin $markdown -or 'NOTICE.md' -notin $markdown) { throw 'Unexpected documentation in installed package.' }
    $entry = Get-ItemProperty -LiteralPath $key
    if ($entry.InstallLocation.TrimEnd('\') -ine $target.TrimEnd('\')) { throw 'Incorrect registered install location.' }
    $upgrade = Start-Process -FilePath $setup -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($upgrade.ExitCode -ne 0) { throw "Silent upgrade failed: $($upgrade.ExitCode)." }
    $sentinel = Join-Path $target 'user-file.txt'
    Set-Content -LiteralPath $sentinel -Value 'keep'
    $uninstall = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru -WindowStyle Hidden
    if ($uninstall.ExitCode -ne 0) { throw "Silent uninstall failed: $($uninstall.ExitCode)." }
    $after = if (Test-Path -LiteralPath $profile) { (Get-FileHash -LiteralPath $profile).Hash } else { 'absent' }
    if ($before -ne $after) { throw 'Normal Vela settings changed.' }
    if ((Test-Path $key) -or -not (Test-Path -LiteralPath $sentinel) -or (Test-Path -LiteralPath (Join-Path $target 'Vela.Windows.exe'))) {
        throw 'Uninstall failed to remove the app or preserve the unrelated file.'
    }
    Remove-Item -LiteralPath $sentinel
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    Remove-Item -LiteralPath $parent
    Write-Output 'PASS silent install, registration, upgrade, uninstall, unrelated file preservation, and unchanged normal settings.'
}
finally {
    $entry = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
    if ($entry -and $entry.InstallLocation.TrimEnd('\') -ieq $target.TrimEnd('\') -and (Test-Path -LiteralPath (Join-Path $target 'unins000.exe'))) {
        Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -WindowStyle Hidden
    }
}

@echo off
setlocal
set "velaArch=%~1"
if "%velaArch%"=="" set "velaArch=x64"
if /i "%velaArch%"=="arm64" goto architecture_ok
if /i "%velaArch%"=="x64" goto architecture_ok
echo Usage: tools\install.cmd [x64^|arm64]
exit /b 2
:architecture_ok
pushd "%~dp0.." || exit /b 1
if not exist "artifacts\Vela-%velaArch%\Vela.Windows.exe" goto missing
if not exist "artifacts\Vela-%velaArch%\Vela.Windows.pri" goto missing
set "velaInstallDirectory=%LOCALAPPDATA%\Programs\Vela"
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $running=Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue; if ($running) { Write-Host 'Exit Vela from its tray menu before installing or updating.'; exit 1 }; exit 0"
if errorlevel 1 goto failed
robocopy "artifacts\Vela-%velaArch%" "%velaInstallDirectory%" /E /R:1 /W:1 /NFL /NDL /NJH /NJS
if errorlevel 8 goto failed
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $shell=New-Object -ComObject WScript.Shell; foreach ($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) { $link=$shell.CreateShortcut((Join-Path $folder 'Vela.lnk')); $link.TargetPath=Join-Path $env:velaInstallDirectory 'Vela.Windows.exe'; $link.WorkingDirectory=$env:velaInstallDirectory; $link.IconLocation=Join-Path $env:velaInstallDirectory 'Assets\Vela.ico'; $link.Description='Vela for Windows'; $link.Save() }"
if errorlevel 1 goto failed
echo Installed %velaInstallDirectory%\Vela.Windows.exe
echo Desktop and Start menu shortcuts created. Sign-in startup remains your choice in Vela Settings.
popd
exit /b 0
:missing
echo A complete publish folder is required. Run tools\build.cmd %velaArch% first.
:failed
popd
exit /b 1

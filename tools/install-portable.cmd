@echo off
setlocal
set "velaSource=%~dp0"
set "velaTarget=%LOCALAPPDATA%\Programs\Vela"
if not exist "%velaSource%Vela.Windows.pri" goto incomplete
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; if(Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue) { Write-Host 'Exit Vela from its tray menu before installing or updating.'; exit 1 }; $src=$env:velaSource.TrimEnd('\'); $target=$env:velaTarget; if($src -ine $target) { New-Item -ItemType Directory -Path $target -Force | Out-Null; Get-ChildItem -LiteralPath $src | Copy-Item -Destination $target -Recurse -Force }; $shell=New-Object -ComObject WScript.Shell; foreach($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) { $link=$shell.CreateShortcut((Join-Path $folder 'Vela.lnk')); $link.TargetPath=Join-Path $target 'Vela.Windows.exe'; $link.WorkingDirectory=$target; $link.IconLocation=Join-Path $target 'Assets\Vela.ico'; $link.Save() }; $run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; if(Get-ItemProperty -Path $run -Name 'Vela.Utilities' -ErrorAction SilentlyContinue) { Set-ItemProperty $run 'Vela.Utilities' ([string][char]34+(Join-Path $target 'Vela.Windows.exe')+[char]34+' --startup') }; $key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Vela.Utilities'; New-Item $key -Force | Out-Null; New-ItemProperty $key DisplayName -Value 'Vela for Windows' -Force | Out-Null; New-ItemProperty $key DisplayVersion -Value '0.2.2' -Force | Out-Null; New-ItemProperty $key DisplayIcon -Value (Join-Path $target 'Assets\Vela.ico') -Force | Out-Null; New-ItemProperty $key InstallLocation -Value $target -Force | Out-Null; New-ItemProperty $key UninstallString -Value ([string][char]34+$env:ComSpec+[char]34+' /d /c '+[char]34+[char]34+(Join-Path $target 'Uninstall.cmd')+[char]34+[char]34) -Force | Out-Null; New-ItemProperty $key NoModify -PropertyType DWord -Value 1 -Force | Out-Null; New-ItemProperty $key NoRepair -PropertyType DWord -Value 1 -Force | Out-Null"
if errorlevel 1 goto failed
echo Installed Vela for your Windows account. Settings were preserved.
start "" "%velaTarget%\Vela.Windows.exe"
exit /b 0
:incomplete
echo Extract the complete Vela ZIP before running Install.cmd.
:failed
pause
exit /b 1

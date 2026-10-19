@echo off
setlocal
set "velaTarget=%LOCALAPPDATA%\Programs\Vela"
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; if(Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue) { Write-Host 'Exit Vela from its tray menu before uninstalling.'; exit 1 }; $target=$env:velaTarget; $shell=New-Object -ComObject WScript.Shell; foreach($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) { $path=Join-Path $folder 'Vela.lnk'; if((Test-Path -LiteralPath $path) -and ($shell.CreateShortcut($path).TargetPath -ieq (Join-Path $target 'Vela.Windows.exe'))) { Remove-Item -LiteralPath $path } }; Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'Vela.Utilities' -ErrorAction SilentlyContinue; Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Vela.Utilities' -Recurse -ErrorAction SilentlyContinue; Write-Host 'Vela shortcuts and startup were removed. Your settings remain in LocalAppData\Vela.'"
if errorlevel 1 goto failed
rem Run outside the install folder so Windows can release this command file first.
start "" /D "%TEMP%" powershell -NoProfile -Command "Start-Sleep -Seconds 2; $target=Join-Path $env:LOCALAPPDATA 'Programs\Vela'; if(-not (Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue)) { Remove-Item -LiteralPath $target -Recurse -Force }"
exit /b 0
:failed
pause
exit /b 1

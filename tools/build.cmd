@echo off
setlocal
set "velaArch=%~1"
if "%velaArch%"=="" set "velaArch=x64"
if /i "%velaArch%"=="arm64" goto architecture_ok
if /i "%velaArch%"=="x64" goto architecture_ok
echo Usage: tools\build.cmd [x64^|arm64]
exit /b 2
:architecture_ok
set "velaPlatform=%velaArch%"
if /i "%velaArch%"=="arm64" set "velaPlatform=ARM64"
pushd "%~dp0.." || exit /b 1
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $velaOutput=[IO.Path]::GetFullPath('artifacts\Vela-%velaArch%\Vela.Windows.exe'); if(Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $velaOutput }){throw 'Exit Vela through Settings / General / Exit before rebuilding this output folder. No files were changed.'}"
if errorlevel 1 goto failed
if exist "artifacts\Vela-%velaArch%\compiled-source-manifest.json" (
    del "artifacts\Vela-%velaArch%\compiled-source-manifest.json" || goto failed
)
if exist build-source-manifest.json (
    powershell -NoProfile -Command "$ErrorActionPreference='Stop'; Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -Force; $m=Get-Content build-source-manifest.json -Raw|ConvertFrom-Json; foreach($f in $m.files.PSObject.Properties){if((Get-FileHash $f.Name -Algorithm SHA256).Hash.ToLowerInvariant() -ne $f.Value){throw ('Source mismatch '+$f.Name)}}; Write-Host ('Verified build snapshot '+$m.snapshotId)"
    if errorlevel 1 goto failed
)
dotnet run --project tests/Vela.Core.Tests -c Release
if errorlevel 1 goto failed
dotnet publish src/Vela.Windows/Vela.Windows.csproj -c Release -p:Platform=%velaPlatform% -r win-%velaArch% --self-contained true -o artifacts/Vela-%velaArch%
if errorlevel 1 goto failed
dotnet publish src/Vela.OcrWorker/Vela.OcrWorker.csproj -c Release -r win-x64 --self-contained true -o artifacts/Vela-%velaArch%/Ocr
if errorlevel 1 goto failed
copy /y tools\install-portable.cmd "artifacts\Vela-%velaArch%\Install.cmd" >nul || goto failed
copy /y tools\uninstall-portable.cmd "artifacts\Vela-%velaArch%\Uninstall.cmd" >nul || goto failed
copy /y LICENSE "artifacts\Vela-%velaArch%\LICENSE" >nul || goto failed
copy /y NOTICE.md "artifacts\Vela-%velaArch%\NOTICE.md" >nul || goto failed
copy /y docs\windows-feature-audit.md "artifacts\Vela-%velaArch%\windows-feature-audit.md" >nul || goto failed
copy /y docs\feature-audit.json "artifacts\Vela-%velaArch%\feature-audit.json" >nul || goto failed
copy /y docs\hardware-support.md "artifacts\Vela-%velaArch%\hardware-support.md" >nul || goto failed
copy /y docs\verification.md "artifacts\Vela-%velaArch%\verification.md" >nul || goto failed
copy /y docs\portable-readme.md "artifacts\Vela-%velaArch%\README.md" >nul || goto failed
if exist build-source-manifest.json (
    copy /y build-source-manifest.json "artifacts\Vela-%velaArch%\compiled-source-manifest.json" >nul || goto failed
)
echo Built artifacts\Vela-%velaArch%\Vela.Windows.exe
popd
exit /b 0
:failed
echo Build failed. See the diagnostics above.
popd
exit /b 1

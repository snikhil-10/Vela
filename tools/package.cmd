@echo off
setlocal
set "velaArch=%~1"
if "%velaArch%"=="" set "velaArch=x64"
if /i "%velaArch%"=="arm64" goto architecture_ok
if /i "%velaArch%"=="x64" goto architecture_ok
echo Usage: tools\package.cmd [x64^|arm64]
exit /b 2
:architecture_ok
pushd "%~dp0.." || exit /b 1
if not exist "artifacts\Vela-%velaArch%\Vela.Windows.exe" goto missing
if not exist "artifacts\Vela-%velaArch%\Vela.Windows.dll" goto missing
if not exist "artifacts\Vela-%velaArch%\Vela.Windows.pri" goto missing
if exist build-source-manifest.json (
    powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $expected=(Get-FileHash 'build-source-manifest.json' -Algorithm SHA256).Hash; $actual=(Get-FileHash 'artifacts\Vela-%velaArch%\compiled-source-manifest.json' -Algorithm SHA256).Hash; if($actual -ne $expected){throw 'Published output belongs to another source snapshot. Rebuild before packaging.'}; $m=Get-Content build-source-manifest.json -Raw|ConvertFrom-Json; foreach($f in $m.files.PSObject.Properties){if((Get-FileHash $f.Name -Algorithm SHA256).Hash.ToLowerInvariant() -ne $f.Value){throw ('Source changed since build snapshot: '+$f.Name)}}"
    if errorlevel 1 goto failed
)
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; Compress-Archive -Path 'artifacts\Vela-%velaArch%' -DestinationPath 'artifacts\Vela-%velaArch%.zip' -Force; Get-FileHash 'artifacts\Vela-%velaArch%.zip' -Algorithm SHA256 | Format-List"
set "velaResult=%ERRORLEVEL%"
popd
exit /b %velaResult%
:failed
echo Package refused. Rebuild the current source before packaging.
popd
exit /b 1
:missing
echo A complete publish folder is required. Run tools\build.cmd %velaArch% first.
popd
exit /b 1

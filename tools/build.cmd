@echo off
setlocal
set "velaArch=%~1"
if "%velaArch%"=="" set "velaArch=x64"
if /i "%velaArch%"=="arm64" goto architecture_ok
if /i "%velaArch%"=="x64" goto architecture_ok
echo Usage: tools\build.cmd [x64^|arm64]
exit /b 2
:architecture_ok
pushd "%~dp0.." || exit /b 1
powershell -NoProfile -Command "& ([scriptblock]::Create((Get-Content -LiteralPath 'tools/build.ps1' -Raw))) -Architecture '%velaArch%' -Root (Get-Location).Path"
set "velaResult=%ERRORLEVEL%"
popd
exit /b %velaResult%

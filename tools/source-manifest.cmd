@echo off
setlocal
set "velaMode=%~1"
if "%velaMode%"=="" set "velaMode=Verify"
if /i "%velaMode%"=="Verify" goto mode_ok
if /i "%velaMode%"=="Refresh" goto mode_ok
if /i "%velaMode%"=="List" goto mode_ok
echo Usage: tools\source-manifest.cmd [Verify^|Refresh^|List]
exit /b 2
:mode_ok
pushd "%~dp0.." || exit /b 1
powershell -NoProfile -Command "& ([scriptblock]::Create((Get-Content -LiteralPath 'tools/source-manifest.ps1' -Raw))) -Mode '%velaMode%' -Root (Get-Location).Path"
set "velaResult=%ERRORLEVEL%"
popd
exit /b %velaResult%

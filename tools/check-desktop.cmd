@echo off
setlocal
pushd "%~dp0.." || exit /b 1
powershell -NoProfile -Command "& ([scriptblock]::Create((Get-Content -LiteralPath 'tools/check-desktop.ps1' -Raw))) -Root (Get-Location).Path"
set "velaResult=%ERRORLEVEL%"
popd
exit /b %velaResult%

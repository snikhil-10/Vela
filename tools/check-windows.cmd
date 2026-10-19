@echo off
setlocal
pushd "%~dp0.." || exit /b 1
set "velaTestArch=%~1"
if "%velaTestArch%"=="" set "velaTestArch=arm64"
if /i not "%velaTestArch%"=="arm64" if /i not "%velaTestArch%"=="x64" exit /b 2
dotnet publish tests/Vela.Windows.Tests -c Release -r win-%velaTestArch% --self-contained true -o artifacts/WindowsChecks-%velaTestArch%
if errorlevel 1 goto failed
robocopy artifacts\Vela-%velaTestArch%\Ocr artifacts\WindowsChecks-%velaTestArch%\Ocr /E /R:1 /W:1 /NFL /NDL /NJH /NJS
if errorlevel 8 goto failed
artifacts\WindowsChecks-%velaTestArch%\Vela.Windows.Tests.exe
set "velaResult=%ERRORLEVEL%"
popd
exit /b %velaResult%
:failed
popd
exit /b 1

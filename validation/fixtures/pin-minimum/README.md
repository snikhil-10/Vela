# Native image-pin minimum-size fixture

This standalone WinUI fixture creates the production `ImagePinWindow` with a generated bitmap and an isolated profile. It requests three collapsing window sizes, then checks the native minimum, image layout bounds and topmost/resizable state. It starts no resident Vela services or tray icon and does not use the clipboard or normal profile. It is separate from the 22 backend checks and the 130 inputs in the product/backend build manifest.

From the repository root, build on Windows:

```powershell
dotnet publish validation/fixtures/pin-minimum/PinSmoke.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/PinSmoke-x64
```

Before running, copy the exact candidate's `Vela.Windows.dll`, `Vela.Core.dll` and `Vela.Windows.pri` into that output. Also copy `Vela.Windows.pri` as `PinSmoke.pri`, which the fixture executable needs for the application's XAML resources. Keep the candidate's `Assets/Vela.ico` and the published runtime files. Then run `artifacts/PinSmoke-x64/PinSmoke.exe`. It exits after writing `pin-smoke-result.log`; failures set a nonzero exit code. No installation is needed. This fixture is intended for x64 Windows; ARM64 execution has not been tested here.

The October 19 run used the source-identical copy of these fixture files under `artifacts/pin-shelf-followup-2026-10-19/pin-smoke`, with the exact final candidate DLL/PRI copied after publishing. The checkpoint records their hashes. It passed at 150% scaling. This is a native component/layout test; manual border dragging, mixed-monitor DPI and six-pin lifecycle tests remain separate.

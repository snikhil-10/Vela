# Native session-tool component checks

This Windows x64 fixture runs production shelf and image-pin components under WinUI with its own generated profile. It starts no resident services, hooks or tray icon; it does not read or write the global clipboard or normal profile. A hidden dispatcher window keeps XAML alive between closing and reopening component windows.

It checks shelf filtering through native control properties: Unicode, empty/clear states, live collection changes, stable object identity/order, preservation of visible selections, dropping hidden selections and unsubscribing when closed. It separately creates six native image-pin windows, rejects a seventh, closes/reopens a slot, checks image-source release and idempotent CloseAll, rejects disabled/canceled admission, and injects a window-tracking failure to verify cleanup.

From the repository root:

```powershell
dotnet publish validation/fixtures/session-tools/SessionTools.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/SessionTools-x64
Copy-Item artifacts/SessionTools-x64/Vela.Windows.pri artifacts/SessionTools-x64/SessionTools.pri -Force
```

Keep the published runtime and `Assets/Vela.ico`. To test an exact built candidate, replace the fixture output's `Vela.Windows.dll`, `Vela.Core.dll` and `Vela.Windows.pri` with that candidate's copies, then copy its PRI as `SessionTools.pri` again. Run `artifacts/SessionTools-x64/SessionTools.exe`; it exits with a nonzero code on failure and records `session-tools-result.log`. The profile under this fixture output is disposable and overwritten on each run. Each pin request uses a fresh generated bitmap, matching the real decoder workflow.

These are native component checks using properties and real window lifetime; they are separate from the ordinary Windows backend checks and their build manifest. They do not establish physical mouse/keyboard drag, native picker completion, screen pixel appearance, Narrator, real Settings-toggle teardown or full resident app shutdown. No installation or publication is needed.

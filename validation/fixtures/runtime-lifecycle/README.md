# Native runtime component checks

This fixture loads a built candidate's exact DLLs under WinUI and uses a generated profile beside its own executable. It does not install global input hooks, inject keyboard/mouse input, publish clipboard data, change sign-in registration or use the normal Vela profile. It creates temporary owned windows and one temporary tray icon.

Keep awake checks use the production service with injected clock/power/process/display sources and a captured request adapter. They cover monotonic expiry, unavailable probes, request refusal/recovery, independent rules, display flags, repeated Start, Stop/restart and stale callbacks. A real main-panel Stop timer control/handler is checked against an injected battery-paused request, including admission after Exit begins. A separate short real system-only request runs on the fixture UI thread: the default five-second dispatcher callback releases an expired 100 ms timer, Stop releases a second request and the original thread flags are restored. It does not change a power plan, set the system clock, unplug AC, suspend Windows or test lid behavior. Native release has dispatcher/scheduling granularity.

Resource/thermal service checks capture notifications, verify one timer and teardown, reject queued callbacks after Stop and drain a read-only thermal query without late notification. The Dell has no supported NVIDIA sensors; these are lifecycle checks rather than GPU threshold/hot-plug evidence. Real toast delivery and quiet-time behavior remain separate.

It manually steps the production wheel callback and receives real WM_MOUSEWHEEL messages in two owned windows. It checks destination changes, large signed-delta bursts, option clearing and suspend/resume. Suspension must clear key/mouse pairing, debounce, close-confirmation and focus state; a canceled Caps Lock tap cannot become Escape. The fixture seeds no active Super chord and injects no input. This establishes native message packing and component lifecycle, not physical wheel/touchpad compatibility or timer cadence.

With all 31 tools selected before onboarding, it checks the real runtime, input/clipboard/shortcut services, wake state, shelf receiver, sensors and dim overlays remain inactive through Save/reapply and attempted tool admission. It verifies persisted selections and validation startup protection, then completes onboarding with an empty selection. It never enables the physical input features.

Settings checks hold the actual profile file against replacement and require a failed import to preserve live settings, disk bytes and the reviewed backup object. A successful import persists an independent copy while preserving local onboarding. The real Settings window transfer tracker reads a generated backup, opens a pending native ContentDialog, blocks a duplicate operation and cancels/dismisses on window close/controller Exit. A deliberate 100 ms cancellation cleanup delay verifies shutdown awaits work without logging normal cancellation as a failure; a separate deliberately faulted owned operation must still be logged. No canceled or late import is admitted. This does not operate the system picker or press the native confirmation buttons.

It also initializes the production controller with shelf, image pins and image tools only. It loads a session shelf entry and native pin, disables/re-enables their features, checks preserved identity and renewed cancellation scope, then exits during the actual OCR preparation pipeline with a deliberately blocked injected recognizer. Shutdown must cancel and await that operation, remove prepared temporary data, preserve the source and close all native windows and pins. The ordinary backend suite separately tests real OCR worker cancellation. This fixture does not claim a physical picker/button flow, actual session lock/suspend, Explorer drag or full hardware/input coverage.

After a successful x64 application build, run:

```powershell
.\tools\check-desktop.cmd
```

The helper checks the source snapshot, publishes the fixtures, verifies exact candidate DLL hashes, and copies resources and the worker. Exit Vela normally before running it. The executable returns nonzero on failure and writes `runtime-lifecycle.log` under `artifacts/DesktopChecks-x64/RuntimeLifecycle`. Its generated `profile` may be overwritten on a subsequent run. Do not replace DLLs with another build after the test begins.

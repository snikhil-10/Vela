# Windows UI and JPEG continuation — October 19, 2026

This is the earlier October 19 checkpoint. [Command search continuation](windows-command-search-checkpoint.md) records the newer `4af60249` source and focused results; the evidence below retains its original source identity.

Vela remains an unreleased 0.2.2 preview. The user authorized resumed Computer Use, asked to preserve normal settings and the accepted taskbar cue, and prohibited publication. The latest instruction says the user is away for a long time and cannot perform manual steps. Work continues without further prompts. The October 18 checkpoints remain historical evidence.

## Source and automated checks

Snapshot `9deb6e55-9cbc-42ee-ab39-1eea7c217fe0` has 115 verified app/test inputs. The x64 native XAML/OCR build passes **141 core checks**. **17 Windows backend checks** pass on this physical x64 Dell. ARM64 native XAML/OCR cross-publishing also passes from the x64 host; no ARM64 runtime result is claimed.

The new JPEG file-size limit tries bounded quality settings at or below the selected quality, then checks the actual output byte count before keeping the copy. Limits apply to each JPEG, from 1 to 102400 KiB in the UI; zero retains ordinary quality conversion. Dimensions do not shrink automatically. An impossible limit reports an error and leaves no incomplete copy. The existing batch behavior still stops at the first failed file; earlier successful copies remain. PNG/BMP limits and target-size video compression are not implemented.

Native testing exposed an existing quality defect: the former metadata-setting implementation produced **176,869 bytes at both quality 100 and quality 10** for a noise fixture. Quality now goes into the encoder creation options, as described in [Microsoft's bitmap imaging guidance](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/imaging). The same native probe produces **389,399 bytes at quality 100 and 22,378 bytes at quality 10** after the correction. The regression requires those settings to change compression, rather than merely checking JPEG decodability.

Six new core checks cover fitting preferred quality, bounded search, impossible limits, minimum quality, cancellation and invalid/empty measurements. The native check additionally verifies decodable capped output, unchanged dimensions, unique names, impossible/unsupported limits, cancellation and unchanged original bytes. Final byte enforcement remains necessary because encoder output size is not assumed to obey a universal monotonicity/determinism contract.

## Rendered observations

The first UI group used snapshot `7a0378fb`, before the JPEG changes. Its radial/onboarding/settings/cleaning source is unchanged in the current snapshot except for the separate batch-image controls in ToolWindow.

| Check | Actual result |
| --- | --- |
| Onboarding welcome and feature selection | Both render on the physical laptop. Selecting only File shelf persists. |
| Midway close/resume | Closing step two and activating the resident instance reopens step two with File shelf still selected. |
| Access and completion pages | Both render. Access shows only the selected shelf behavior; sign-in startup is disabled for the isolated profile. Completing onboarding shows the saved shelf-only panel. |
| General settings and appearance | General fits at the laptop's current 150% setting. System → Dark → System settles into the corresponding native appearances. No OS theme was changed. |
| Normal Exit | General → Exit ends the process. |
| Radial pointer selection | Eight-sector wheel renders; centre drags into North and East enter exactly those submenus. The centre Back click returns to the parent. |
| Radial cancellation/navigation | Release outside the wheel retains the root. Backspace returns; number keys reach the third level. A missing executable stays inline and switches to a readable list fallback. Activating the main panel dismisses the radial window. |
| Cleaning — first UI group | Keyboard-only countdown renders and the mouse Stop button restores the available state. An exact marker is accepted after closing the active window. This first case did not instrument elapsed time. |
| Cleaning — current source | Mouse-lock countdown continues and an attempted Stop click is blocked, with the cursor remaining at Start. Still paused at 57.702 seconds; first available-state observation at 64.534 seconds. A subsequent mouse click turns the isolated option off. Recovery is bracketed, not measured at exactly 60.000 seconds. Scroll blocking and held Escape were not tested. |
| Cleaning — timed close | Keyboard-only session starts at 15:35:28.953 UTC; native Close action at +8.343 seconds. After explicitly refocusing Find a window, the exact marker `vela-timed-recovery-20261019` is in UIA at +48.484 seconds and the settled rendered field at +52.903 seconds, before the original deadline. This establishes recovery before timeout, not immediate release latency. |
| Cleaning — feature disable | Active keyboard-only session starts at 15:37:48.459 UTC; Settings disables the feature at +20.861 seconds. The cleaning window closes. The literal filter value `Cleaning mode-disabled-recovered` appears at +29.935 seconds, before the original deadline. Normal settings are unaffected. |
| New JPEG controls | Render with native labels. Quality and size fields are disabled for PNG and BMP, enabled for JPEG. The native size control commits 100 KiB. Scrolling reaches Convert/OCR/Cancel at 150%. An empty batch reports an inline selection error. File and destination pickers open, but picker-based conversion remains unverified because the picker is not targetable and the user is away. |
| Shelf transfer setup | Explorer renders two generated files and confirms both selected (47 bytes). The drag endpoint is rejected as outside the source window bounds before input. No transfer is claimed. |

Centre-return drag paths, capture loss during a held drag, gesture Escape, touch/pen and Narrator are not established by the tested straight-line drags. Scroll blocking, held-Escape recovery, remaining feature-disable/session/clipboard-in-flight flows, mixed DPI/high contrast and broader editor/IME/elevated behavior remain open.

## Computer Use limits and local fixtures

Computer Use displays the native file picker as a related image but does not list its PickerHost window as a target. A coordinate attempt is rejected as being over PickerHost rather than Vela. Both `list_apps` and `list_windows` omit that target. The user cannot perform the requested manual selection while away. The task-owned isolated process was stopped to dismiss the picker without an active conversion; no further manual prompts are pending for continued work.

The shelf attempt uses two generated text files, without changing the clipboard. Computer Use rejects a cross-window drag endpoint outside the source window's 763×569 bounds. This is a transport limit, not evidence of an application drag failure. No source file moved and no destination copy was created.

Some immediate post-input screenshots show the previous frame while the accessibility tree has already advanced. A fresh observation is used before subsequent coordinate input and before assigning a settled rendering result. The compact radial window's hidden native Close entry has no cached bounds; its dismissal was tested through normal foreground deactivation instead.

Fixtures, outputs and logs are under ignored `artifacts/ui-validation-2026-10-19/`. The profile is `artifacts/laptop-validation/profile`; normal `%LOCALAPPDATA%\Vela\settings.json` was never replaced. No user document is used as an input. Important logs are:

- `results.log` — bounded UI assertions and explicit limits.
- `jpeg-core.log`, `jpeg-fixed-build.log`, `jpeg-quality-fixed.log`, `jpeg-arm64-build.log` — passing automated checks/builds.
- `jpeg-windows-checks.log`, `jpeg-quality-regression.log` — retained failing tests that exposed the quality defect.
- `image-fixture.json` — generated BMP identity; `files/`, `image-output/`, `shelf-source/`, `shelf-target/` contain only task fixtures.
- `settings.before.json`, `startup.before.json` are private preservation fixtures and remain ignored.

Normal settings and all five accepted taskbar receiver files match their pre-test hashes at the latest preservation check. Sign-in startup remains unavailable in the validation profile. General → Exit closes the final isolated process; the generated-file Explorer window is closed without deleting its files. No input lock, conversion or Vela instance is left running. No package, installer run, remote, public upload or release has been made. The accepted cue and Windows-native exclusion decisions are unchanged.

## Handoff step accounting

The [October 18 laptop checkpoint](windows-laptop-checkpoint.md) records the completed transfer/source integrity checks, local Git baseline, dependency versions and ordered unchanged-source build/backend run (130 core / 12 Windows checks). The required source app was launched on this laptop; current automated and focused rendered results are above. The optional older binary was not needed. No installation or package is required to verify this source preview, and none was made.

The missing external transfer hash sidecar, uncollected newer VM archives/logs, target-desktop hardware, broader manual UI/input matrix and unfinished non-native residuals remain explicit limits. The handoff is accounted for, but the complete port and all verification checklist items are **not complete**. A current machine-readable record is [windows-ui-checkpoint-2026-10-19.json](../validation/windows-ui-checkpoint-2026-10-19.json).

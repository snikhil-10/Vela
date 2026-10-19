# Windows laptop checkpoint — October 18, 2026

Vela remains an unreleased 0.2.2 preview. This continues the October 18 transfer, not the older 0.2.1 binary. Not every feature or Windows completion-checklist item is complete.

## Transfer and setup checklist

| Step in START-HERE / windows-handoff | Actual result |
| --- | --- |
| Extract source onto the local Windows drive | Done. Active source is the existing `Vela` subfolder of the extracted Downloads transfer. No second development copy was created. |
| Use the signed-in native Windows Codex agent / PowerShell | Done. Commands ran from the source root on Windows x64. No WSL, Mac XAML compiler, authentication or security-setting change. |
| Read the five requested files and their scope references | Done: handoff, README, feature audit, verification, VM checkpoint, plus architecture, hardware support, portable readme and JSON audit. Historical VM actions were treated as context, not instructions to change this laptop. |
| Verify October 18 source before using the optional binary | Done: 137/137 source hashes and 108/108 original manifest inputs matched. Original snapshot is `3b791fb4-ff70-4ab1-a947-0a94388639dd`. The transfer ZIP also passed a read-only 7-Zip integrity test of all 147 entries. Its external SHA-256 sidecar is absent, so no sidecar match is claimed. The unused 0.2.1 archive matches its recorded transfer SHA-256. |
| Establish a local checkpoint before editing | Done: Git baseline `3475cc3`. No remote, clone, push or publication. Original checksum files and VM records remain historical. |
| Check architecture, OS and build dependencies | Done: physical x64 Dell; Windows 11 Pro build 26200; SDK 10.0.402 and VC++ x64 runtime 14.50.35719 already installed. Pinned NuGet restore and offline English OCR passed. No SDK installation or policy change was necessary. |
| Run `tools\build.cmd x64` on unchanged source | Passed after the user exited the existing preview. 130 core checks; native XAML and x64 OCR published. The first attempt failed on locked DLLs and is retained in the local log. |
| Run `tools\check-windows.cmd x64` next | Passed: 12 backend checks, zero failures. |
| Launch the newly built source and check UI baseline | Launch/accessibility observation only. The panel exposed the existing 28 enabled tools, but the flyout disappeared before interaction. No new rendered onboarding/tray/settings pass. Later Computer Use reported a physical Escape stop, so UI automation stopped. |
| Continue unfinished non-native source and tests | Done for radial centre-drag selection and shelf lifecycle/identity fixes. Current source passes 135 core and 16 backend checks. Rendered validation and other residual features remain open. |
| Preserve user data, native exclusions, Mica and accepted cue | Normal settings and startup entry unchanged; five receiver hashes match the baseline. Main windows retain Mica. No native duplicate or placeholder tool was added. |
| Package / install / publish | Not performed. The handoff's package command is conditional on successful validation. No release ZIP, installation, remote or public upload was requested or made. |

## Environment and evidence

The laptop is a Dell Pro 14 Plus PB14250 with Intel Core Ultra 7 265U, Intel Graphics driver 32.0.101.8860 and 31.5 GiB reported RAM. The display reports 1920×1200; the registry reports AppliedDPI 144 (150%). These are observations, not multi-monitor or DPI-transition tests. The eventual i7-11700K / EVGA RTX 3080 / ASUS / Noctua / LG desktop was not tested.

The current source snapshot is `7a0378fb-773b-475e-b818-9a018478604d`, with 112 app/test build inputs. `build-source-manifest.json` gates the build; the same record is retained at `validation/windows-laptop-source-manifest.json` and copied into the publish folder. Original source checksums intentionally describe the transfer, not these subsequent edits.

Local logs are under ignored `artifacts/laptop-validation/`:

- `environment.json`, `baseline-result.json`
- `baseline-build.log` (locked-output failure), `baseline-build-after-exit.log`, `baseline-windows-checks.log`
- `manifest-build.log`, `manifest-build-retry.log` (nested-PowerShell hash lookup failures)
- `manifest-build-fixed.log`, `manifest-windows-checks.log` (final source build/check results)
- `arm64-cross-build.log` (ARM64 native XAML cross-publish from this x64 host), `transfer-archive-check.log` (original transfer archive only)
- `settings.before.json`, `startup.before.json` are private local preservation fixtures; they must not be added to Git or a package.

The executable is `artifacts/Vela-x64/Vela.Windows.exe`. Keep the whole folder together. It is a local build, not a validated new release. `tools\build.cmd arm64` also cross-published the ARM64 app and its x64 OCR worker successfully from this x64 host, with no build warnings/errors. That command's 135 core checks ran on the x64 host. No ARM64 executable/backend check or emulated ARM64 runtime test was attempted; this laptop does not supply that runtime environment.

## Source changes and tests

Radial menus now support pressing the centre, dragging toward a highlighted sector and releasing to open it or enter its submenu. Returning to the centre, leaving the valid area, losing capture, navigating with a key, resizing into list mode or closing cancels the gesture. Centre clicks and native keyboard/automation activation remain available. Five core checks cover one through eight sectors, boundaries/wrap, final release position, cancellation, invalid coordinates and fresh-session state. Pointer handling follows [Microsoft's documented capture/cancellation model](https://learn.microsoft.com/windows/uwp/design/input/handle-pointer-input). No rendered gesture, touch/pen, Narrator or high-contrast pass is claimed.

Shelf entries now have session identity rather than value equality. A completed drag of an entry that was removed cannot accidentally remove a newly added identical entry. Removal requires the offered Copy result. Delayed incoming providers are cancellable and are rejected before collection mutation after disable/session interruption/shutdown; tool-close also cancels its clipboard/drop import. The two new Windows checks exercise a held provider, cancellation, a fresh addition, pre-cancellation, late identical entries and unoffered operations. Desktop drag endpoints and lifecycle transitions remain separate open checks. Shelf Copy selected now checks Windows' clipboard write result and flushes the package before reporting success.

The isolated `--data-directory` option keeps settings and startup-error diagnostics in an explicit local fixture folder and blocks sign-in registration changes. It rejects missing, relative, UNC, duplicate and normal-user-data paths. `--settings` opens General after completed onboarding. Exit the current instance first; validation still uses the app's one-instance guard. The new backend check verifies path rejection, isolated diagnostics and the startup write guard. A fresh onboarding profile was launched but not inspected before the Computer Use stop. No normal user settings were replaced to test onboarding.

The build helpers now refuse a running executable in their output folder before changing files. The `.cmd` manifest checker explicitly imports Windows PowerShell's utility module because lookup through the command wrapper failed on this host. Manifest-gated native rebuilding succeeded after the fix. Packaging has the analogous module imports; the package command itself remains untested in this run.

The remaining new backend check enumerated one physical Core Audio capture endpoint and read its mute state. It did not record, mute/unmute or hot-plug a device. Existing read-only discovery returned zero NVIDIA sensors and zero DDC displays. No hardware write or brand-level support claim follows from this.

## Open work and stopping state

UI automation stopped when Computer Use reported a physical Escape event. No further UI automation was issued. The task-owned isolated process was terminated with no enabled tools, cleaning lock, pending transfer or shelf data. Vela is left stopped; the normal settings file and sign-in value match their pre-test state. The accepted five-file taskbar cue remains byte-identical. No VM helper, power/lid setting, hardware control, Windows theme, security setting or clipboard-publication fixture was run on the laptop.

The standard backend suite works with isolated data packages. The separate native publication fixture replaces the real clipboard, so it was not run here; its earlier VM pass is not a laptop pass. User clipboard data was not seeded or replaced for testing.

Remaining rendered checks include onboarding/resume and pre-completion listener instrumentation, tray/keyboard/Explorer restart/startup, settings/theme/contrast/accessibility, all feature-disable paths, outgoing shelf multi-file drag/cancel/remove-after-drop, all taskbar/tray targets, missing originals in the UI, snippet exact output and clipboard races, broader editors/IME/elevated apps, mouse/scroll/Super-key compatibility, cleaning recovery, image-pin limits, mixed DPI/multiple monitors, wake/power rules and real audio/DDC/GPU writes. Existing core/backend coverage does not close these desktop checks.

Unimplemented non-native residuals remain in the audit: additional hardware sensors and fan recovery/watchdogs, audio priority/disconnect rules and amplification, Bluetooth sleep recovery, advanced capture/recording/scrolling/expiring links, reviewed app leftovers, developer-feed aggregation, disk/script/menu indexing, localization and searchable/file clipboard history. The new centre-drag gesture does not establish every possible radial gesture or finish the port.

The original VM's latest archives/logs are not available in this source transfer. Their collection and archive CRC/PE/manifest validation remain open. No laptop test is assigned to those archives. The user requested no questions or prompts until they explicitly allow them again.

## Refreshing the local source record after future edits

The build deliberately rejects changed inputs while `build-source-manifest.json` describes a previous snapshot. Preserve the current validation record, then generate a new manifest before building changed source. This local PowerShell example includes source/test resources as well as code; its input count can exceed the 112 compile inputs in this checkpoint:

```powershell
$velaRoot = (Get-Location).ProviderPath
$velaPrevious = Get-Content build-source-manifest.json -Raw | ConvertFrom-Json
$velaInputs = @('Directory.Build.props', 'Vela.sln', 'global.json') + @(
    Get-ChildItem src, tests -Recurse -File |
    Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]' |
    ForEach-Object { $_.FullName.Substring($velaRoot.Length + 1).Replace('\', '/') }
)
$velaHashes = [ordered]@{}
foreach ($velaInput in $velaInputs | Sort-Object -Unique) {
    $velaHashes[$velaInput] = (Get-FileHash -LiteralPath $velaInput -Algorithm SHA256).Hash.ToLowerInvariant()
}
$velaNext = [ordered]@{
    snapshotId = [guid]::NewGuid().ToString()
    parentSnapshot = $velaPrevious.snapshotId
    files = $velaHashes
}
$velaNext | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 build-source-manifest.json
.\tools\build.cmd x64
.\tools\check-windows.cmd x64
```

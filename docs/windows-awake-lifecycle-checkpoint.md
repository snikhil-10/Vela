# Keep awake and alert lifecycle checkpoint — October 22, 2026

Source `399f2871-d115-4db0-b516-f92e785f02d6` follows `8bd1d1aa-acaf-4bac-9b99-6b28d4ba35e1` and records **193** source, test, fixture, tool and workflow inputs. This remains the unreleased 0.2.2 preview with 31 implemented tools. It is not a finished port or a claim of 100% functionality. See [the machine-readable evidence](../validation/windows-awake-lifecycle-checkpoint.json), [GitHub preparation](github-readiness.md) and [remaining verification](verification.md).

Keep awake now measures timer duration with a monotonic clock. Calendar changes cannot extend or shorten its logical deadline. Battery and unavailable AC status pause an AC-only request while the original deadline continues to elapse. Disabling or stopping the service clears that timer; stale actions and queued callbacks cannot restart it. Invalid durations do not replace a valid timer. The supported duration remains greater than zero through 24 hours, matching the existing UI limit.

The panel's **Stop timer** control remains usable when a timer is paused on battery. Stopping the timer preserves separately configured application and external-display rules. These rules release their request when no longer matched. Unavailable process/display/power probes report the problem and use a safe fallback; a declined native request reports “Request unavailable” and can recover on retry. No settings schema, catalog entry or native exclusion was added.

Resource alerts and thermal alerts now own only one timer per active service. Resource alerts ignore callbacks arriving after Stop. Thermal teardown retains its generation/in-flight protection and drains read-only sampling without a late notification. Sustained thresholds and cooldown policy are unchanged.

## Actual results

| Check on this Dell x64 laptop | Result | Scope |
| --- | --- | --- |
| Current source inventory | 193 hashes match | Discovered build/test/tool/fixture inputs |
| Windows core suite | **236 passed, 0 failed** | Includes 14 new Keep awake cases; repeated build runs are not extra coverage |
| x64 Windows backend | **28 passed, 0 failed** | Existing native media, OCR, cancellation and Shell transfer regressions |
| SessionTools desktop components | Passed | Native shelf/search/pin behavior against the candidate DLL |
| RuntimeLifecycle desktop components | Passed | Awake/alert tests plus previous onboarding/settings/wheel/controller/OCR regressions |
| x64 application and OCR publishing | Passed | Complete local output with verified provenance |
| ARM64 application cross-publishing | Passed | Core runs on x64; OCR worker remains x64; ARM64 execution not established |
| Separate clean source export | All five commands passed | Manifest, x64 build, backend, desktop components, ARM64 build on this configured laptop |

The command sequence is recorded in the JSON checkpoint. The clean export uses a separate temporary Git index and Git line-ending attributes, without changing the owner's index. It does not copy `.git`, `bin`, `obj` or generated `artifacts`. It shares this laptop's installed SDK/runtime and NuGet cache. Hosted CI and a fresh-machine installation remain separate gates. No new binary ZIP, installer, commit, remote, push or publication was made.

## Failures reproduced before correction

- The preceding source's DLL accepted a one-minute Hold after Stop: **“A stopped Keep awake service accepted a stale hold action.”** Cleanup released the generated request.
- The preceding resource-alert service sampled from a callback invoked after Stop: **“A queued resource callback sampled or notified after Stop.”** The CPU baseline changed; fixture notifications were captured rather than displayed.
- A preliminary candidate passed the initial lifecycle cases, but an injected unavailable process probe escaped Evaluate. The added negative cases now pass for unavailable process/display/power probes and declined/retried native requests. This preliminary failure is retained separately from the original-source failures.

The wall-clock weakness was identified in source and covered by new deterministic clock tests. The operating-system clock was never changed. Original and preliminary DLL identities and local failure-log hashes are recorded in the JSON evidence; neither is represented as the final validation result.

## Native component evidence

The fixture loads the exact candidate application DLL, constructs the real AwakeService, and supplies explicit clock/power/process/display/request adapters for deterministic transitions. It covers exact logical expiry, calendar jumps, battery pause/expiry, independent app/display rules, filename/case matching, flag changes, lost rules, unavailable probes, request failure/retry, repeated Start, Stop/restart and stale actions/callbacks. It constructs the real panel and invokes its handler with a battery-paused timer; this is component evidence, not a physical button click.

A separate test uses the real default five-second DispatcherQueueTimer and Windows execution-state API on the fixture's own UI thread. A 100 ms request releases after the dispatcher observes expiry; another request releases on Stop. The fixture checks the prior returned system/display flags and restores its original flags in `finally`. This briefly requests system wakefulness on the fixture thread. It does not alter power plans, Sleep/lid settings or the system clock. The API's documented return value and thread scope are described by [Microsoft's SetThreadExecutionState reference](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate).

**Logical expiry is precise in the model; native release follows the five-second polling interval and dispatcher scheduling.** A busy UI thread may delay evaluation. This is not a hard real-time timer or evidence of manual Sleep, lid-close or critical-battery behavior.

Resource/thermal checks exercise production services, verify one timer, then invoke stopped callbacks. An in-flight read-only thermal poll drains without late notification. This Dell reports no supported NVIDIA sensor; real GPU temperatures, hot-plug, physical threshold delivery and Windows quiet-time behavior are not claimed. The fixture captures notifications, installs no global input hooks, injects no keyboard/mouse input and publishes no global clipboard content.

Previous native components still pass: all 31 selected tools remain inactive before onboarding completion; failed/successful settings imports preserve the required state; pending confirmation cancels on close/Exit; normal cancellation drains while genuine faults remain recorded; 340 wheel messages reach owned windows; loaded shelf/pins tear down and re-enable; active OCR preparation cancels and cleans up. The deliberately blocked OCR recognizer is injected; actual worker cancellation is covered separately by the backend suite. System picker and confirmation-button operation remain open.

## Preservation and remaining limits

Normal `%LOCALAPPDATA%\Vela\settings.json` remains byte-identical with SHA-256 `b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`. The normal profile was not launched. The real Git index is unchanged. Settings schema/model and the feature catalog are unchanged from the parent. The six protected taskbar-cue/Mica sources still match October 18; shelf transfer sources match the preceding validated source. All 77 feature scope decisions are unchanged. Fixture processes exited normally; no Vela resident or OCR worker remains.

Physical keyboard/mouse/editor/IME/elevated-target compatibility, full system-picker/save/overwrite flows, broader Explorer collision/skip/folder/provider/long-copy scenarios, actual lock/suspend, accessibility/mixed DPI and multi-monitor behavior remain open. Ordinary physical shelf success is historical evidence from source `68426e4c`; no fresh physical drag is claimed here. Actual AC transitions, external-display hot-plug, manual Sleep/lid/critical battery, unavailable DDC/NVIDIA hardware and multi-device microphone cases still need their stated tests. ARM64 execution and hosted CI remain unverified.

Deferred non-native residuals remain in the audit, including audio/Bluetooth automation, amplification, additional sensors/fans, advanced capture/editing, reviewed app leftovers, broader indexing/history and localization. This work improves implemented functionality and tests without adding placeholders or changing Windows-native exclusions. Detailed logs live locally under `artifacts/awake-lifecycle-2026-10-22`; the checkpoint summaries and hashes travel with the source.

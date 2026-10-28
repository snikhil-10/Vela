# Locked-desktop continuation — October 22, 2026

**Later update:** The owner subsequently unlocked Windows. [Human-assisted settings transfer tests](windows-settings-transfer-ui-checkpoint.md) passed and the isolated resident exited normally. Open-resident/locked-desktop statements below retain the state observed earlier.

The source remains `399f2871-d115-4db0-b516-f92e785f02d6`, with 193 verified inputs. No product or integrated test source changed. The preceding [awake/alert checkpoint](windows-awake-lifecycle-checkpoint.md) retains its 236 core / 28 Windows checks, both builds and both native component suites. The additional filesystem checks below are separate ad hoc scenarios; they do not increase those suite counts or establish picker/physical-input compatibility. See [the supplemental JSON evidence](../validation/windows-headless-stress-checkpoint.json).

## Interactive test availability

An isolated validation profile was opened with **zero enabled features**, separate from normal settings. Computer Use could read its Settings accessibility tree, but the first click failed with `coordinate input geometry is unavailable`; fresh activation failed with `failed to activate captured window`. Vela's single-instance activation exposed its panel, whose accessibility tree also reported zero tools. Its screenshot showed Windows lock-screen content rather than usable application pixels. Interactive input stopped.

The owner subsequently explicitly authorized clicking the password-free Sign in button. A fresh read-only window/app inventory exposed background application windows but no targetable lock/sign-in window. A fresh Vela capture still showed the lock screen. No Sign in click, credential entry, authentication attempt or settings change was performed. This is a tool-access limitation, not evidence that a password was required. No picker, save/overwrite dialog or physical-input pass is claimed.

The isolated Vela process remains open with zero tools enabled. It was not force-terminated, and normal Exit could not be operated through the locked desktop. Its presence must be accounted for before another normal-output build or native desktop fixture run. The earlier checkpoint's zero-process statement describes its own completed test run, before this isolated UI attempt.

## Backup filesystem stress

The harness references the compiled `Vela.Core.dll` from the validated x64 output. It uses only generated files under local ignored artifacts. It exercises concurrent exports, readable snapshot integrity and cancellation/error cleanup. It does not use the clipboard or normal profile.

| Scenario | Observed result |
| --- | --- |
| Six rounds of 32 concurrent exports, with a delete-sharing reader | 192 attempted; **51 completed, 141 reported access refusals**. Reader observed 17 complete known snapshots, zero partial/mixed/missing snapshots and zero read refusals. No staging leaks. |
| 32 staged writes canceled before commit | All canceled; preceding backup bytes/hash unchanged; no staging leaks. |
| 32 staged writes with generated writer faults | All faults surfaced; preceding backup bytes/hash unchanged; no staging leaks. |
| 32 exports while destination denies delete sharing | All refused; preceding backup unchanged; export and import recovered after the read lock was released. |

**Four bounded integrity scenarios passed; zero failed under the stated contract.** Concurrent success is not guaranteed. The first harness assumed every competing export should succeed and stopped on an `UnauthorizedAccessException` from replacement. That assumption was too strong for competing Windows filesystem operations. The revised harness records access refusals separately and checks integrity, cleanup and recovery; the original refusal log is retained. No production behavior was changed to obtain a pass. An earlier harness build also used an incorrect DLL reference path; its compiler log is retained separately and is not a product build failure.

These are deterministic generated workloads on this configured Dell, not a proof of arbitrary concurrency, cross-process preference merging, interrupted-power durability or network-filesystem behavior. They do not exercise system pickers, confirmation buttons, physical devices, actual service lock/suspend transitions or absent hardware. Detailed logs, harness sources and generated backups are under `artifacts/picker-ui-2026-10-22`; summary hashes travel in the JSON checkpoint.

Normal settings remain byte-identical; the accepted cue/Mica sources, settings/catalog/shelf sources, 77 scope decisions and real Git index remain unchanged. No installer, binary ZIP, commit, remote, push or publication was made. Remaining physical/picker/hardware/accessibility/ARM64 and deferred-feature limits in [verification](verification.md) still apply.

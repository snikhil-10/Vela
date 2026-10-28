# Human-assisted settings transfer UI — October 22, 2026

Source remains `399f2871-d115-4db0-b516-f92e785f02d6`, with 193 verified inputs. No product or integrated test source changed. The owner unlocked Windows, allowing the isolated zero-tool resident from [the earlier locked-desktop attempt](windows-headless-stress-checkpoint.md) to be tested and exited normally. This checkpoint supplements [the awake/alert results](windows-awake-lifecycle-checkpoint.md) and [settings/lifecycle components](windows-settings-lifecycle-checkpoint.md). See [the JSON evidence](../validation/windows-settings-transfer-ui-checkpoint.json).

The last full automated sequence remains **236 core / 28 x64 Windows checks**, both architecture builds, both native desktop component suites and a separate clean source export. These UI cases are separate observations and do not increase those counts. No rebuild or suite repetition was needed for unchanged product/test inputs.

## Actual UI observations

| Case | Observed result | Interaction |
| --- | --- | --- |
| Unlocked Settings and Access & data | Passed; controls respond and validation sign-in startup is disabled | Computer Use |
| Export a generated zero-tool profile through Save As | Passed; Vela shows “Settings exported.” A 1,193-byte backup matches the complete normalized test profile and has no startup field | Computer Use opens export; owner enters the disposable path and clicks Save |
| Cancel the import confirmation | Passed; confirmation states zero tools/zero snippets. Cancel closes it and test preferences remain byte-identical | Owner selects the fixture through Open; Computer Use clicks Cancel |
| Accept the import confirmation | Passed; Vela shows “Settings imported.” Light appearance is persisted, zero tools remain enabled and local onboarding stays complete at step 3 despite the fixture specifying false/step 0 | Owner selects the same fixture again; Computer Use clicks Import |
| Display the imported appearance | Passed; General visibly shows Light and validation startup remains disabled | Computer Use |
| General → Exit | Passed; the resident exits normally, with no Vela windows, app process or OCR worker remaining | Computer Use; process/window inventories verify completion |

The generated export SHA-256 is `fa8555e1ad8c365d80a314364c69cd65a49e9d19c11869f7916b14a9b2c54149`. Its model is equivalent to the on-disk isolated profile before import. The generated fixture is read-only input to these tests and stays unchanged.

Cancel leaves the test profile SHA-256 at `9cda69a59cb3b231f99bfcab5b06da8a374a034f7272de97911a7a64ffac4e48`. Successful import changes that disposable profile to `fbd6250b87381013650ddfaf3c6ca45857c39cb2b26a296193357dc9318198ca` with the expected Light appearance and local onboarding state. This is an intentional test-profile change, separate from the normal preferences.

## Computer Use limit and owner assistance

Save As is rendered by `PickerHost.exe`. The tool can capture it as related UI but does not return a targetable picker window. Its filename-field click was rejected because the point was over PickerHost rather than Vela; activating the owner, taking a fresh capture and retrying produced the same rejection. No filename was typed by automation and no picker result was inferred from a rejected click.

The owner performed three specific picker interactions and reported completion: one Save and two Open selections. Computer Use operated Vela's export/import buttons, the native confirmation's Cancel and Import buttons, General navigation and Exit. These are **human-assisted end-to-end cases**, not unattended picker automation. The tool's picker limitation remains relevant for other dialogs.

Settings readback and process checks are independent of the UI observations. The export was decoded by the compiled core backup reader and compared as a complete normalized model; cancellation compares exact file bytes/hashes; successful import checks persisted values and preserved local onboarding. The immediate post-Exit window inventory briefly included closing entries; the subsequent settled inventory and process check report zero.

## Preservation and remaining limits

Normal `%LOCALAPPDATA%\Vela\settings.json` remains byte-identical at SHA-256 `b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`. The normal profile was never launched. The six protected cue/Mica sources, settings/catalog/shelf sources, 77 native-exclusion/scope decisions and real Git index are unchanged. No startup control was changed, no hardware write or automated clipboard publication was performed, and no process was force-terminated.

The isolated resident is now closed, so its earlier locked-output constraint is cleared. Earlier reports describing it as open retain their historical state; this report records its later normal Exit.

This does not close the full picker matrix: Save As cancellation/overwrite, import-picker cancellation, malformed/future/oversized files through the actual picker, busy destinations through UI, different file providers and other media/folder dialogs remain open. Existing core/backend/component coverage for malformed files, size limits, refusal and shutdown cancellation is separate. Physical input hooks/editor/IME/elevated compatibility, actual lock/suspend services, accessibility/mixed DPI, broader Explorer/provider transfers, absent hardware, ARM64 execution, hosted CI and deferred non-native features still need their stated checks.

Detailed observations and generated fixtures are under `artifacts/picker-ui-2026-10-22`; their hashes and summaries travel with source. No installation, binary package, commit, branch rename, remote, push or GitHub Release was made. This remains the unreleased 0.2.2 preview.

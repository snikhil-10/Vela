# Preparing the source for GitHub

**Repository setup — October 27, 2026:** The owner selected the public repository [snikhil-10/Vela](https://github.com/snikhil-10/Vela) and authorized the first development-preview source push. Repository-local author identity is configured with the owner's supplied name and noreply email, the upload branch is `main`, and `origin` points to that destination. The nine earlier local checkpoints are retained. Hosted status and logs are available under [GitHub Actions](https://github.com/snikhil-10/Vela/actions/workflows/windows.yml). Source upload and CI artifacts remain separate from a GitHub Release; no release was requested. Earlier preparation statements below retain their original dates and conditions.

Vela 0.2.2 is an unreleased development preview with 31 implemented tools. The source can be reviewed, built and tested locally before the owner chooses a GitHub repository. No repository, remote, push, release, binary package or installation is created by this workflow.

## Build and verification

Use Windows 11 x64 and the .NET 10 SDK. The current laptop uses SDK 10.0.402. Windows App SDK, Windows SDK build tools, OCR dependencies and the English model are pinned in source. OCR also needs the Microsoft Visual C++ x64 runtime. Keep the complete output folder together.

From the source root, run each command only after the preceding one succeeds:

```powershell
.\tools\source-manifest.cmd Verify
.\tools\build.cmd x64
.\tools\check-windows.cmd x64
.\tools\check-desktop.cmd
.\tools\build.cmd arm64
```

Desktop checks need an interactive Windows desktop and require Vela to exit normally first because the controller owns one tray identity. They use separate generated profiles, owned windows and generated media. They do not change the normal profile, clipboard, startup registration or hardware settings. The wake fixture briefly owns a system execution-state request on its own UI thread and restores the prior flags; it does not change power plans or the system clock. They return nonzero on failure. ARM64 is cross-built on this x64 laptop; this does not establish ARM64 runtime behavior.

The Windows backend suite covers native media/OCR, actual Windows folder transfers, source preservation and cancellation. The desktop component suites cover shelf/pin behavior, real wheel-message delivery, onboarding gating, transactional settings import, pending confirmation cancellation, monotonic Keep awake rules, transient fixture-thread execution flags, stopped alert callbacks, feature teardown and an active OCR preparation pipeline during controller shutdown. The injected blocked recognizer in that last case is explicit; the backend suite separately tests the actual owned OCR process. Component checks do not establish every physical mouse, picker, editor, accessibility or hardware scenario.

For portable core checks on Linux/macOS or Windows:

```sh
dotnet run --project tests/Vela.Core.Tests -c Release
```

Five file-sharing/read-only checks run only on Windows; test counts therefore depend on the host. The GitHub workflow verifies source, runs portable core checks, builds x64 and ARM64, and runs x64 Windows backend checks. Workflow dependencies are pinned to verified official action commits, credentials are not persisted, and token permissions are read-only. Uploaded build artifacts are labeled previews; the workflow creates no GitHub Release. An actual hosted workflow run is still required after the owner pushes the source.

## Source manifest

`build-source-manifest.json` records a source snapshot, parent and SHA-256 input inventory. The verifier discovers source, test, tool and fixture files instead of trusting a manually maintained list. It rejects changed, new and removed inputs. Generated bin/obj/artifacts directories are excluded. Build scripts copy the verified manifest to their successful output after compilation and OCR publishing complete.

`SOURCE-FILES.sha256` is the original October 18 transfer inventory, retained as historical evidence. It is not the current source manifest.

After intentionally editing source or validation inputs, review the changes and run:

```powershell
.\tools\source-manifest.cmd Refresh
.\tools\source-manifest.cmd Verify
```

Then rebuild and rerun affected checks. Refresh records a new identity; it is not a test pass. The checked-in attributes preserve stable source line endings, including CRLF batch files and unchanged historical evidence. The command wrappers use the same PowerShell implementation without changing the machine's execution policy.

## Review before the first push

The owner needs to choose a GitHub account/organization, repository name and visibility, then provide the destination URL and commit author name/email. A private repository is a reasonable starting point for this unfinished preview. Create the destination empty: do not initialize it with another README, license or gitignore. GitHub documents this in [adding locally hosted code](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github).

Use an email the owner wants attached to commits; GitHub provides a private-address alternative through its noreply address. Copy that exact address from the account's email settings rather than inventing it. Author name/email can be configured for this repository only; see [GitHub's commit email guidance](https://docs.github.com/en/account-and-profile/how-tos/email-preferences/setting-your-commit-email-address). Git for Windows already has the `manager` credential helper configured on this Dell. Command-line authentication has not been verified; the owner may need to complete browser authentication when the first authorized push is attempted. Passwords or access tokens should not be pasted into this chat.

As of October 22, source `399f2871` has 193 verified inputs; the full local sequence and separate clean source export pass. The current branch is `master` and nine local commits already exist. The first upload should preserve that history and include the reviewed continuation changes. Renaming the upload branch to `main` can be done during the authorized first-push preparation; it has not been performed. No remote or author configuration has been changed.

The existing workflow runs on pushes and pull requests, checks portable core behavior, builds both Windows architectures and runs x64 backend checks. It stores preview build artifacts for 14 days and does not create a GitHub Release. Its first hosted run remains unverified until the owner authorizes a push. No signing or publishing credentials are required for this source-preview workflow.

The owner later unlocked Windows and helped operate three file-picker steps. Basic settings export and native confirmation Cancel/Import passed, then the isolated zero-tool resident exited normally. No validation app or OCR process remains. See [the human-assisted UI checkpoint](windows-settings-transfer-ui-checkpoint.md). Broader picker/physical-input/hardware cases remain recorded preview limits; a first GitHub push does not establish them.

Keep source, licenses, the pinned OCR model/provenance, tests, fixtures and build tools together. Generated application outputs, fixture profiles and local artifacts are ignored. Existing dated reports and checkpoint JSON retain their original source identities; many detailed log paths refer to the local development workspace rather than files included in a build. Do not replace current source with the collected October 18 VM binaries.

Review the changes and new source files, then make a local commit using the owner's chosen author identity. Add a remote and push only when the owner chooses the destination. The current workflow has no publishing credentials and requires no GitHub access to validate locally. Preserve LICENSE and NOTICE.md and the independent Vela branding.

## Remaining functionality work

Passing the automated checks is a bounded preview milestone. A claim of 100% functionality is not supported while these remain:

- Physical wheel/button/Caps Lock/close-protection compatibility, precision touchpads, real snippet/plain-paste editor and IME/elevated-app cases.
- Full picker/save/cancel/overwrite flows, active user-driven media operations, actual lock/suspend and broader resident transitions. Controller cancellation and backend cleanup are covered separately.
- Current-source full Explorer collision cancellation, partial skip, arbitrary destinations, folder/provider and long-copy completion. Ordinary physical two-file copy/removal passed; actual Shell-handler cancel/skip tests are separate.
- Narrator, high contrast, mixed DPI/monitors, broader layouts and ARM64 execution.
- Actual DDC/NVIDIA and other unavailable hardware, multi-device microphone/hot-plug and device failures.
- Deferred non-native residuals in the 77-feature audit: audio priority/automation and safe amplification, Bluetooth rules, extra sensor/fan backends, advanced capture/editing, reviewed app leftovers, broader indexing/history and localization.

Windows-native exclusions, WinUI 3/Mica and the accepted taskbar cue remain requirements. The readiness checkpoint records actual results and limits; it does not label the preview a finished port.

# Vela for Windows

Native Windows 11 utilities for files, focus and everyday workflows, together in a WinUI 3 and Mica tray app.

**Vela 0.2.2 is an unreleased development preview.** The source implements 31 optional tools, with further features and validation still in progress. There is no finished-port or 100% functionality claim. Build the current source for the latest fixes; the optional historical 0.2.1 binary is older.

Vela is an independent, unofficial project inspired by [Vorssaint](https://github.com/vorssaint/vorssaint-utils). See [attribution and identity](NOTICE.md).

## Features

| Area | Implemented tools |
| --- | --- |
| Files and clipboard | Session file/text/link shelf with search and multi-file dragging; tracking-link cleanup; text snippets; clipboard auto-clear; plain-text paste with clipboard restoration; save copied images as PNG. |
| Launching and workspace | Quick panel, command search, launcher profiles and nested radial menus; open-window search, window pinning, saved window positions and undo. |
| Focus | Timed keep-awake with application, external-display and AC-only rules; cleaning mode with a deadline and recovery controls. |
| Input | Smooth notched-wheel scrolling, Alt+wheel horizontal scrolling, horizontal inversion, mouse-button shortcuts, duplicate-click filtering, Caps Lock Super key and selected-app close protection. |
| Images and media | Session image pins with a visible minimum size and topmost toggle; batch image conversion, resizing, watermarks and size/quality controls; offline English OCR; MP4 conversion, M4A extraction and animated GIF batches. |
| Display and system | Per-display software dimming, supported DDC/CI brightness, resource alerts, supported NVIDIA sensor readings/thermal alerts and all-microphone mute. |
| On-demand tools | Developer RSS/Atom feeds, internet speed measurements and review of older files in a selected messaging-download folder. |

Tools can be enabled or disabled individually. Input features support application exclusions. Hardware and codec capabilities are discovered at runtime; a brand name alone does not establish support. More detail is in the [feature audit](docs/windows-feature-audit.md) and [hardware guide](docs/hardware-support.md).

### Scope

Vela focuses on extras beyond Windows' built-in tools. Snap, Alt+Tab, basic clipboard history, ordinary screenshots/recording, volume routing, standard uninstall and other native capabilities stay with Windows. Partial overlaps retain only the extra functionality. PowerToys is treated as an optional third-party utility.

The [77-feature inventory](docs/feature-audit.json) records the decisions and remaining non-native work. Deferred items include audio/Bluetooth automation, amplification, additional sensor/fan backends, advanced capture/editing, broader indexing/history, reviewed application leftovers and localization. These are not exposed as placeholder tools.

## Build and run

Requirements:

- Windows 11 build 22621 or later and the **.NET 10 SDK**.
- The Microsoft Visual C++ **x64** runtime for the offline OCR worker.
- Network access for the initial restore of the pinned NuGet dependencies.

The project pins Windows App SDK 2.5.1, Windows SDK build tools and Tesseract 5.2.0. Application outputs are self-contained and unsigned. The OCR worker is x64 in both application builds.

Exit a running Vela instance normally before rebuilding its output. From the repository root:

```powershell
.\tools\source-manifest.cmd Verify
.\tools\build.cmd x64
.\artifacts\Vela-x64\Vela.Windows.exe
```

Choose your tools during first-run onboarding. Settings remain available from the tray. Keep the complete output directory together, including its assets, OCR worker, resources and licenses.

To cross-build the ARM64 application:

```powershell
.\tools\build.cmd arm64
```

Cross-building succeeds on the x64 development laptop; execution of this source on ARM64 hardware remains unverified. See the [build and GitHub guide](docs/github-readiness.md) and [portable quick start](docs/portable-readme.md).

## Verification

The last full recorded validation, **October 22, 2026**, covers:

| Check | Recorded result |
| --- | --- |
| Core regression suite on Windows x64 | 236 passed, 0 failed |
| Windows backend checks on x64 | 28 passed, 0 failed |
| Native desktop components | SessionTools and RuntimeLifecycle passed |
| Application publishing | x64 and ARM64 cross-build passed |
| Separate clean source export | Manifest, builds, backend and desktop checks passed on the configured Dell |

Additional [backup-integrity stress checks](docs/windows-headless-stress-checkpoint.md) and [human-assisted settings transfer UI checks](docs/windows-settings-transfer-ui-checkpoint.md) passed. Picker steps performed by the owner are identified explicitly. Detailed source identities, results and limits are in the [awake/alert checkpoint](docs/windows-awake-lifecycle-checkpoint.md) and [verification checklist](docs/verification.md). Check [GitHub Actions](https://github.com/snikhil-10/Vela/actions/workflows/windows.yml) for the status and logs of hosted runs; the table above records the local validation.

After building x64, exit Vela and run the Windows checks sequentially:

```powershell
.\tools\check-windows.cmd x64
.\tools\check-desktop.cmd
```

Desktop checks need an interactive Windows session. They use generated profiles, owned windows and test media. The wake fixture briefly owns a system execution-state request on its own thread and restores its prior flags; it does not change power plans or the system clock.

Portable core checks can run on Windows, Linux or macOS:

```sh
dotnet run --project tests/Vela.Core.Tests -c Release
```

Five file-sharing/read-only cases run only on Windows, so counts differ by host. Native WinUI/XAML compilation and Windows runtime checks require Windows. A passing build does not establish physical-device compatibility.

Remaining validation includes broader keyboard/mouse/editor/IME/elevated-app compatibility, picker cancel/overwrite/error paths, Explorer/provider transfers, actual lock/suspend and power transitions, accessibility/mixed DPI, hardware/hot-plug and ARM64 execution. The preview is not ready to be described as fully verified.

## Local data and network use

- Normal preferences, snippets, launcher paths and saved workspace titles/positions are stored in `%LOCALAPPDATA%\Vela\settings.json`.
- The shelf and image pins are session-only. Shelf entries reference explicitly added originals; ordinary copying keeps those originals.
- Settings export/import uses JSON backups up to 4 MB. Backups can contain snippet text, file paths and feed URLs. Sign-in startup is separate from the backup.
- Vela has no account, analytics or cloud sync. It does not index the disk, collect clipboard history or record microphone audio.
- Network tools run on demand: developer feeds contact the saved URLs on Refresh; the speed test contacts Cloudflare and uses up to 80 MB plus overhead, including generated upload traffic. Those servers receive the public IP and requests; Vela sends no user files or results report.

Start-at-sign-in is opt-in. Closing the panel hides it; **Settings → General → Exit** stops the resident app and releases its hooks, wake requests, pins and dimming overlays. A microphone mute setting remains as chosen.

For isolated development validation, launch with `--data-directory "C:\absolute\local\fixture-folder"`. This separates settings and diagnostics and prevents sign-in registration changes. Vela permits one active instance, so exit before switching profiles. `--settings` opens General after completed onboarding.

## Development

Build scripts verify `build-source-manifest.json` before compilation. After intentional source/test/tool/fixture changes, review the changes, refresh the manifest, then rebuild and rerun affected checks:

```powershell
.\tools\source-manifest.cmd Refresh
.\tools\source-manifest.cmd Verify
```

Refreshing records an identity; it does not constitute a test pass. Generated outputs, local fixtures, editor state, environment secrets and signing containers are covered by the repository's [.gitignore](.gitignore).

For a first GitHub upload, create an empty destination repository and use the README, ignore file and license already present here. See [first-push preparation](docs/github-readiness.md). The workflow runs checks and stores preview build artifacts; it does not create a GitHub Release.

## Documentation

- [Verification and remaining work](docs/verification.md)
- [Feature scope and Windows-native exclusions](docs/windows-feature-audit.md)
- [Build and GitHub preparation](docs/github-readiness.md)
- [Hardware support](docs/hardware-support.md)
- [Architecture](docs/architecture.md)
- [Windows development handoff](docs/windows-handoff.md)

## License and attribution

Vela is distributed under **GPL-3.0-or-later**. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md) for licensing, attribution and the project's independent identity.

OCR dependencies retain their Apache-2.0, BSD and MIT notices in [src/Vela.OcrWorker/Licenses](src/Vela.OcrWorker/Licenses). Other dependencies retain their respective license terms. Keep these notices with source and build distributions.

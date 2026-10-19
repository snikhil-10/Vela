# Vela for Windows

A native **WinUI 3 / XAML / C#** Windows 11 tray app inspired by Vorssaint. Vela is the independent working name required by [upstream's branding policy](NOTICE.md).

Version **0.2.2 is in development**. Current source `25fcb15c` passes **181 core and 20 Windows backend checks** on the physical x64 Dell, plus x64 native XAML/OCR publishing and ARM64 cross-publishing. Image batches now continue after unreadable or size-failing inputs and report completed/failed results without losing earlier copies. M4A/MP4/JPEG size limits, on-demand developer feeds, ranked command search and prior focused UI evidence are preserved. The full UI, input and hardware matrix remains open. Normal settings and the accepted taskbar cue remain intact. The last packaged release remains 0.2.1. See the [image-batch checkpoint](docs/windows-image-batch-checkpoint.md), [M4A checkpoint](docs/windows-audio-budget-checkpoint.md), [developer-feed checkpoint](docs/windows-developer-feeds-checkpoint.md) and [verification notes](docs/verification.md). This is an unfinished port, not a new release.

- [Windows feature audit and exclusions](docs/windows-feature-audit.md)
- [Machine-readable inventory](docs/feature-audit.json)
- [Windows verification and remaining work](docs/verification.md)
- [Hardware support and the target desktop](docs/hardware-support.md)
- [Design and architecture](docs/architecture.md)
- [Portable build quick start](docs/portable-readme.md)
- [Move development to a Windows PC](docs/windows-handoff.md)

Windows-native capabilities are excluded from the runtime catalog. **PowerToys is not treated as built in.** Partial overlaps are split so features such as timed keep-awake and extra dimming remain useful without recreating Windows power or brightness settings. There is no Dynamic Island or Liquid Glass.

The app has one system tray icon, a compact Mica flyout, native Settings navigation, and four-step onboarding: welcome, tool selection, selected access explanations, and completion. Disabled resident features tear down their listeners; their settings remain available when re-enabled. Settings can be reordered, themed, exported and imported.

## Implemented tools

- Timed keep-awake, application rules, external-display rules and AC-only requests.
- Link tracking cleanup, optionally for plain-text copied links.
- Session file/text/link shelf with multi-file drag in/out and configurable taskbar, taskbar-icon or tray-icon drop targets.
- Snippet library with arbitrary triggers, immediate or Space/Tab/Return activation, and date/time/clipboard variables.
- Clipboard auto-clear on delay, lock or suspend, copied-image PNG saving, and shortcut-driven plain-text paste with clipboard restoration.
- Cleaning mode with optional mouse lock, a 60-second deadline and hold-Escape recovery.
- Quick panel, ranked multi-word/quoted-phrase command search and named launcher profiles; radial submenus with keyboard navigation, centre-drag selection and a small-window list layout. Focused laptop mouse cases pass; the broader gesture/accessibility matrix remains open.
- Open-window search with display filtering and pin/unpin; named custom window workspaces with restore and undo.
- Session-only image pins from the clipboard or selected files, with resizable Mica windows and a topmost toggle.
- Smooth wheel scrolling, Alt+wheel horizontal scrolling, horizontal inversion, button shortcuts, duplicate click filtering, Caps Lock Super key, and per-app double-press close protection.
- Per-display software dimming, on-demand external-monitor DDC/CI brightness, sustained resource alerts and supported NVIDIA temperature/power readings with thermal alerts.
- All-microphone mute/unmute.
- Batch image conversion, proportional resizing, JPEG quality and per-image size limits, configurable text watermarks and offline English OCR; MP4 conversion and M4A audio extraction with optional per-file size limits and trimmed animated GIF batches through Windows codecs.
- On-demand developer RSS/Atom release headlines with saved sources and local title search.
- On-demand HTTP latency, download and upload measurements with cancellation and bounded data use.
- Review older files in a selected messaging-download folder before explicitly sending chosen files to the Recycle Bin.

These 31 tools describe the source implementation. Device support is discovered at runtime; monitor brand alone does not establish DDC/CI support. CPU/case/GPU fan control and additional CPU/AMD/Intel thermal backends are unfinished. Input compatibility, clipboard ownership, Core Audio, accessibility and rendered layouts remain subject to the Windows checklist. Remaining non-native work is listed in the audit; the app exposes no placeholder toggles for it.

## Build on Windows

Use Windows 11 and the **.NET 10 SDK**. The NuGet packages pin Windows App SDK 2.5.1, the SDK build tools and Tesseract 5.2.0; the project is unpackaged and self-contained. Windows 11 ARM64 is supported for a Fusion VM on this Apple Silicon Mac, with a separate x64 target for conventional PCs.

From PowerShell in a Windows checkout:

Exit any Vela instance running from the output folder before rebuilding. The scripts refuse to overwrite a running preview.

```powershell
# ARM64 / VMware Fusion on Apple Silicon
./tools/build.ps1 -Architecture arm64 -Run

# x64
./tools/build.ps1 -Architecture x64 -Run
```

After building, `tools\package.cmd arm64` (or `x64`) creates a portable ZIP and prints its SHA-256. The ZIP includes `Install.cmd` and `Uninstall.cmd` for per-user installation and removal. Installation preserves saved preferences and adds Desktop/Start shortcuts plus a Windows Installed apps entry. No administrator access is required.

To install a published build into `%LOCALAPPDATA%\Programs\Vela`, exit Vela and run `artifacts\Vela-arm64\Install.cmd` (or the x64 folder's Install.cmd). This adds Desktop/Start menu shortcuts and Windows Installed apps registration, and preserves an existing sign-in startup choice.

The build also publishes an isolated x64 OCR worker and its pinned English model. OCR requires the Microsoft Visual C++ x64 runtime; ARM64 uses Windows x64 emulation for that worker. The executable and its required files are written to `artifacts/Vela-arm64` or `artifacts/Vela-x64`. Keep the whole publish folder together. Builds are unsigned. A successful publish is not a runtime test. GitHub Actions contains both architectures, but no workflow has been dispatched or verified from this workspace.

For local UI validation, exit Vela and launch the executable with `--data-directory "C:\absolute\local\fixture-folder"`. This creates separate settings and startup diagnostics and prevents sign-in registration changes. Do not point it at `%LOCALAPPDATA%\Vela`. `--settings` opens General settings directly after completed onboarding. The app retains one active instance; exit a validation instance before launching normally. See the laptop checkpoint for evidence and limits.

If PowerShell's script policy blocks `.ps1` files, run `tools\build.cmd arm64` (or `tools\build.cmd x64`) from Command Prompt. It runs the same tests and publish commands without changing the machine's script policy.

To use Visual Studio, open `Vela.sln` with a WinUI-capable Visual Studio installation supporting .NET 10 and select x64 or ARM64. The PowerShell script is the reference build command.

## Checks available on macOS

```sh
dotnet run --project tests/Vela.Core.Tests -c Release
dotnet restore src/Vela.Windows/Vela.Windows.csproj -p:Platform=ARM64 -r win-arm64
python3 tools/check-windows-types.py
dotnet run --project tools/XamlMetadata -c Release -- .
```

The API check compiles C# against the actual WinUI/Windows SDK reference assemblies and supplies **temporary XAML field stubs only for that check**. They are never included in the app. The metadata check validates XAML types and property names without creating Windows objects. Neither check compiles XAML, renders the UI, or proves the app runs. Microsoft's XAML compiler cannot execute on macOS.

## Local data and lifecycle

Settings, snippets, launcher paths and saved window titles/positions live in `%LOCALAPPDATA%\Vela\settings.json`. The shelf exists only for the current session and stores references to explicitly dropped files. Vela does not index the disk, collect clipboard history or record audio. The optional Internet speed test contacts Cloudflare only when started, uses up to 80 MB plus overhead, and uploads generated zero bytes. Cloudflare receives the public IP and test traffic; Vela sends no user content or results report. Developer feeds contact only your chosen feed URLs on Refresh; URLs are saved/exported, while headlines stay in memory. Exported settings include snippet contents, file paths and feed URLs.

Start-at-sign-in is opt-in and uses the current user's Run entry. It can also be disabled through Windows Startup apps. Closing the panel hides it; **Exit Vela** removes the tray icon and releases resident services, wake requests, input hooks, window pins and dimming overlays. Microphone mute state is a user-selected Windows device setting and remains as chosen.

GPL-3.0-or-later. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

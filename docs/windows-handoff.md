# Continue Vela on Windows

Refreshed October 18, 2026. This document carries the implementation context to a new Windows Codex session. It is not the full conversation history.

**Current continuation — October 19:** resumed Computer Use is authorized, but the user is now away for a long time and cannot perform manual steps. Continue independently without further prompts. Snapshot `4af60249` passes 149 core and 17 x64 backend checks plus native x64 and ARM64 cross-publishing. Read [the command search checkpoint](windows-command-search-checkpoint.md) first, then [the earlier October 19 UI checkpoint](windows-ui-checkpoint-2026-10-19.md) and [the October 18 transfer checkpoint](windows-laptop-checkpoint.md). Onboarding/settings/radial checks have progressed; JPEG quality and byte limits are fixed/implemented. Command search now supports multi-word/phrase/path matching, ranked results and a verified keyboard viewport fix. Remaining UI and feature work stays open. Preserve normal settings and the accepted five-file cue; do not publish anything. Earlier Mac/VM details remain historical.

## Product and requirements

Vela is an independent Windows port of [Vorssaint](https://github.com/vorssaint/vorssaint-utils). The working name follows upstream's trademark policy; keep LICENSE and NOTICE.md. Source is GPL-3.0-or-later.

The user wants the complete useful non-native port, not only a UI prototype:

- First audit the original features against Windows itself. Exclude Windows-native duplicates. **PowerToys does not count as built in.** Preserve only the extra behavior of partially overlapping features. Read docs/windows-feature-audit.md and docs/feature-audit.json before changing scope.
- Use native **WinUI 3 / XAML / C#**, one notification-area icon, a compact flyout/window with Mica or desktop acrylic, native Windows 11 onboarding and settings, and a modular grid with dynamically enabled tools.
- Use Fluent controls, sensible corner radii, Segoe UI Variable, and the native TitleTextBlockStyle/BodyTextBlockStyle text ramp. **No Liquid Glass. Dynamic Island is deliberately omitted.**
- Support common hardware through capability discovery. Missing hardware must produce an honest unavailable state, not sample readings or placeholder controls.
- Continue implementing and testing independently. The user has authorized work on the project and has offered a Windows laptop for development. The laptop is currently unavailable, so active development continues on the Mac with the Fusion Windows VM. Work autonomously where possible; ask only when information is necessary. No publication or new remote repository has been requested.

## Read first

1. README.md — current implementation, build and local data.
2. docs/windows-feature-audit.md and docs/feature-audit.json — all 77 upstream feature decisions and remaining residuals. Upstream audit commit: 794608a58d8a88608d3c3cb2e4f22373cf52bfdc.
3. docs/verification.md — actual verification evidence and the open checklist. Do not turn unchecked items into claims of support.
4. docs/architecture.md and docs/hardware-support.md — design, lifetime and hardware constraints.

## Current implementation

Version **0.2.2 is in development**. The last packaged release is 0.2.1. Thirty tools have implementations. The latest source passes the focused Windows checks below; broader validation remains open:

- Timed/rule-based keep-awake; link cleanup; session file/text/link shelf with configurable taskbar/tray targets; arbitrary snippet triggers with immediate/delimiter timing.
- Clipboard auto-clear; save copied images; plain-text paste with clipboard restoration; cleaning with optional mouse lock and timed/Escape recovery.
- Quick panel; command bar; nested radial launchers with keyboard navigation and a small-window list fallback; named launcher profiles; open-window search/pinning; saved custom window workspaces with restore and undo.
- Session-only image pins; smooth/horizontal/inverted wheel input; mouse button shortcuts; duplicate-click filtering; Caps Lock Super key; double-press close protection.
- Extra software dimming; external-monitor DDC/CI brightness; sustained resource alerts; NVIDIA temperature/power readings and thermal alerts; all-microphone mute.
- Image conversion/resize/JPEG quality/text watermarks/offline English OCR; MP4/M4A/trimmed animated-GIF batches; explicit messaging-download review; on-demand bounded internet speed tests.

The catalog rejects native and deferred features rather than exposing inert toggles. Closing the panel hides it; Exit releases resident services. Preferences live in %LOCALAPPDATA%\Vela\settings.json. Sign-in startup is optional. Review actual source and verification notes before extending behavior.

## Development on the laptop

Use **Windows 11 22H2 or later**, with the native Windows Codex agent/PowerShell, and install the **.NET 10 SDK** (not just the runtime). Visual Studio is optional for the command-line build. The last verified SDK was 10.0.402; global.json allows later .NET 10 feature releases. Dependencies restore from NuGet. Windows App SDK 2.5.1 and the Windows SDK build tools are pinned in the projects.

Keep the source on a native local Windows filesystem, for example C:\Dev\Vela-transfer\Vela. Open that source directory as the Codex project. Do not try to run Microsoft's XAML compiler on macOS or develop this WinUI app inside WSL.

For an Intel/AMD laptop, run these from the source root in PowerShell, in order:

```powershell
.\tools\build.cmd x64
.\tools\check-windows.cmd x64
.\artifacts\Vela-x64\Vela.Windows.exe
```

For a Snapdragon/other ARM64 laptop, use arm64 instead of x64 in those three paths/arguments. Confirm the laptop architecture in Settings > System > About; the intended desktop below is a separate machine.

build.cmd runs the core checks, publishes the native XAML application, and publishes the isolated x64 OCR worker. check-windows.cmd then runs the backend integration checks using that worker. Both .cmd scripts avoid changing PowerShell's script policy. Keep the entire publish folder together. Offline OCR also needs Microsoft's Visual C++ x64 runtime, including under ARM64 emulation; see docs/portable-readme.md for the Microsoft download. The normal development/build flow does not require running Codex as administrator.

To package a successful build, run .\tools\package.cmd x64 (or arm64). The generated Install.cmd installs for the current user, preserves preferences, and adds shortcuts/Installed apps registration. Exit running Vela before installing an update. The preview is unsigned.

The transfer includes the older 0.2.1 x64 ZIP in the sibling Vela-build/0.2.1 folder. It does not contain the October 18 fixes; build the bundled source to use those fixes. To try it before building, extract that ZIP completely and run Vela-x64\Install.cmd or Vela.Windows.exe. A fresh Windows account gets fresh onboarding and settings. The VM's personal settings and session data are not included in the transfer.

## Verified baseline

The optional older preview is described by Vela-build/0.2.1/build-provenance.json and compiled-source-manifest.json: version 0.2.1, 79 core checks, nine Windows integration checks per architecture and 81 compile inputs. Its binary is preserved byte-for-byte. It is separate from the newer source. Vela/SOURCE-FILES.sha256 covers the complete source delivered in this transfer.

The latest source and VM build snapshot is `3b791fb4-ff70-4ab1-a947-0a94388639dd`. All 108 compile-input hashes match the bundled source. ARM64 and x64 each passed 130 core and 12 backend checks plus the separate native Unicode clipboard publication/restoration fixture; x64 ran under ARM64 emulation. The latest guest ZIPs have not been collected for host CRC/PE/embedded-manifest validation and are not included as a new release.

Focused ARM64 desktop checks passed exact saved output for the complete immediate/Space/Tab/Return sequence, longest suffix, Unicode, multiline and clipboard placeholder at 150 ms/key, and eight consecutive Space expansions at 75 ms/key. Both retained the original text and HTML clipboard formats. The configured plain-paste shortcut passed exact output and rich clipboard restoration. Shelf Add clipboard, extended selection and Copy selected transferred two known files into Explorer, with source originals and target copies matching byte for byte. Closing an active keyboard cleaning window released input before the 60-second deadline. These are Notepad/Explorer and VM observations, not general editor, IME or physical-device support.

The user accepted the `edc0eb10` taskbar cue: “Box gone; drop and fade work.” Its five receiver files remain byte-identical in the latest source. Preserve direct release on the icon, the real Explorer icon beneath the cue, confined padding, and both fade directions. The earlier rejected UI Automation label `Vela.Windows - 1 running window` has a captured regression. Do not reintroduce AccessibilitySettings.HighContrastChanged subscription; it caused a verified startup COM failure. Main application windows retain Mica; the temporary transparent drop receiver intentionally has no material or native frame.

Actual outgoing multi-file drag/cancellation/remove-after-drop, mouse pointer/scroll locking, feature-disable/session/clipboard-in-flight cancellation, broader editors/IME/elevated apps, physical x64/devices and live dark-shell switching remain open. CUA dragging did not establish an endpoint, so it gives no verdict for outgoing drag. Do not treat these focused passes as completion of the port. Read the dated details in docs/verification.md. The transfer's validation folder records the native compile snapshot and a concise checkpoint; the full new guest logs are still inside the VM.

Earlier runtime evidence covers onboarding, tray/global shortcuts, settings/themes at 200%, install/update/removal with preference preservation, awake, clean links, plain-paste clipboard restoration, workspaces, keyboard-only cleaning recovery, speed tests, image pins, GIFs and nested launchers. Physical x64, device and broader accessibility/input validation remain open.

Do not call an unattended build a rendered UI test. Repeat tests when code changes or new evidence calls for them. Hardware capability discovery alone does not validate writes or make a brand supported.

## Remaining work and suggested next steps

First build and run the unchanged baseline on the laptop, verify onboarding/tray/settings and the integration checks, and record actual hardware/OS/scaling. Keep existing user preferences unless testing with an explicitly isolated account or agreed backup. The laptop's processor, GPU, display and peripherals are not known yet.

Then continue the non-native residuals in the audit. They include fan control and recovery watchdogs; additional CPU/AMD/Intel thermal and power backends; preferred-audio/disconnect automation and amplification; Bluetooth sleep rules; advanced capture/recording/scrolling/expiring-link workflows; reviewed app leftovers; disk/script/menu indexing; radial gestures; localization; developer-feed aggregation; and advanced clipboard search/file history. Basic Windows clipboard history is excluded as native. Each residual needs implementation and honest runtime validation before completion can be claimed.

Radial submenus, keyboard navigation and the small-window list fallback are now implemented. Limits are eight siblings, three levels and 64 entries per profile. The wheel opens dedicated launcher settings without enabling the Command bar; both editors share the active profile. Read the latest verification section before extending gestures or treating other accessibility/display scenarios as verified.

Additional open validation includes shelf file drag/drop and missing originals, six-pin UI limit, explicit third-party window unpin, broader input/clipboard ownership and Core Audio cases, accessibility, multiple monitors/mixed DPI, elevated application behavior and hardware support. See the complete checklist in docs/verification.md.

## Intended desktop hardware

The user's eventual desktop is **Intel i7-11700K, EVGA RTX 3080, 32 GB DDR4-3200, ASUS Z590 Pro WiFi, Noctua NH-D15, LG 24GN600-B over HDMI/DisplayPort**. The exact motherboard model may need confirmation when implementing its backend. The development laptop may have different hardware.

DDC/CI brightness and installed-driver NVIDIA NVML temperature/power queries exist. No CPU/AMD sensor backend or fan-writing backend is implemented. The Noctua cooler is controlled through motherboard PWM headers, not a separate addressable device. No kernel driver, fan/voltage/clock write, firmware setting or Windows security setting was changed for this port. Hardware-specific development must keep firmware recovery and failure handling explicit; do not claim the ASUS/EVGA/LG desktop was tested in the VM or on another laptop.

## Source and task transfer

The Mac folder was initialized as Git but has **no commits and no remote**; all source files were untracked. The transfer deliberately includes every source file reported by Git, including untracked files and dotfiles. It excludes .git, bin/obj caches, Mac/VM machinery, credentials and personal Vela data. No upstream push has occurred. Do not rely on git clone or the upstream repository to recover this port. Establish a local Git checkpoint on Windows before starting new edits; choose any shared remote only with the user.

A fresh Windows chat can continue using this document but does not inherit the complete original conversation. Exact-chat handoff requires the same account/workspace, a connected Windows host, and saved projects for the same Git repository on both devices. See the official [Windows app guide](https://learn.chatgpt.com/docs/windows/windows-app) and [remote connections/handoff guide](https://learn.chatgpt.com/docs/remote-connections). Signing in alone does not copy the local working directory or its task state.

Suggested first prompt in the Windows project:

> Read docs/windows-handoff.md, README.md, docs/windows-feature-audit.md and docs/verification.md. Continue Vela from the current implementation. Set up the Windows build dependencies, verify the unchanged baseline on this laptop, and then continue the unfinished non-native features. Preserve the Windows-native exclusions, WinUI 3/Mica design requirements and existing user data. Record actual test results and remaining limits; do not label the preview a finished port.


## Original Mac/VM state at the handoff checkpoint

At 18:53 on October 18, the original VM preferences were restored byte-for-byte from the latest local feedback backup, and preserved installed 0.2.1 reopened. No cleaning lock or test build was left active. No personal settings, clipboard contents, credentials, Mac/VM files or authentication data are bundled. Do not run VM-specific feedback helpers on the laptop or copy another machine's preferences over its settings.

Windows display-off while plugged in was changed from five minutes to Never at the user's request to investigate black idle captures. Hibernation was already Never. No authentication, Mac or Fusion preference was changed. The task-owned temporary Mac keep-awake assertion was stopped. These details describe the original test environment and are not instructions to change the laptop's system settings.

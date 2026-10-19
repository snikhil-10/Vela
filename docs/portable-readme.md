# Vela for Windows 0.2.2 — unreleased preview draft

Vela is an independent Windows port of Vorssaint, built with native WinUI 3, Mica, Windows 11 controls and a system tray icon. It contains 30 implemented tools. Windows-native duplicates are excluded; PowerToys is not counted as built in. Dynamic Island and Liquid Glass are omitted.

## Install or run

1. Choose **x64** for an Intel/AMD Windows PC, including the target i7-11700K / RTX 3080 desktop. Choose **ARM64** for VMware Fusion on Apple Silicon or an ARM Windows PC.
2. Extract the complete ZIP. Keep every file and subfolder together.
3. Run **Install.cmd** to install for your Windows account, add Desktop/Start menu shortcuts and register Windows Installed apps. Exit any running Vela instance before installing an update. Saved preferences are preserved.
4. Alternatively, open **Vela.Windows.exe** directly from the extracted folder for portable use.
5. Complete the four-step welcome experience and select your tools. Click Vela's notification-area icon (it may be behind the tray's up arrow). **Ctrl+Alt+V** opens the panel when Quick panel is enabled. Settings enables, disables and reorders tools.

Closing a panel hides it. Use the tray menu's **Exit Vela** or Settings → General → Exit to stop its background services. Sign-in startup is opt-in; installation preserves an existing startup choice. If you move a portable folder, turn startup off and back on to update its path.

To uninstall, exit Vela and use Windows Installed apps or **Uninstall.cmd**. This removes installed app files, shortcuts and startup registration. Preferences in `%LOCALAPPDATA%\Vela` are preserved.

Windows 11 version 22H2 or later is required. .NET and Windows App SDK runtimes are included. Administrator access is not required. These local preview builds are unsigned. Offline English OCR uses a bundled x64 Tesseract worker and model; it needs Microsoft's Visual C++ x64 runtime, including under ARM64 emulation. If OCR reports a missing runtime, install it from [Microsoft](https://aka.ms/vs/17/release/vc_redist.x64.exe).

## Tools

The modular panel includes timed/rule-based awake requests, clean links, file shelf, snippets, clipboard auto-clear, save clipboard image, plain-text paste, keyboard cleaning, quick/command/radial launchers and profiles, open-window search/pinning, saved window workspaces with undo, session-only clipboard/file image pins, mouse-wheel and keyboard enhancements, DDC/CI monitor brightness, extra software dimming, resource and NVIDIA thermal alerts, microphone mute, batch image conversion/resize/quality/text watermarks/OCR, batch MP4/M4A/trimmed animated-GIF conversion, messaging-download review, and an on-demand internet speed test.

Plain paste's default shortcut is **Ctrl+Alt+Shift+V**; open-window search is **Ctrl+Alt+W**. Shortcuts are configurable. Radial launchers support eight items per menu, three levels and 64 items per profile. Use 1–8, Up/Down, Enter/Space, Backspace/Left and Escape. Small windows use a scrollable list. Manage and reorder groups using the wheel's settings button or the Command bar; command search includes nested launchers. Plain paste targets regular desktop applications; elevated, secure or delayed-paste applications need their own paste command. It restores supported clipboard formats after pasting and avoids overwriting a newer copy. Windows clipboard history remains managed by Windows.

Monitor brightness appears only after a successful DDC/CI capability query. NVIDIA readings depend on the installed driver's NVML support. No fan, voltage or clock control is included. See **hardware-support.md** for the target ASUS/Noctua/EVGA/LG desktop and brand limitations.

Image/video outputs are new files; originals remain untouched. Optional white text watermarks support four corners, opacity and size controls, and preserve PNG transparency. Codec support follows the installed Windows codecs. Download review scans metadata only inside a folder you explicitly choose, excludes recently copied files and linked folders, and requires selection and confirmation before recycling anything.

## Verification and limits

The October 18 physical x64 laptop continuation, snapshot `7a0378fb-773b-475e-b818-9a018478604d`, passes 135 core and 16 Windows backend checks with native XAML publishing and 112 verified build inputs. The unchanged transfer baseline passed 130 core and 12 backend checks first. Radial centre-drag selection and shelf lifecycle fixes are in source; rendered gesture, shelf, cleaning and broader input tests remain open. Computer Use reported a physical Escape stop before the isolated onboarding profile could be inspected. Read-only discovery found one microphone endpoint, no supported NVIDIA sensors and no DDC displays; no hardware writes were tested. The accepted taskbar receiver remains unchanged. The earlier ARM64/VM evidence and its limits are retained in verification.md. No 0.2.2 package or installation was made. See windows-laptop-checkpoint.md in the source checkout for the full checklist.

This is not a finished port. Fan control, additional thermal backends, audio-priority automation, Bluetooth sleep rules and several advanced residual workflows remain unimplemented. Other input, clipboard, display, accessibility and device cases still need testing. See **verification.md** and **windows-feature-audit.md** for exact status.

Settings, snippets, launcher paths and saved workspace titles/positions are stored in `%LOCALAPPDATA%\Vela\settings.json`. The shelf lasts for the current session. Vela has no telemetry, account or cloud sync. The optional speed test uses Cloudflare only when started, with up to 80 MB plus overhead and generated zero-byte uploads; Cloudflare receives your public IP and test traffic. Vela sends no files, clipboard content or results report. Startup failures are logged locally in `%LOCALAPPDATA%\Vela\startup-error.log`.

The corresponding source ZIP includes build scripts and the complete feature audit. Source is GPL-3.0-or-later; see **LICENSE** and **NOTICE.md**. Bundled Microsoft and OCR components retain their own licenses and notices.

The accepted taskbar cue retains direct icon drops, the real Explorer icon beneath the transparent receiver, confined padding and both fade directions. Main application windows retain Mica. Its five receiver files are byte-identical to the transferred baseline.

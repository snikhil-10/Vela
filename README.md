# Vela for Windows

Vela is a native Windows 11 tray app with tools for files, focus, media and everyday workflows. It uses WinUI 3 and Mica.

**Vela 0.2.2 is a development preview.** Some features and device combinations still need testing. Vela is an independent, unofficial project inspired by [Vorssaint](https://github.com/vorssaint/vorssaint-utils).

## Download and run

1. Sign in to GitHub, open [Vela's build workflow](https://github.com/snikhil-10/Vela/actions/workflows/windows.yml) and select a successful run.
2. Under **Artifacts**, download `Vela-preview-x64` for an Intel or AMD PC. An ARM64 build is also available, but its current runtime behavior has not been verified on ARM64 hardware. Workflow artifacts expire.
3. Extract the complete download and run `Vela.Windows.exe`. Keep all files and folders together.
4. Choose your tools during onboarding. Open Vela from the notification area; the icon may be behind the taskbar's up arrow.

For a per-user installation with Desktop and Start menu shortcuts, run `Install.cmd` from the extracted folder. Exit Vela before installing an update. Use Windows Installed apps or `Uninstall.cmd` to remove the installation; your preferences are kept.

Vela requires **Windows 11 build 22621 or later**. The app is self-contained and unsigned. Offline English OCR also needs the [Microsoft Visual C++ x64 runtime](https://aka.ms/vc14/vc_redist.x64.exe); the OCR worker is x64 in both builds.

## Features

- A session file shelf, image pins, tracking-link cleanup, snippets and clipboard tools.
- Timed and rule-based keep awake, cleaning mode, window search and saved workspaces.
- A quick panel, command search, launcher profiles and a radial menu.
- Optional keyboard and mouse enhancements with application exclusions.
- Batch image and video tools, watermarks, resizing and offline English OCR.
- Per-display dimming, supported external-monitor brightness, resource and NVIDIA thermal alerts, and microphone mute.
- On-demand developer feeds, internet speed testing and messaging-download review.

Tools can be enabled individually. Monitor, GPU and codec features depend on the device and installed drivers. Windows continues to handle its built-in features such as Snap, Alt+Tab, standard screenshots and basic clipboard history.

## Build from source

Use Windows 11 and the **.NET 10 SDK**. The first build restores the pinned NuGet dependencies. Exit any running Vela instance before rebuilding its output.

```powershell
.\tools\build.cmd x64
.\artifacts\Vela-x64\Vela.Windows.exe
```

For an ARM64 cross-build, use `.\tools\build.cmd arm64`. Keep the complete output folder together.

To run the Windows backend and desktop component checks after an x64 build, exit Vela and run:

```powershell
.\tools\check-windows.cmd x64
.\tools\check-desktop.cmd
```

Desktop checks need an interactive Windows session. The portable core suite can also run on Linux or macOS with `dotnet run --project tests/Vela.Core.Tests -c Release`.

## Settings and privacy

Preferences, snippets, launcher paths and saved workspace information are stored in `%LOCALAPPDATA%\Vela\settings.json`. The shelf and image pins last for the current session. Exported settings may contain snippets, file paths and feed URLs; sign-in startup is configured separately.

Vela has no account, analytics or cloud sync, and does not record microphone audio. Developer feeds contact saved URLs only when refreshed. The optional speed test contacts Cloudflare and uses up to 80 MB plus overhead, including generated upload traffic.

Closing the panel hides it. Use **Settings → General → Exit** to stop the app and release its resident services. Start-at-sign-in is opt-in.

## License

Vela is licensed under **GPL-3.0-or-later**. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md). Third-party OCR license notices are included with the source and app.

# Shelf, cleaning and snippet comparison

Compared October 17, 2026 against Vorssaint source revision `5457c2d69a0dffb62a7c644252e28207b1188457`. The original 77-feature scope audit remains pinned to its earlier revision; this focused comparison does not silently expand that scope.

| Behavior | Vorssaint reference | Vela 0.2.2 implementation |
| --- | --- | --- |
| Snippet trigger prefix | Arbitrary non-whitespace trigger; no required semicolon | Arbitrary 1–64-character trigger; examples `sig`, `@email`, `;signature` |
| Expansion timing | Immediate or after Space/Tab/Return; new entries default to delimiter mode | Per-snippet timing picker with the same choices; existing Vela entries retain immediate behavior |
| Matching | Exact case by default; longest matching suffix | Exact case and longest matching suffix; immediate shorter triggers can naturally complete before a longer one is typed |
| Delimiter | Preserved with the replacement | Completing physical key is consumed; replacement is followed by exactly one delimiter before paced input replay |
| Editing context | Clears context on navigation/focus changes | Clears on window/focus changes, mouse clicks, shortcuts, navigation and dead-key/IME transitions |
| Shelf destinations | Shelf window, menu-bar icon/area and other macOS-specific gestures | Configurable full taskbar, Vela taskbar button or visible tray icon. Highlight appears when dragging begins, release on the icon adds the file, then the shelf opens. Direct drop, frameless padded cue and two-way fade accepted by the user at edc0eb10 |
| Shelf collection | Files/folders, text and links; multiple items | Files/folders, text and links; extended selection and complete multi-file drag packages |
| Remove after drop | Configurable | Configurable; only accepted drags remove entries, and originals remain intact |
| Cleaning | Keyboard lock with an emergency unlock | Keyboard lock plus requested optional mouse lock, 60-second deadline and continuous two-second Escape recovery |

Reference files are `Services/Snippets/TextSnippetSupport.swift`, `Services/Snippets/TextSnippetService.swift`, `UI/Settings/TextSnippetsSettings.swift`, `Services/Shelf/ShelfService.swift`, its support files and `UI/Settings/ShelfSettings.swift` under `Sources/Vorssaint` in the [upstream repository](https://github.com/vorssaint/vorssaint-utils/tree/5457c2d69a0dffb62a7c644252e28207b1188457).

Windows notification icons do not offer an OLE file-drop callback. Vela places a temporary drop window over the selected region while dragging; it does not replace Explorer's taskbar. Taskbar-button mode keeps a minimized shelf window so that a real Vela button exists. Tray mode needs a visible tray icon. The taskbar-button detector inspects the taskbar’s own accessibility subtree on a worker and validates the matching Vela button’s bounds before showing its target. The drop surface validates the supplied payload before accepting it. A mouse movement with its button held can briefly show the affordance even when its source does not provide transferable data.

Validation results and remaining platform limitations are recorded in [verification.md](verification.md). These changes do not complete the unimplemented device, capture, audio or other residual features listed there.

**Current validation:** The taskbar cue in native snapshot `edc0eb10` passed the user’s physical direct-drop and fade checks; its five receiver files remain byte-identical. Snapshot `3b791fb4` passes 130 core and 12 Windows backend checks per architecture, plus a separate native Unicode clipboard publication and restoration fixture. Fresh saved-file checks pass both the complete mixed-trigger sequence at 150 ms/key and eight rapid consecutive expansions at 75 ms/key, preserving original text and HTML in each. The plain-paste shortcut also passes exact output and rich clipboard preservation. Two-file shelf Copy selected transfers both files into Explorer with byte-for-byte preservation of originals and copies; actual drag-out remains unverified. Closing active keyboard cleaning restores typing before timeout. Original preferences and installed 0.2.1 were restored afterward. The current ZIPs remain in the guest pending collection and host archive validation. Remaining outgoing shelf/cleaning cases, editor/IME compatibility, physical hardware and dark-shell switching remain open. Detailed evidence is in [verification.md](verification.md).

The earlier taskbar matcher rejected `Vela.Windows - 1 running window`; its regression fails before the correction and passes afterward. The accepted receiver preserves the original icon, confines the padded highlight to the taskbar and fades both ways. Main application windows retain Mica.

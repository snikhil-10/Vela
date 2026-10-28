# Image pin and shelf follow-up — October 19, 2026

Source `7e03f407-7a46-41f7-b331-486f737383b4` has **130 verified product/backend compile inputs**. It passes **181 core and 22 physical x64 Windows backend checks**, native x64 XAML/OCR publishing and ARM64 cross-publishing. ARM64 execution is untested on this Dell. The [JSON checkpoint](../validation/windows-pin-shelf-checkpoint.json) records output and evidence hashes. This is an unreleased 0.2.2 development preview; the full port and original checklist remain incomplete.

## User-assisted results on the preceding source

The user tested snapshot `f77132b9` on the Dell Pro 14 Plus PB14250, using the isolated profile under `artifacts/functionality-audit-2026-10-19/profile`. These physical interactions were reported by the user; the agent did not observe the drag or picker completion itself.

| Case | Actual result |
| --- | --- |
| Two-file outgoing shelf copy, automatic removal Off | User reported both files copied. Independent hashes confirm both 33-byte copies match their generated originals and both originals remain intact. |
| Escape during drag, automatic removal On | User reported both shelf entries stayed. This passes the mid-drag cancellation case on the preceding source. |
| Escape on Explorer's duplicate-file skip/replace dialog | User reported the shelf entry disappeared. This is a reproduced user-reported bug, distinct from mid-drag cancellation. |
| Batch image native picker and JPEG conversion | User reported success. Independent decode confirms a 14,314-byte, 1000×160 JPEG. The generated source PNG retains SHA-256 `8b3834dbe783db867d8597ba5fac4c97a16300562ce2d134c446cc9506cef488`. A completed mixed/corrupt batch through the picker remains unverified. |
| Choose image to pin; topmost behavior | User reported selection and staying above other apps worked. |
| Resize image pin to title bar | User supplied a screenshot showing the image completely hidden by collapse. This is a reproduced user-reported bug. |

## Changes and new checks

Image pins now require a **320×280 logical-pixel minimum window**, converted to physical pixels at the current DPI. A XamlRoot change updates that minimum, and the image has a 96×96 minimum layout area. Resizing, the topmost toggle and the shared Mica style remain available. Windows provides the native presenter minimum-size properties used here; see [Microsoft's window management documentation](https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows).

The separate [native pin fixture](../validation/fixtures/pin-minimum/README.md) instantiated the production window using the **exact final x64 product DLL and PRI**, a generated bitmap and its own profile. At **150% scaling**, requested sizes 200×40, 500×40 and 40×500 were clamped to **480×420, 500×420 and 480×500 physical pixels**. The image remained within the client area at **179.3×112.0, 179.3×112.0 and 262.7×164.0 logical pixels**. Topmost and resizable flags remained enabled. This is an actual native component/layout check, separate from a physical border-drag observation. Mixed-monitor DPI changes, accessibility scaling, unusual image aspect ratios and the six-pin lifecycle remain open. Earlier fixture attempts failed on resource lookup and tray-icon setup; the final fixture loads the required PRI and starts no resident services.

The shelf previously trusted `DragItemsCompleted` with Copy even while Explorer could still be asking about a collision. File entries now need a native **Logical Performed DropEffect** Copy receipt, after any native asynchronous operation has ended. Missing, canceled or unoffered effects retain the entries. Waiting stops after 30 seconds without treating elapsed time as success. Closing the tool or canceling the shelf session stops pending confirmation; later imports are protected by object identity. The native receipt is documented in [Shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard), and asynchronous completion in [Shell data transfer scenarios](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios). This implementation reads the drag's data object and does not replace the global clipboard.

The new 22nd backend check exercises the real DataPackage/IDataObject bridge and native receipt round trips. Bare accepted Copy, None, Move, Link and combined effects retain both files; a canceled wait retains them; only an explicit Copy receipt allows removal of the exact original entries. Disabling removal retains them, replacement entries survive a late result, and original file bytes stay unchanged. This fixture supplies the target receipt itself. **Explorer's duplicate-dialog Escape, successful automatic removal and mixed skip/replace/partial-copy behavior still require end-to-end retesting on the new build.** A target that omits feedback, or finishes after the bounded wait, can leave entries after a successful copy. No original files are deleted by automatic shelf removal.

## Build, preservation and present state

The candidate is `artifacts/Vela-followup-x64/Vela.Windows.exe`. The older `artifacts/Vela-x64` preview remains running with its isolated profile because Computer Use could not complete a normal exit: `coordinate input geometry is unavailable`, followed by `window is not a usable app window` on the recovery attempt. It was not force-terminated. The final candidate has not received a full resident UI tour. The component fixture exited normally. ARM64 output is `artifacts/Vela-arm64`; both published manifests match the new source manifest, PE machine fields are x64 `8664` / ARM64 `AA64`, and both contain the compiled XAML PRI. All final build logs contain no compiler warning/error diagnostics.

Normal `%LOCALAPPDATA%/Vela/settings.json` remains SHA-256 `b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`; its sign-in Run entry remains absent. The five accepted taskbar cue files and `WindowStyle.cs` remain byte-identical to the October 18 native snapshot. The 31-tool catalog and 77 native/split/deferred scope decisions are preserved. No hardware setting, normal profile, source original, installer, package, remote or publication was changed by this follow-up. The user's manual clipboard use is separate from the earlier automated audit's historical sequence-36 observation; that old sequence is not claimed current.

Remaining tests also include broader physical input, real editors/IME/elevation, lifecycle/clipboard ownership, hardware, high contrast, mixed DPI and accessibility as listed in [verification](verification.md). Missing historical Mac/VM archives and logs remain a collection task. No whole-feature or finished-port claim follows from these focused passes.

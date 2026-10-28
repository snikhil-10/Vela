# Shelf mouse-drag startup — October 19, 2026

Source **`28775fed-70f7-4f21-9e50-90a33910c8e9`** continues failed interactive candidate `bfaf9de5`, with **134 product/backend inputs**. Both application architectures publish without final compiler diagnostics. **26 x64 Windows backend checks pass**; unchanged core/test inputs retain the preceding **187-pass** result. An exact-DLL fixture passes native payload and completion calls across apartments and separate processes. **The revised physical gesture remains pending**, with its isolated shelf open for testing. [JSON evidence](../validation/windows-shelf-pointer-drag-checkpoint.json) records the actual tests and limits. This is an unreleased development preview.

**Subsequent physical result:** the user reported awkward dragging and an unconfirmed-copy message despite copying. Both destination hashes match the originals, and agent inspection found two retained shelf entries. The trace shows successful copying through WinUI's fallback; repeated experimental native mouse drags returned None, and a lingering native loop was canceled with automated Escape. Automatic removal and the experimental gesture failed. This resident then exited normally through Settings. [The next checkpoint](windows-shelf-shell-checkpoint.md) removes the competing mouse path and tests streamed extraction against the actual Windows folder handler. Initial pending statements below retain their historical context.

## Reproduced failure and diagnosis

The user reported that files could not move outside the shelf on `bfaf9de5`. Their screenshot shows two selected entries and a forbidden cursor. Three recorded DoDragDrop calls each ended with effect None, and the destination stayed empty. Both originals remain intact. A separate native discovery fixture found that both the original WinRT data object and the tracked object advertise CF_HDROP and accept its QueryGetData request. The earlier cross-process copies also pass. Those component results do not establish an interactive drag pass.

The preceding route canceled DragItemsStarting and queued a second drag. Competing WinUI/native gesture lifetimes are the working explanation, rather than a proven complete root cause. This candidate avoids that handoff and adds validation-profile format/callback diagnostics for the next physical observation.

## Change

For a left-mouse press on an already selected file row with removal On, the shelf snapshots the selected file references and disarms WinUI item dragging before movement. Once movement passes Windows' DPI-aware drag threshold, it releases the row/list pointer captures and starts native OLE directly. Only Copy is offered. Escape/session/window cancellation and successful async completion keep their existing guards. Stationary clicks and subpixel movement do not start the native operation.

If WinUI recognizes a gesture first, the mouse candidate is cleared and WinUI owns that gesture alone. It uses the earlier safe copy bridge, which retains file entries without final confirmation. Touch/pen, text, mixed-selection validation and removal-Off behavior retain that path. Pointer release, cancellation, capture loss and window close reset an armed mouse gesture. Self-drops are rejected during the native operation. No accepted taskbar receiver or Mica source was edited.

## Actual checks

- Both x64 and ARM64 final publishes pass without warning/error diagnostics. The initial compile failed on an unqualified Windows.Foundation namespace, with secondary XAML diagnostics; those logs are preserved. The corrected final build passes.
- The 26 Windows checks pass, including native mouse-source continue/release/Escape/cancellation rules; stationary/subpixel versus horizontal/vertical threshold cases at 100%, 150%, 200% and 300%, plus invalid scale fallback; transfer callback status/identity; and the preceding shelf/image/OCR/audio/video/GIF regressions. These are backend checks, not physical pointer-event coverage.
- The final DLL SHA-256 is **`77513e653d3d502a05402c4d8625980aa957912f93978312a229a02335099e85`**. A true STA source marshals its IDataObject into an MTA receiver, which reads two CF_HDROP paths in Unicode/order. Cancellation and failure reject completion with zero copies; S_OK Copy produces two identical copies. A separate local process repeats cancellation and success through native COM proxies. All generated originals remain identical. No global clipboard or Explorer UI is used by that fixture.
- Core source/test hashes match the preceding 187-pass run and the OCR worker is copied unchanged from the validated x64 output. Neither is presented as a new run. ARM64 execution remains untested.
- Computer Use opened the new native shelf and observed removal On, the manual target and an empty session. The failed resident exited normally. The new resident uses a separate validation profile and remains open for the user-assisted retry; its actual two-file copy/removal and cancellation results are pending.

## Preservation and remaining work

Outputs are `artifacts/Vela-shelf-pointer-drag-{x64,arm64}`; the current profile is `artifacts/manual-shelf-recheck-2026-10-19/profile-pointer`. Generated `source` and empty `fresh-destination-pointer` folders are prepared. Normal settings remain SHA-256 **`b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`**. The six accepted cue/Mica files match October 18; the catalog, schema and 77 scope decisions are unchanged. Nothing installed, packaged or published.

Physical two-file selection/drag, copying and automatic removal must pass before calling this correction verified. Duplicate-dialog ×/Escape, partial/skip transfers, mid-drag Escape, filtered outgoing transfers and loaded lifecycle also remain open. Unconfirmed destinations retain entries. Full picker/media lifecycle, broader input/clipboard/accessibility/DPI/hardware and unfinished non-native residuals remain in the audit. The preceding separate harness's SVG resource-staging discrepancy is unchanged and is not claimed fixed here.

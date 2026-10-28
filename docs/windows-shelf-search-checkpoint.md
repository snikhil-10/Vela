# Shelf search and pin lifecycle — October 19, 2026

Source **`83b3ad32-15f9-4f78-84ef-17f4f30474c6`** continues `7e03f407` with **130 verified product/backend inputs**. It passes **181 core / 23 physical x64 Windows backend checks**, x64 XAML/OCR publishing and ARM64 cross-publishing. Separate native fixtures use the exact final product DLL, core DLL and PRI. [JSON evidence](../validation/windows-shelf-search-checkpoint.json) records hashes and preservation checks. This is an unreleased 0.2.2 preview; the full port remains incomplete.

## Session shelf search

**Find shelf items** matches case-insensitive AND terms and quoted phrases against names, stored paths, full text and links already in the shelf. Results preserve order and object identity. Add/remove/reset refresh the view. Still-visible selections survive; hidden selections are dropped and are not restored silently when search is cleared. Copy, drag and Remove selected act on visible selected entries. Hidden shelf entries and original files remain intact.

Search adds no clipboard history, disk scan, background listener, persistence or settings fields. The 31-tool catalog is unchanged. Searchable/file clipboard history and full command-bar indexing remain deferred.

The new backend regression covers Unicode, phrases, links/paths, no matches, stable identity/order, a filtered native file package, canceled drag retention and confirmed removal of only the filtered entry. Generated original file bytes remain unchanged. Private DataPackages supply the inputs; the global clipboard is untouched.

The [native session-tool fixture](../validation/fixtures/session-tools/README.md) checks the production shelf controls with generated content and its own profile: initial empty state, live add/remove/reset, Unicode/no-match/clear, visible selection, hidden-entry retention and detachment after closing. All pass. These are control-property and real window-lifetime checks, separate from physical typing, drag, picker completion, screen pixels and accessibility observations.

## Pins and cleanup

The fixture creates **six visible native pins with nonzero image layout**, rejects a seventh without eviction, closes one and opens a replacement. Closing clears image sources and frees capacity. CloseAll releases every active source and is idempotent. Disabled/pre-canceled requests create no window; a later valid request succeeds.

An injected registration/theme-tracking exception reproduced a product bug: the new pin and image source stayed allocated and consumed capacity. The failing native log is preserved. Tracking and activation now share the existing close-on-failure guard. The same failure now closes the window, releases the source and returns the count to zero while propagating the exception.

The separate minimum-size fixture also passes against the exact candidate at **150% scaling**: requested sizes **200×40 / 500×40 / 40×500** clamp to **480×420 / 500×420 / 480×500 physical pixels**. Images stay inside the client and topmost/resizing remain enabled. Actual Settings-toggle teardown, full resident exit, physical border dragging, mixed DPI and unusual aspect ratios remain separate gaps.

Early fixture setup failures are not passes: the first standalone host exited with XAML code `0xC0000005` without its final log. A hidden host window kept XAML alive between close/reopen checks. A subsequent attempt reused one native bitmap across source lifetimes and failed after disposal; using a fresh generated bitmap per request matches the real decoder workflow. The independent registration-leak regression was reproduced after those fixture corrections, then fixed and rerun successfully.

## Build, preservation and limits

New loose outputs are **`artifacts/Vela-shelf-search-x64`** and **`artifacts/Vela-shelf-search-arm64`**. Both have matching compiled-source manifests, XAML PRI and correct PE machine fields (`8664` / `AA64`). ARM64 uses the same x64 OCR worker as designed; **ARM64 execution is untested on this Dell**. Final logs have no compiler warning/error or failed-test diagnostics.

Normal settings remain SHA-256 **`b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`**; sign-in Run remains absent. The five accepted cue files and `WindowStyle.cs` match the October 18 native snapshot. The catalog matches the parent; all **77 decision/native-equivalent/port-scope fields are unchanged**. Only implementation evidence in the audit was updated. Fixtures exited normally and neither read nor wrote the global clipboard. No normal profile, hardware setting, installation, package, remote or publication was changed.

The older `f77132b9` isolated resident preview remains running and was not force-terminated. The new candidate has no full resident UI tour. The earlier `7e03f407` x64 output is retained. Physical filtered outgoing drag, Explorer duplicate-dialog cancellation/successful auto-removal/partial copies, real Settings-toggle/exit, broader input/editor/clipboard lifecycle, accessibility, mixed DPI and hardware remain open. Deferred non-native residuals remain in the [feature audit](windows-feature-audit.md); historical Mac/VM collection is separate.

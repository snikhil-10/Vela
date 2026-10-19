# Command search continuation — October 19, 2026

This checkpoint precedes [MP4 size budgets](windows-video-budget-checkpoint.md), which records the newer `b627a158` source. Evidence below retains its original source identity.

Vela remains an unreleased 0.2.2 preview. This continuation works on the existing scoped Command bar while picker, drag-transport and hardware checks remain open. It needs no manual steps from the user and does not index the disk, collect clipboard history or change the Windows-native exclusions.

## Implementation

Search now accepts several case-insensitive literal words in any order. Quoted text remains one phrase, including while its closing quote is still being typed. Every term must match; a phrase cannot be assembled across a name/category/metadata boundary. Duplicate words and whitespace-only queries are handled consistently. Query entry is limited to 256 characters.

Enabled Vela tools, enabled snippet/window/workspace actions and the active launcher's saved names/paths remain the only sources. The feature does not execute or open a result during search. Exact command names rank before prefixes, then contained phrases, reordered name terms and metadata matches. Ties retain the original command order. Category labels, a result count and an empty-results message explain the list.

Up/Down changes the selected result while the query retains keyboard focus. Enter invokes the selected command. Native Tab/End navigation remains available in the ListView. The list's maximum height adapts to the available tool viewport, fixing a rendered defect where selection could move beneath the outer scroll area's visible edge. A string fallback keeps unrealized accessibility rows readable without exposing action/record details. No settings schema changed.

## Automated evidence

Current snapshot **`4af60249-96ed-4a8d-81dd-22a462917045`** has **117** verified app/test inputs. Its x64 and ARM64 native XAML/OCR builds pass **149 core checks** with no compiler warnings/errors. ARM64 is a cross-build from this x64 laptop; no ARM64 execution is claimed. **17 Windows backend checks** pass on the physical x64 Dell, including the existing JPEG, shelf, OCR, image-pin, clipboard-package and video checks.

Eight new core checks cover multi-field AND matching, quoted phrase boundaries, incomplete quotes, blank queries/source order, ranking priorities, stable ties, literal Unicode/path punctuation and repeated words/Unicode whitespace. The first core run exposed a null keyword-array edge case (148 pass / 1 fail); it was corrected before the native candidate. The failing log is retained.

## Rendered evidence and limits

All inputs belong to `artifacts/command-search-2026-10-19/profile`, with generated missing-file launcher paths. Normal settings are never loaded into that profile. No user file, clipboard publication, external launch or awake request was needed.

| Source | Actual observation |
| --- | --- |
| First candidate `e7bbd42b` | Blank query shows 23 scoped commands. `editor work` finds Work › Editor, `editor.exe personal` finds Personal › Editor through saved-path metadata, and `"work notes"` finds the single phrase match. |
| First candidate `e7bbd42b` | `notes` returns 18 results. Exact Notes precedes Notes archive; metadata-only Other is last. A long-list keyboard test exposes selection clipped below the visible edge. This is a retained failure, not a visibility pass. |
| Final `4af60249` | Ctrl+Alt+Space opens Command bar from Vela's panel. Blank and `notes` counts/order remain correct. Newly realized and fallback result labels stay readable. |
| Final `4af60249` | Native Tab/End reaches the last result. Shift+Tab returns to the complete visible query; Up/Down selects My notes/Other, with the selected final row fully visible at the laptop's current 150% scaling. |
| Final `4af60249` | An explicitly focused empty-result query followed by Enter leaves Command bar unchanged. `keep awake` finds one Tool result; Enter opens its settings. The panel still reports Keep awake Off, and Stop is disabled. |
| Final `4af60249` | General → Exit ends the isolated validation process. |

The query/parser and action construction are unchanged between the candidates; the final changes concern viewport sizing and string fallback. First-candidate phrase/path observations are identified separately rather than being relabeled as new final-build UI runs. Immediate post-input captures can lag the action; settled screenshots are observed before recording visual results. Automation SetValue can also require explicit physical query refocus before keyboard checks.

Narrator, touch/pen, mixed DPI/high contrast, every enabled command kind and real launcher execution were not tested in this continuation. The earlier picker, shelf drag, input/editor, clipboard ownership and target hardware limits in [verification.md](verification.md) remain open. This is not a completed port or a release.

## Local records and preservation

Passing logs are `artifacts/command-search-2026-10-19/fixed-build-x64.log`, `fixed-build-arm64.log`, `fixed-windows-checks.log` and `core.log`. `ui.log` records bounded desktop observations; initial build/test logs and `core-first-failure.log` retain the earlier evidence. These artifacts and private preservation fixtures remain ignored by Git.

[The JSON checkpoint](../validation/windows-command-search-checkpoint.json) records source identity, evidence hashes and preservation checks. The five accepted taskbar cue files, Mica configuration, runtime tool catalog and all 77 audit decisions are unchanged. Normal settings and the sign-in startup entry match their pre-test state. No package, installer, remote, public upload or release was made.

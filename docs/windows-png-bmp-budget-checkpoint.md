# PNG/BMP byte limits and resident lifecycle — October 19, 2026

Source **`ad9a5f89-267d-42e5-a728-caf112d868c3`**  continues `41a1b63c` with **133 product/backend compile inputs**. It passes **187 core / 25 physical x64 Windows checks**, x64 XAML/OCR publishing and ARM64 cross-publishing. ARM64 execution remains untested on this Dell. [JSON evidence](../validation/windows-png-bmp-budget-checkpoint.json) records file/log hashes, outputs, UI observations and limits. Vela remains an unreleased 0.2.2 development preview.

## Implementation

The existing per-image byte limit now also supports PNG and BMP. Zero retains unlimited conversion. With a positive limit, JPEG keeps its existing quality search; PNG/BMP keep their formats and can reduce dimensions while preserving aspect ratio. The form explains this before conversion. The search first measures the selected starting size, then a minimum long edge of 64 pixels (or the smaller starting edge), and tries at most twelve candidates. A fitting source is not enlarged or reduced unnecessarily. The result is a measured fit, not a guarantee of the largest possible resolution for every compression pattern.

The encoder scales the prepared bitmap, including any watermark, using Windows' [BitmapEncoder transform](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapencoder). PNG preserves alpha; BMP explicitly uses opaque pixels, matching the existing form description. Vela rechecks the actual final file size and removes its own unfinished or oversized copy. Selected originals and uniquely named previous outputs remain unchanged. Native streams/bitmaps are disposed between files and trials. Per-file failures continue to later inputs; cancellation removes the current unfinished copy and retains earlier completed files.

## Actual backend results

The new native check generates a 512×256 image with seeded noise and 128 alpha, a small image and an invalid image. It decodes retained outputs through Windows codecs.

| Format | Ordinary copy | Limit | Retained copy | Dimensions |
| --- | ---: | ---: | ---: | --- |
| PNG | 428,010 bytes | 142,670 bytes | **142,612 bytes** | **295×148**, alpha retained |
| BMP | 524,342 bytes | 174,780 bytes | **174,694 bytes** | **295×148**, opaque |

Both ordinary outputs remain byte-identical after capped conversion. Both formats also pass an impossible one-byte cap with no output, invalid/unfit/valid batch continuation (1/3), a fitting 16×8 source under 1024 bytes without enlargement, cancellation before dimension probes and after output creation, and cancellation at the next input retaining the completed copy. A capped, resized PNG retains its generated text watermark. All generated originals remain byte-identical and can be reopened exclusively. The preceding JPEG, image batch and OCR regressions still pass.

Six added core checks cover fitting/small inputs, measured selection and the twelve-attempt bound, the minimum edge, irregular size measurements, cancellation and invalid inputs/empty encoded data. Final totals are **187/187 core** and **25/25 Windows backend checks**, with no compiler warning/error diagnostics in either architecture publish.

## Native UI and lifecycle observations

Before this change, the `41a1b63c` resident candidate ran with a new isolated profile (PID 27152). Settings, its three-tool panel and Batch image tools rendered. Disabling Batch image tools through the actual Features switch closed the open idle tool and saved only shelf/imagePins as enabled. Settings → General → Exit terminated that candidate normally. The previously locked `f77132b9` preview (PID 16196) also exited normally through its Settings button; it was not force-terminated. Evidence is in `artifacts/remaining-ui-2026-10-19/`.

The final `ad9a5f89` x64 candidate (PID 18836) then ran with a separate media-only profile. Its form renders the enabled PNG size field and resolution-reduction explanation. Entering 64 KiB commits to the NumberBox value; PNG → BMP → JPEG retains 64. JPEG quality is disabled for PNG/BMP and enabled for JPEG. Scrolling reaches the actions, and Convert with no images renders the expected error. Closing the tool, reopening the resident panel, opening Settings and choosing Exit all complete normally. No resident or OCR worker remains. Accessibility text snapshots are saved in `artifacts/png-bmp-budgets-2026-10-19/form-trees.json`.

The image picker on the preceding candidate opened, but the transport rejected both the original and recovered click because the dialog belongs to **PickerHost.exe**, which its window inventory does not expose. The actual error was `point (927, 675) is over PickerHost.exe "", not target window Vela.Windows.exe "Vela"; activate the target or take a fresh screenshot before retrying`. Escape dismissed the picker, returning the form to zero selected images. Some text-only Settings/panel input also reported `coordinate input geometry is unavailable`; fresh selection, activation and screenshot-backed state allowed the intended action to succeed. These are automation limits, not successful picker-based conversions.

## Preservation and remaining priorities

Both loose outputs, `artifacts/Vela-png-bmp-x64` and `artifacts/Vela-png-bmp-arm64`, contain matching compiled-source manifests and compiled XAML resources. Both include the freshly published x64 OCR worker; ARM64 uses emulation for that worker as before. No ZIP/package, installation, normal-profile launch, remote change or publication occurred.

Normal settings remain SHA-256 **`b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`**, with no Vela sign-in Run value. The five accepted cue files and `WindowStyle.cs` still match October 18. The 31-tool catalog, settings schema and all 77 native/split/deferred scope triples are unchanged. No global clipboard operation or hardware write was performed. Historical Mac/VM evidence retains its original source identity.

The next checks remain physical outgoing/duplicate-dialog shelf cancellation and successful removal; mixed image/OCR picker-to-result flows; active batch Cancel/feature-disable/Exit; loaded pin/shelf lifecycle; and broader input, accessibility, mixed-DPI and hardware tests. The two resident Exit passes above cover idle operation, not those active-work cases. Write-denied/full-disk/cleanup failures, unusually large or oriented images, arbitrary codecs and cross-device visual quality were not established. Bounded dimension search may reject a limit or retain a smaller fit even when another untested size could fit. Additional hardware, audio automation, Bluetooth, advanced capture/indexing and localization residuals remain in the audit. This checkpoint does not complete the port.

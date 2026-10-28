# Offline OCR batch continuation — October 19, 2026

Source **`41a1b63c-43ce-4f60-bee1-79fffb206cfc`** continues `83b3ad32` with **131 verified product/backend compile inputs**. It passes **181 core / 24 Windows backend checks** on the physical x64 Dell, x64 XAML/OCR publishing and ARM64 cross-publishing. ARM64 execution is not established here. [JSON evidence](../validation/windows-ocr-batch-checkpoint.json) records manifests, artifact/log hashes and preservation checks. Vela remains an unreleased 0.2.2 development preview.

## Change

The former OCR loop stopped the batch when a selected image failed. It now returns completed/total counts and named failures, continuing with later images after expected decode, access, I/O or worker errors. The native batch form shows the result with error severity whenever failures exist. Each output uses a unique name, so earlier text files and selected originals are retained. English recognition remains offline through the existing bundled x64 Tesseract worker, with a 4096-pixel preview edge and 100-megapixel input guard.

Cancellation is checked during preparation and before/after saving text. A cancellation or exception after launching a worker stops that owned process and awaits its exit and pipe readers before temporary-input cleanup. Canceling after output creation removes the current unfinished copy; earlier completed copies stay. The UI's existing tracked-operation/Cancel wiring remains in place. See Microsoft's [WaitForExitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexitasync?view=net-10.0) and [Kill](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0) contracts. No other app process is selected or stopped.

## Actual checks

The new native regression uses copies of the generated English OCR fixture plus unreadable inputs, in a disposable test tree:

| Case | Result |
| --- | --- |
| Corrupt / valid / empty / valid batch | **2/4 completed**, two named failures; both later valid files contain the expected recognized text. |
| Existing text output and same source basenames | Three unique recognized text files remain, including the prior output; its bytes are unchanged. |
| Actual worker failure | A test-only callback corrupts Vela's prepared temporary PNG, leaving the selected original intact. The real worker exits unsuccessfully; the next image is recognized. Both captured private temporary directories and workers are gone afterward. |
| Active worker cancellation | The final run observed owned worker **PID 4964**, canceled the request and verified that no worker from the isolated test output remained and no destination copy was kept. This is cancellation after process start, not a timing claim about the recognition phase. |
| Cancel after destination creation | The current unfinished text copy is removed. |
| Cancel at the next image | Exactly one earlier completed, correctly recognized text copy remains. |
| Pre-canceled / empty / all-invalid batches | No outputs; cancellation propagates, empty counts are 0/0, and ten invalid inputs retain ten failures with an eight-detail display plus a two-more summary. |
| Original bytes and handles | All generated selected originals remain byte-identical and can be opened exclusively after processing. |

The preceding single-image OCR fixture still passes. The complete final Windows suite passes **24/24**, including the existing image/video/audio/shelf checks. Final core checks pass **181/181**. Both architecture publishes include matching compiled-source manifests and nonempty XAML resources, with no compiler warning/error or failed-test diagnostics. ARM64 reuses the exact x64 OCR worker as designed.

## Preservation and limits

New loose outputs are **`artifacts/Vela-ocr-x64`** and **`artifacts/Vela-ocr-arm64`**. The older isolated resident preview remains open; it was not force-terminated. No new full resident UI tour was performed. The OCR result binding compiles in both architectures, but **a mixed OCR batch through the native picker/form, rendered partial-failure presentation, Cancel button and full resident Exit still need end-to-end UI testing**. Backend cancellation does not establish those complete flows.

Tests cover generated English text and the listed failure/cancellation boundaries. Recognition quality across arbitrary documents, other languages, active cancellation during unusually large native preparation, output/temp permission failures and a hanging worker were not established. There is no new automatic OCR timeout. Cleanup of the two injected-worker temporary directories was measured; broader failure-to-delete behavior remains unverified. PNG/BMP byte budgets and advanced editing remain deferred.

Normal settings remain SHA-256 **`b2f9288aded3fc573c1d6f9633ddf944650b8ee6fe35ee27cd53fddef7f804b2`**; sign-in Run remains absent. The accepted five receiver files and `WindowStyle.cs` match the October 18 snapshot. The 31-tool catalog, all 77 scope triples and settings schema are unchanged. No global clipboard operation, hardware setting, normal-profile write, installation, package, source sync or publication occurred. Historical Mac/VM evidence remains separate from these new Dell results.

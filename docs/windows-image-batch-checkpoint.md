# Image batch failure isolation — October 19, 2026

Vela remains an unreleased 0.2.2 preview. Image conversion now continues after an unreadable image or an individual JPEG size-limit failure. It returns completed/total counts and named failures, and the native form uses error severity for partial or failed batches. Global invalid options still fail before processing; unsupported extensions can no longer silently produce PNG bytes with another filename suffix.

Source **`25fcb15c-e9df-4f6b-bf98-e3c3d6ee3056`** follows M4A snapshot `315d04be` and covers **127 compile inputs**. The physical x64 Dell passes **181 core and 20 Windows backend checks**, including the new compound image-batch scenario. Native x64 XAML/OCR publishing and ARM64 cross-publishing pass with zero compiler warnings/errors. ARM64 execution is untested on this laptop.

## Actual native verification

All inputs are generated local fixtures. A batch containing corrupt PNG, valid PNG, empty JPEG and another valid PNG completes **2/4**, reporting both bad filenames. The valid inputs have the same basename in separate folders; they and an earlier output produce three distinct decodable PNGs. Each preserves the requested 200×100 resize and approximately 50% alpha. Source and earlier output bytes remain identical, and all source handles can be reopened exclusively after work ends.

A separate JPEG batch puts a noisy image that cannot fit 4 KiB before a tiny image that can. It completes **1/2**, reports the first size failure, and retains only the later **631-byte** JPEG under the 4,096-byte cap. Existing JPEG quality/cap, watermark, OCR, pin-preview and media checks still pass.

Cancellation after creating the current output removes that partial file. Cancellation when starting the next input retains the already completed decodable copy and produces no next-file output. Pre-canceled work creates nothing. An unsupported `.gif` output is rejected before writing instead of creating a mislabeled PNG.

Successful image counts advance after native image/stream disposal completes. Expected per-file read/codec/size failures remove only their owned incomplete output and permit the loop to continue. Cleanup failures still surface instead of being hidden. Cancellation propagates as cancellation. The result retains all failure records; the compact UI message displays the first eight details, consistent with video batches.

## Rendered observations and limits

The final x64 form is exercised in a fresh isolated profile containing only Quick panel and Batch image tools, at unchanged 150% scaling. PNG defaults, disabled JPEG-only controls, scrolling and the empty-batch validation path remain available. No picker is opened. Native backend conversion/count/error records pass, and source review confirms that the form binds `HasFailures` to error severity. A rendered completed mixed batch remains unverified because PickerHost is not targetable through the current automation transport.

This change covers image conversion. OCR retains its existing separate batch behavior; PNG/BMP size budgets, broader codecs, huge images, write-denied/full-disk recovery, arbitrary cleanup failures, Narrator/high contrast and mixed DPI remain unverified or unfinished. It does not establish a completed port.

## Evidence and preservation

`artifacts/image-batch-2026-10-19/` contains preliminary/final backend logs, both build logs, private validation fixtures and bounded form observations. [The JSON checkpoint](../validation/windows-image-batch-checkpoint.json) records source/output/log hashes and actual limits. The source manifest records the new native test input. Normal settings/startup, 31-tool catalog, accepted five-file cue, Mica and all 77 audit decisions remain unchanged. No package, installer, remote or public release was created.

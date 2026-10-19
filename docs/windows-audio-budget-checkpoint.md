# M4A size limits — October 19, 2026

This records M4A snapshot `315d04be`; the later [image-batch continuation](windows-image-batch-checkpoint.md) preserves it and records the newer source.

Vela remains an unreleased 0.2.2 preview. Batch video tools now applies the optional per-file size limit to **M4A audio extraction as well as MP4 conversion**. Zero keeps the existing high-quality audio preset; positive whole-number limits accept 1–2048 MiB. GIF retains its separate fixed safeguards. The catalog remains at 31 implemented tools, and native Windows exclusions are unchanged.

Source **`315d04be-d4d7-425e-9678-99668e317542`** follows developer-feed snapshot `e509026e` and covers **126 compile inputs**. The physical x64 Dell passes **181 core checks** (eight new audio checks) and **19 Windows backend checks** (one new native audio scenario). Native x64 XAML/OCR publishing and ARM64 cross-publishing pass with zero compiler warnings/errors. ARM64 execution is untested on this laptop.

## Behavior

The AAC planner reserves container overhead using the detected duration and chooses supported mono/stereo output rates without raising the output preset. MP4 uses the same shared rate selection as before. A requested M4A cap forces re-encoding and is enforced against the completed file's actual bytes. Oversized attempts are deleted before a lower-rate retry; at most three encoding passes are permitted, with early termination at the minimum supported rate. A cap may fail even if an external encoder could fit it. The UI explains this quality/minimum limitation.

Missing audio is reported explicitly for both ordinary and capped extraction. Per-file failures preserve the rest of the batch, successful previous copies and original media. Output names remain unique. Cancellation before work, after preparation and during active encoding removes partial copies through the existing tracked operation lifecycle. No audio is played or recorded.

## Actual native results

The test generates a 12-second stereo 48 kHz PCM tone/noise fixture and a small video carrying that audio; no user media, microphone or playback device is used.

| Check | Actual result |
| --- | --- |
| Ordinary M4A extraction from generated video | 291,429 bytes. |
| Capped extraction | Limit 218,571 bytes; retained 195,442 bytes, one observed encoding pass. |
| Encoded stream | 128,000 bps, stereo, 48,000 Hz; duration 12.011 seconds; no video stream. |
| Decode | Native AAC-to-WAV transcoding succeeds; RIFF/WAVE data contains non-silent PCM samples. |
| PCM input | Separate backend conversion also fits the M4A cap. The UI picker remains video-only. |
| Impossible cap | One byte reports a named M4A failure with no new output. |
| Cancellation | Pre-canceled work, cancellation at the prepared Starting callback and actual encoder-progress cancellation all leave no partial copies. |
| Missing audio | Both uncapped and capped extraction reject a generated silent video with an explicit missing-stream error. |
| Mixed batch | Invalid media plus silent video plus valid source gives 1/3 completed, two named failures and one new uniquely named M4A. |
| Preservation | Original WAV/video/silent-video bytes and earlier output bytes remain identical. |
| MP4/GIF regressions | All earlier native media scenarios still pass, including MP4 actual-byte limits, strict-cap failure, silent video and progress cancellation. |

Core checks cover supported rates, overhead/duration estimates, impossible limits, measured-size rate reduction, stopping at minimum, numeric extremes and invalid inputs. The initial native run passed 19 scenarios but reported a nullable compiler warning; the final source fixes the annotation and passes without warnings. Native M4A fixtures observed one pass; lower-rate retry choices are verified separately by core tests, without claiming a native multi-pass M4A observation.

## Rendered checks and limits

The final x64 app runs in a fresh isolated profile with Quick panel and Batch video tools only, at unchanged 150% scaling. The field starts at zero, enables for M4A, commits 2 MiB with Tab, disables while preserving 2 for GIF, and re-enables with 2 when returning to M4A. GIF controls collapse again. An empty M4A conversion shows a red inline selection error. MP4 retains the same shared value and controls.

No picker was opened: the existing PickerHost automation limit remains. Picker-to-conversion execution and rendered mixed-batch error styling are therefore unverified. Large/long media, arbitrary codecs, DRM, multichannel source downmixing, subjective audio quality, Narrator/high contrast, mixed DPI and hardware-encoder variations remain untested. The app does not promise an exact target size, lossless extraction or preserved source channel layouts. PNG/BMP byte limits and other residuals remain open in [the audit](windows-feature-audit.md).

## Evidence and preservation

`artifacts/audio-budgets-2026-10-19/` contains core/native build/backend logs, bounded UI observations and private validation settings/backups. [The JSON checkpoint](../validation/windows-audio-budget-checkpoint.json) records source/output/log fingerprints and actual results. Normal settings/startup, the accepted five-file cue, Mica and all 77 audit decisions remain unchanged. No package, installer, remote or public release was made.

Implementation references: [Microsoft AAC encoder rates](https://learn.microsoft.com/en-us/windows/win32/medfound/aac-encoder), [CreateM4a](https://learn.microsoft.com/en-us/uwp/api/windows.media.mediaproperties.mediaencodingprofile.createm4a?view=winrt-26100), and [music duration metadata](https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.musicproperties.duration?view=winrt-26100).

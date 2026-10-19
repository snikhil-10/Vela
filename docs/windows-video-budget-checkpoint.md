# MP4 size budgets — October 19, 2026

This checkpoint records MP4 snapshot `b627a158`; the later [developer-feed continuation](windows-developer-feeds-checkpoint.md) preserves it and records the newer build.

Vela remains an unreleased 0.2.2 preview. This continuation implements the retained batch-video size-limit workflow while preserving Windows-native exclusions, normal settings, WinUI/Mica and the accepted taskbar cue.

## Behavior

Batch video tools accepts an optional whole-number **MP4 limit from 1 to 2048 MiB per output file**; zero keeps the normal quality preset. The limit is disabled for M4A and GIF. Compression keeps the selected output resolution and may reduce picture/sound quality. It does not trim the source or choose a lower resolution automatically.

The planner reserves container overhead and audio space using the source duration. Silent inputs keep no audio stream. Audio rates use the documented Windows mono/stereo AAC choices, 96/128/160/192 kbps ([Microsoft AAC encoder reference](https://learn.microsoft.com/en-us/windows/win32/medfound/aac-encoder)). MP4 budgets force [re-encoding](https://learn.microsoft.com/en-us/uwp/api/windows.media.transcoding.mediatranscoder.alwaysreencode) and measure the completed file rather than treating bitrate estimates as a guarantee. An oversized attempt is removed before a lower-rate retry. At most three passes are allowed, with an earlier stop when rates cannot be lowered. A copy is retained only if its actual bytes fit.

Files that cannot fit report a failure; other files in the batch continue. Successful earlier copies remain. Output names are unique and originals are unchanged. Cancellation checks cover preparation, native encoding and completion; partial output is removed. MP4/M4A results now carry success/failure counts so partial/failed batches use error styling instead of a green success notice. No settings schema changed.

## Actual automated results

Source **`b627a158-faf3-4be6-8a31-2cdc4f430db6`**, **120** verified inputs, passes **157 core and 18 Windows backend checks** on the physical x64 Dell. Native x64 XAML/OCR publishing and ARM64 cross-publishing pass with no reported compiler warnings/errors. ARM64 runtime is untested here.

Eight new core checks cover preset retention, duration/audio/overhead accounting, silent inputs, impossible budgets, measured retry reduction/floors, integer range boundaries and invalid inputs. The added Windows check is a compound native encoder regression using eight generated noise images and a four-second generated tone. No user media is read or recorded.

| Native case | Observed result |
| --- | --- |
| Regular preset vs capped copy | Baseline **4,653,127 bytes**; limit **3,489,845 bytes**; retained copy **3,278,934 bytes**. A video frame decodes, duration remains about four seconds, and audio extraction from the capped copy succeeds. |
| Strict 128 KiB cap | Two encoding passes; zero retained copies; explicit size-limit failure. No oversized/incomplete output remains. |
| Impossible one-byte cap | Rejected before keeping an output. |
| Audio-only cap | Explicitly rejected rather than silently ignored. |
| Broken file then valid video | One success of two, one named failure, with a new unique output and earlier copies preserved. |
| Cancellation after preparation | Partial output removed. |
| Cancellation during native progress | Progress callback exercises active encoding cancellation; partial output removed. |
| Silent four-second video, 64 KiB cap | Retained output fits, and source/output profiles both have no audio stream. |
| Original preservation | Original source bytes remain identical. |

## Rendered observations and limits

The final x64 source runs with `artifacts/video-budget-2026-10-19/profile`, selecting only Quick panel and Batch video tools. At the laptop's current 150% scaling, the field defaults to zero, commits 2 MiB, disables for M4A/GIF while retaining its value, and re-enables with that value on returning to MP4. GIF trim/rate/loop controls and Convert/Cancel remain reachable by scrolling. An empty MP4 batch produces the red inline selection error. General → Exit ends the isolated instance.

No picker was opened in this continuation: PickerHost remains unavailable as a Computer Use target. Full picker-to-conversion UI execution and rendered partial-completion styling are therefore not established. Native backend conversion and typed failure counts are verified separately. Narrator, mixed DPI/high contrast, arbitrary codecs/DRM, long/4K videos and all hardware encoders remain untested. Quality is not guaranteed at a small limit; a file may fail even when an external encoder could fit it. M4A target-size limits, PNG/BMP limits and advanced editing remain deferred. Other open port work stays in [the feature audit](windows-feature-audit.md).

## Evidence and preservation

`artifacts/video-budget-2026-10-19/` contains `build-x64.log`, `build-arm64.log`, `windows-final.log`, core results, earlier exploratory native logs and `ui.log`. Private settings/startup preservation fixtures and the validation profile remain ignored by Git. [The JSON checkpoint](../validation/windows-video-budget-checkpoint.json) records source/output/evidence hashes and preservation checks.

Normal settings and sign-in startup match their pre-test state. All five accepted cue files, Mica configuration, the tool catalog and all 77 audit decisions are unchanged. No Vela process, conversion or input lock is left active. No package, installer, remote, public upload or release was made. The preview is not a completed port.

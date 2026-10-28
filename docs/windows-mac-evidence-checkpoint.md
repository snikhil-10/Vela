# Collected Mac/VM evidence — October 19, 2026

The user supplied **14 files** in `C:/Users/Admin/Downloads/Wormhole okorPL`. Both native ZIPs belong to the requested historical snapshot **`3b791fb4-ff70-4ab1-a947-0a94388639dd`**, with **108 compile inputs**. Host archive validation passes. At collection time the newer Dell source was **`83b3ad32`** and was not replaced, rebuilt or run as part of that collection. Subsequent feature work is recorded in the latest verification entries. [Machine-readable results and hashes](../validation/windows-mac-evidence-checkpoint.json) distinguish fresh host inspection from historical guest execution.

## Fresh host checks

| Evidence | Result |
| --- | --- |
| Collector inventory | All 13 listed payload files match their recorded SHA-256 and size. The 14th file is the inventory itself. Original VM paths/timestamps are collector claims; the Dell cannot independently inspect those originals. |
| ARM64 ZIP | **133,980,626 bytes**, 731 entries; complete CRC read passes. SHA-256 `0899f4db2c87ceb1cdc7e57776da27b53cccf7bcfc950368be73064f2a1aafd5` also matches the VM build log. |
| x64 ZIP | **136,717,879 bytes**, 735 entries; complete CRC read passes. SHA-256 `e56d706e528b794dae5e1f9599c0fe6b9d6aaf4d3a74df94b011487fb4617ccd` also matches the VM build log. |
| Packaged and separate source manifests | Both packaged manifests are byte-identical to their respective supplied copies. All three separate manifests and both packaged manifests match the pinned snapshot's 108 paths/hashes. |
| Original October 18 source | All 108 inputs independently hash-match the original transfer ZIP. Its CRC passes; all **146 CONTENTS.sha256** and **137 SOURCE-FILES.sha256** entries pass. |
| Executable architectures | Main app PE machine is ARM64 **AA64** / x64 **8664** respectively. Both OCR workers are x64 **8664**, as designed. Both archives include nonempty XAML PRI and .NET 10 runtime configuration. |
| Embedded application manifests | Vela-authored identity, execution level, compatibility, DPI and long-path sections match the original source. All **29 registered runtime DLLs** are present in each archive and byte-match the corresponding pinned-SDK Dell runtime copies. The complete merged manifests match the corresponding Dell builds. |
| Archive names/content | No traversal/absolute paths, duplicate Windows-case paths or known profile/credential filenames were found. Files were read in place; no archive binaries or installers were executed. |

The application manifest comparison accounts for the self-contained SDK's registration-free WinRT/COM declarations and section ordering. The executable manifest is larger than the authored fragment; it is not expected to be byte-identical to that fragment. The ZIP spells `Microsoft.ui.xaml.dll` differently from its manifest's `Microsoft.UI.Xaml.dll`, which resolves to the same unique file on Windows. The verifier uses Windows-case matching and validates both WinRT and COM registrations. See Microsoft's [PE format](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format) and [self-contained Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps) documentation. Initial stricter comparisons were corrected for those packaging semantics; no historical binary was modified.

## Historical execution evidence now available

Both architecture build logs name `3b791fb4`, contain **130 core PASS lines / zero failures**, and record native publishing plus the clipboard-publication fixture and restored text/HTML consumer pass. Both backend logs contain **12 PASS lines / zero failures**. There are no failed-test or compiler warning/error diagnostics in those supplied logs. These are recorded VM results, **not tests rerun on this Dell**; historical x64 execution was under ARM64 emulation.

The separate `immediate-clipboard-probe.log` records a pass but does not itself identify architecture/snapshot. It is retained without assigning it to an architecture; the per-architecture build logs provide the specifically associated publication results.

Fresh base64/UTF-8 decoding independently matches the expected generated desktop payloads:

- `snippet-3b791fb4.log`: exact 111-character immediate/Space/Tab/Return, longest-suffix, multiline, Unicode and clipboard-placeholder result.
- `rapid-3b791fb4.log`: exactly eight `SPACE x` lines, including every following x and Return, 72 characters.
- `plain-3b791fb4.log`: exactly `Vela plain paste test`, 21 characters.

Each log also records text/HTML clipboard preservation. No current clipboard payload was read or replaced by this inspection. Matching packaged input manifests and logs is evidence of the recorded build, not an independently reproduced or signed build.

## Remaining collection limit and preservation

The **original external `.sha256` sidecar for `Vela-Windows-transfer-2026-10-18.zip` was not received**. The locally available ZIP hashes now to `12bdba265c172db9bcbe9b0b1896ede3ab21a974e00923adf4ba4a8cdea1be1f`, but that new calculation does not replace the missing original sidecar. Its internal checksums pass; comparison against the original external checksum remains unavailable. The collection is therefore not labelled complete in every requested respect.

Twelve small supplied log/manifest/inventory files are preserved byte-for-byte under [historical-vm-3b791fb4](../validation/historical-vm-3b791fb4/guest-files.json). The ZIPs remain unchanged in the user's supplied folder. The original October 18 [VM checkpoint](../validation/windows-vm-checkpoint.json) retains its dated transfer-state fields and links to this follow-up. The repeatable read-only verifier is [verify-mac-evidence.py](../tools/verify-mac-evidence.py); its command and preservation hashes are in the JSON report.

Normal settings, the current Dell compile inputs, Mica and the accepted five-file taskbar cue remain unchanged. No old executable was launched, no installation/source sync occurred, and nothing was published. This closes the native archive/log collection and CRC/PE/manifest validation gap; the missing external sidecar and broader physical functionality/unfinished-port work remain separate limits.

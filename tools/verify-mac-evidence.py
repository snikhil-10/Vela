"""Read-only validation of the collected October 18 Vela VM evidence.

Reads ZIPs without extracting or executing them. Writes only the requested report.
PE layout follows https://learn.microsoft.com/en-us/windows/win32/debug/pe-format.
"""
import argparse
import base64
import datetime
import hashlib
import json
import os
import pathlib
import re
import struct
import sys
import uuid
import zipfile
import xml.etree.ElementTree as ET

SNAPSHOT = "3b791fb4-ff70-4ab1-a947-0a94388639dd"
REPO = pathlib.Path(__file__).resolve().parents[1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def text(path):
    data = path.read_bytes()
    return data.decode("utf-16" if data.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig")


def json_read(path):
    return json.loads(text(path))


def xml_tree(element):
    return (element.tag, tuple(sorted(element.attrib.items())), (element.text or "").strip(),
            tuple(xml_tree(child) for child in element))


def pe(data, read_manifest=False):
    if data[:2] != b"MZ":
        raise ValueError("Missing DOS header")
    head = struct.unpack_from("<I", data, 0x3C)[0]
    if data[head:head + 4] != b"PE\0\0":
        raise ValueError("Missing PE signature")
    machine, sections = struct.unpack_from("<HH", data, head + 4)
    if not 0 < sections <= 96:
        raise ValueError("Invalid PE section count")
    optional_size = struct.unpack_from("<H", data, head + 20)[0]
    optional = head + 24
    magic = struct.unpack_from("<H", data, optional)[0]
    result = {"machine": f"{machine:04X}", "sha256": sha(data), "bytes": len(data)}
    if not read_manifest:
        return result
    directory = optional + {0x10B: 96, 0x20B: 112}[magic]
    resource_rva, resource_size = struct.unpack_from("<II", data, directory + 16)
    section_table = optional + optional_size

    def rva_offset(rva, size=1):
        for index in range(sections):
            address = section_table + 40 * index
            virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from("<IIII", data, address + 8)
            delta = rva - virtual_address
            if 0 <= delta and delta + size <= raw_size:
                offset = raw_offset + delta
                if offset + size <= len(data):
                    return offset
        raise ValueError("Resource RVA outside file-backed section")

    if not resource_rva or not resource_size:
        raise ValueError("No executable resource directory")
    resource_base = rva_offset(resource_rva)

    def entries(relative):
        address = resource_base + relative
        named, ids = struct.unpack_from("<HH", data, address + 12)
        if named + ids > 4096 or relative + 16 + 8 * (named + ids) > resource_size:
            raise ValueError("Invalid resource directory")
        return [struct.unpack_from("<II", data, address + 16 + 8 * i) for i in range(named + ids)]

    manifests = []

    def descend(value, path):
        if len(path) > 4:
            raise ValueError("Excessive resource depth")
        if value & 0x80000000:
            for name, child in entries(value & 0x7FFFFFFF):
                descend(child, path + [name])
        else:
            if value + 16 > resource_size:
                raise ValueError("Invalid resource data entry")
            rva, length, codepage, reserved = struct.unpack_from("<IIII", data, resource_base + value)
            if length > 2 * 1024 * 1024:
                raise ValueError("Oversized application manifest")
            offset = rva_offset(rva, length)
            payload = data[offset:offset + length]
            root = ET.fromstring(payload)
            manifests.append({"resourceIds": path, "sha256": sha(payload), "bytes": length,
                              "xml": ET.tostring(root, encoding="unicode"), "root": root})

    for name, value in entries(0):
        if name == 24:  # RT_MANIFEST
            descend(value, [24])
    if not manifests:
        raise ValueError("No embedded application manifest")
    result["manifests"] = manifests
    return result


def manifest_check(manifest, reference):
    actual = manifest.get("files", {})
    expected = reference["files"]
    return {"snapshot": manifest.get("snapshotId"), "compileInputs": len(actual),
            "snapshotMatches": manifest.get("snapshotId") == SNAPSHOT,
            "missingInputs": sorted(expected.keys() - actual.keys()),
            "extraInputs": sorted(actual.keys() - expected.keys()),
            "hashMismatches": sorted(key for key in actual.keys() & expected.keys()
                                     if actual[key].lower() != expected[key].lower())}


def valid_manifest(check):
    return (check["snapshotMatches"] and check["compileInputs"] == 108 and
            not any(check[key] for key in ("missingInputs", "extraInputs", "hashMismatches")))


def valid_registration(node):
    if node.tag == "{urn:schemas-microsoft-com:winrt.v1}activatableClass":
        return bool(node.attrib.get("name")) and node.attrib.get("threadingModel") in ("both", "sta", "mta")
    guid_key = {"{urn:schemas-microsoft-com:asm.v3}comClass": "clsid",
                "{urn:schemas-microsoft-com:asm.v3}comInterfaceProxyStub": "iid"}.get(node.tag)
    if guid_key is None:
        return False
    try:
        uuid.UUID(node.attrib[guid_key])
        return guid_key == "clsid" or bool(node.attrib.get("name"))
    except (ValueError, KeyError):
        return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("incoming", type=pathlib.Path)
    parser.add_argument("--original-transfer", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    incoming = args.incoming.resolve()
    reference = json_read(REPO / "validation/native-build-source-manifest.json")
    current = json_read(REPO / "build-source-manifest.json")
    current_hash = file_sha(REPO / "build-source-manifest.json")
    settings = pathlib.Path(os.environ.get("LOCALAPPDATA", str(REPO / "not-installed"))) / "Vela/settings.json"
    settings_before = file_sha(settings) if settings.is_file() else None
    expected_hashes = reference["files"]
    inventory = json_read(incoming / "guest-files.json")
    collector = {row["collectedName"]: row for row in inventory["files"]}
    report = {"schemaVersion": 1, "recordedAt": datetime.datetime.now().astimezone().isoformat(),
              "historicalSnapshot": SNAPSHOT, "currentDellSnapshot": current["snapshotId"],
              "incomingDirectory": str(incoming), "historicalBinariesExecuted": False,
              "historicalBuildOrTestRerun": False, "failures": [], "files": [],
              "standaloneManifests": {}, "archives": {}, "logs": {},
              "verifier": {"path": "tools/verify-mac-evidence.py", "sha256": file_sha(pathlib.Path(__file__)),
                           "pythonVersion": sys.version.split()[0],
                           "command": "python tools/verify-mac-evidence.py <incoming-folder> --original-transfer <October-18-source.zip> --output validation/windows-mac-evidence-checkpoint.json"}}

    def check(condition, message):
        if not condition:
            report["failures"].append(message)

    for path in sorted(incoming.iterdir()):
        if not path.is_file():
            continue
        row = collector.get(path.name)
        digest = file_sha(path)
        matched = None if row is None else (digest == row["sha256"].lower() and path.stat().st_size == row["bytes"])
        check(matched is not False, "Collected hash/size mismatch: " + path.name)
        report["files"].append({"name": path.name, "bytes": path.stat().st_size, "sha256": digest,
                                 "matchesCollectorHashAndSize": matched,
                                 "originalLocationReportedByCollector": None if row is None else row["originalLocation"]})
    report["collectorInventory"] = {"expectedSnapshotMatches": inventory["expectedSnapshot"] == SNAPSHOT,
                                    "listedFiles": len(collector), "missingListedFiles": sorted(set(collector) - {p.name for p in incoming.iterdir() if p.is_file()}),
                                    "locationAndTimestampClaimsIndependentlyVerified": False}
    check(report["collectorInventory"]["expectedSnapshotMatches"], "Collector snapshot mismatch")
    check(not report["collectorInventory"]["missingListedFiles"], "Collector-listed files missing")

    with zipfile.ZipFile(args.original_transfer) as transfer:
        bad = transfer.testzip()
        check(bad is None, "Original transfer ZIP CRC failure: " + str(bad))
        source_mismatches = [name for name, digest in expected_hashes.items()
                             if sha(transfer.read("Vela/" + name)) != digest]
        check(not source_mismatches, "October 18 original-source input mismatches")
        internal_checksums = []
        for checksum_name, prefix in (("CONTENTS.sha256", ""), ("Vela/SOURCE-FILES.sha256", "Vela/")):
            rows = transfer.read(checksum_name).decode("utf-8-sig").splitlines()
            mismatches = []
            for row in rows:
                match = re.fullmatch(r"([a-fA-F0-9]{64})  (.+)", row)
                if match is None:
                    raise ValueError("Malformed bundled checksum row: " + checksum_name)
                digest, name = match.groups()
                with transfer.open(prefix + name) as stream:
                    actual = hashlib.file_digest(stream, "sha256").hexdigest()
                if actual != digest.lower():
                    mismatches.append(name)
            internal_checksums.append({"manifest": checksum_name, "entriesChecked": len(rows), "mismatches": mismatches})
            check(not mismatches, "Bundled checksum mismatch: " + checksum_name)
        original_app_manifest = transfer.read("Vela/src/Vela.Windows/app.manifest")
        original_xml = ET.fromstring(original_app_manifest)
        report["originalTransfer"] = {"path": str(args.original_transfer), "sha256ComputedNow": file_sha(args.original_transfer),
                                      "crcPassed": bad is None, "historicalInputsChecked": len(expected_hashes),
                                      "historicalSourceMismatches": source_mismatches,
                                      "bundledChecksums": internal_checksums,
                                      "externalOriginalSha256SidecarReceived": False,
                                      "newChecksumIsNotOriginalSidecar": True}

    for name in ("build-source-manifest.json", "arm64-compiled-source-manifest.json", "x64-compiled-source-manifest.json"):
        result = manifest_check(json_read(incoming / name), reference)
        report["standaloneManifests"][name] = result
        check(valid_manifest(result), "Standalone historical manifest mismatch: " + name)

    for arch, machine in (("arm64", "AA64"), ("x64", "8664")):
        path = incoming / ("Vela-" + arch + ".zip")
        with zipfile.ZipFile(path) as archive:
            entries = archive.infolist()
            total = sum(entry.file_size for entry in entries)
            if total > 2 * 1024 ** 3 or len(entries) > 50000:
                raise ValueError("Archive exceeds expected inspection bounds")
            bad = archive.testzip()
            check(bad is None, "Native archive CRC failure: " + str(path))
            names = [entry.filename.replace("\\", "/") for entry in entries]
            windows_names = {name.casefold(): name for name in names}
            unsafe = [name for name in names if name.startswith("/") or re.match(r"^[A-Za-z]:", name) or ".." in pathlib.PurePosixPath(name).parts]
            duplicate = sorted({name for name in names if sum(other.casefold() == name.casefold() for other in names) > 1})
            check(not unsafe and not duplicate, "Unsafe or ambiguous archive paths: " + arch)
            prefix = "Vela-" + arch + "/"
            manifest_bytes = archive.read(prefix + "compiled-source-manifest.json")
            snapshot_check = manifest_check(json.loads(manifest_bytes.decode("utf-8-sig")), reference)
            check(valid_manifest(snapshot_check), "Packaged source manifest mismatch: " + arch)
            external_equal = manifest_bytes == (incoming / (arch + "-compiled-source-manifest.json")).read_bytes()
            check(external_equal, "External and packaged manifest bytes differ: " + arch)
            app = pe(archive.read(prefix + "Vela.Windows.exe"), True)
            check(app["machine"] == machine, "Wrong application PE architecture: " + arch)
            for embedded in app["manifests"]:
                root = embedded.pop("root")
                # Self-contained App SDK publishing merges registration-free WinRT
                # declarations into the authored manifest and can reorder sections.
                embedded["wholeXmlMatchesUnmergedSource"] = xml_tree(root) == xml_tree(original_xml)
                sections = {child.tag: len([actual for actual in root if actual.tag == child.tag]) == 1
                            and any(xml_tree(actual) == xml_tree(child) for actual in root)
                            for child in original_xml}
                embedded["historicalAuthoredSectionsMatch"] = sections
                check(root.tag == original_xml.tag and root.attrib == original_xml.attrib and all(sections.values()),
                      "Authored application manifest section differs: " + arch)
                extra = [child for child in root if child.tag not in sections]
                valid_extra = all(child.tag == "{urn:schemas-microsoft-com:asm.v3}file" for child in extra)
                check(valid_extra, "Unexpected non-registration manifest additions: " + arch)
                registrations = []
                current_output = REPO / ("artifacts/Vela-shelf-search-" + arch)
                for child in extra:
                    filename = child.attrib.get("name", "")
                    stored_name = windows_names.get((prefix + filename).casefold())
                    included = bool(filename) and "/" not in filename and "\\" not in filename and stored_name is not None
                    check(included, "Missing/unsafe registered runtime file: " + arch + "/" + filename)
                    declarations_valid = all(valid_registration(node) for node in child)
                    check(declarations_valid, "Unexpected runtime registration child: " + filename)
                    payload_hash = sha(archive.read(stored_name)) if included else None
                    current_dll = current_output / filename
                    registrations.append({"file": filename, "storedEntry": stored_name, "includedInArchive": included,
                                          "declarations": len(child), "sha256": payload_hash,
                                          "matchesSameSdkDellRuntimeCopy": None if not current_dll.exists() else file_sha(current_dll) == payload_hash})
                embedded["sdkRuntimeRegistrations"] = registrations
                current_exe = current_output / "Vela.Windows.exe"
                if current_exe.exists():
                    current_root = pe(current_exe.read_bytes(), True)["manifests"][0]["root"]
                    embedded["matchesCurrentSameSdkMergedManifest"] = xml_tree(root) == xml_tree(current_root)
            worker = pe(archive.read(prefix + "Ocr/Vela.OcrWorker.exe"))
            check(worker["machine"] == "8664", "OCR worker is not x64: " + arch)
            pri = archive.getinfo(prefix + "Vela.Windows.pri")
            check(pri.file_size > 0, "Missing XAML PRI: " + arch)
            config = json.loads(archive.read(prefix + "Vela.Windows.runtimeconfig.json"))
            profile_entries = [name for name in names if pathlib.PurePosixPath(name).name.lower() in ("settings.json", "auth.json", ".env")]
            check(not profile_entries, "Unexpected profile/credential filename in archive: " + arch)
            report["archives"][arch] = {"name": path.name, "entries": len(entries), "uncompressedBytes": total,
                                         "crcPassed": bad is None, "unsafePaths": unsafe, "duplicateWindowsPaths": duplicate,
                                         "packagedManifest": snapshot_check, "packagedAndExternalManifestBytesMatch": external_equal,
                                         "app": app, "ocrWorker": worker, "xamlPriBytes": pri.file_size,
                                         "runtimeTfm": config["runtimeOptions"]["tfm"], "unexpectedProfileOrCredentialEntries": profile_entries}
        build = text(incoming / f"offline-{arch}-build.log")
        backend = text(incoming / f"offline-{arch}-checks.log")
        summary = re.search(r"^(\d+) passed; (\d+) failed\.", build, re.M)
        native = re.search(r"^(\d+) Windows integration checks passed; (\d+) failed\.", backend, re.M)
        core_counts = None if summary is None else list(map(int, summary.groups()))
        backend_counts = None if native is None else list(map(int, native.groups()))
        snapshot_present = "Verified build snapshot " + SNAPSHOT in build
        publication = "PASS Immediate Unicode clipboard publication" in build and "PASS Fresh consumer reads restored text and HTML." in build
        declared_hashes = re.findall(r"^Hash\s*:\s*([a-fA-F0-9]{64})", build, re.M)
        diagnostics = [line for line in (build + "\n" + backend).splitlines()
                       if re.match(r"^FAIL\b", line) or re.search(r"\b(?:error|warning)\s+[A-Z]+\d+\b", line, re.I)]
        result = {"snapshotNamedInBuild": snapshot_present, "corePassedFailed": core_counts,
                  "backendPassedFailed": backend_counts, "corePassLinesBeforeSummary": len(re.findall(r"^PASS ", build[:summary.start()] if summary else build, re.M)),
                  "backendPassLines": len(re.findall(r"^PASS ", backend, re.M)), "publicationAndRestoredConsumerPassRecordedInBuild": publication,
                  "buildLogArchiveHashMatches": file_sha(path) in [value.lower() for value in declared_hashes],
                  "failureOrCompilerDiagnostics": diagnostics}
        report["logs"][arch] = result
        check(snapshot_present and core_counts == [130, 0] and backend_counts == [12, 0] and publication and not diagnostics,
              "Historical build/check log expectations failed: " + arch)
        check(result["buildLogArchiveHashMatches"], "Native archive differs from recorded VM build hash: " + arch)

    mixed = ("IMMEDIATE\r\nSPACE x\r\nTAB\tx\r\nRETURN\r\nx\r\nFirst line\r\nSecond line x\r\n"
             "Mixed CASE ✓\r\nこんにちは x\r\nVela plain paste test x")
    desktop = {}
    for name, expected in (("snippet", mixed), ("rapid", "SPACE x\r\n" * 8), ("plain", "Vela plain paste test")):
        content = text(incoming / f"{name}-3b791fb4.log")
        encoded = re.findall(r"^ACTUAL_UTF8_BASE64 (\S+)", content, re.M)
        actual = base64.b64decode(encoded[0], validate=True).decode("utf-8") if len(encoded) == 1 else None
        exact = actual == expected
        retained = "PASS Original text and HTML clipboard formats are preserved." in content
        desktop[name] = {"exactUtf8PayloadMatchesExpected": exact, "characters": None if actual is None else len(actual),
                         "clipboardFormatPassRecorded": retained, "failureLines": re.findall(r"^FAIL.*", content, re.M)}
        check(exact and retained and not desktop[name]["failureLines"], "Desktop fixture mismatch: " + name)
    report["logs"]["desktop"] = desktop
    probe = text(incoming / "immediate-clipboard-probe.log")
    report["logs"]["standalonePublicationProbe"] = {"passRecorded": "PASS Immediate Unicode clipboard publication" in probe,
                                                     "snapshotOrArchitectureExplicitlyNamed": SNAPSHOT in probe or bool(re.search(r"\b(?:arm64|x64)\b", probe)),
                                                     "notUsedToInferArchitecture": True}
    report["missingOptionalEvidence"] = ["Original Vela-Windows-transfer-2026-10-18.zip.sha256 sidecar was not received; no replacement generated."]
    report["interpretation"] = ["Host independently checked bytes/CRC/PE/resources and decoded generated desktop output.",
                                "Execution passes are recorded in historical guest logs; no VM or historical binary rerun occurred on this Dell.",
                                "x64 historical execution was ARM64 emulation, per the original checkpoint; it is not physical x64 evidence.",
                                "Packaged manifests describe recorded inputs; matching them does not independently prove a reproducible build.",
                                "Original transfer sidecar authenticity verification remains unavailable."]
    check(file_sha(REPO / "build-source-manifest.json") == current_hash, "Current Dell source manifest changed during inspection")
    report["currentDellManifestUnchanged"] = file_sha(REPO / "build-source-manifest.json") == current_hash
    input_mismatches = [name for name, digest in current["files"].items() if file_sha(REPO / name) != digest]
    check(not input_mismatches, "Current Dell compile-input hashes differ")
    settings_after = file_sha(settings) if settings.is_file() else None
    check(settings_before == settings_after, "Normal settings changed during inspection")
    protected = ["src/Vela.Windows/Services/ShelfDropService.cs", "src/Vela.Windows/UI/ShelfDropWindow.cs",
                 "src/Vela.Windows/UI/ShelfDropSurface.xaml", "src/Vela.Windows/UI/ShelfDropSurface.xaml.cs",
                 "src/Vela.Windows/UI/ShelfTargetBackdrop.cs", "src/Vela.Windows/UI/WindowStyle.cs"]
    protected_results = [{"path": name, "sha256": file_sha(REPO / name),
                          "matchesOctober18": file_sha(REPO / name) == reference["files"][name]} for name in protected]
    check(all(row["matchesOctober18"] for row in protected_results), "Accepted cue/Mica differs from historical reference")
    report["preservation"] = {"currentDellManifestSha256": current_hash, "dellCompileInputsChecked": len(current["files"]),
                              "currentDellInputMismatches": input_mismatches, "normalSettingsSha256Before": settings_before,
                              "normalSettingsSha256After": settings_after, "normalSettingsUnchanged": settings_before == settings_after,
                              "acceptedCueAndMica": protected_results}
    preserved = []
    for row in report["files"]:
        if row["name"].endswith(".zip"):
            continue
        copy = REPO / "validation/historical-vm-3b791fb4" / row["name"]
        matches = copy.is_file() and file_sha(copy) == row["sha256"]
        check(matches, "Preserved evidence copy missing or changed: " + row["name"])
        preserved.append({"path": copy.relative_to(REPO).as_posix(), "matchesIncomingBytes": matches, "sha256": row["sha256"]})
    report["preservedSmallEvidence"] = preserved
    report["historicalArchiveCollectionChecksPassed"] = not report["failures"]
    report["entireRequestedCollectionComplete"] = not report["failures"] and not report["missingOptionalEvidence"]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"archivesPassed": report["historicalArchiveCollectionChecksPassed"], "failures": report["failures"],
                      "files": len(report["files"]), "missing": report["missingOptionalEvidence"]}, ensure_ascii=False))
    return 1 if report["failures"] else 0


if __name__ == "__main__":
    raise SystemExit(main())

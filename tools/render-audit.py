#!/usr/bin/env python3
"""Regenerate the inventory and supplemental statuses from feature-audit.json."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
data = json.loads((root / "docs/feature-audit.json").read_text())
path = root / "docs/windows-feature-audit.md"
text = path.read_text()
before, remainder = text.split("## Complete upstream inventory\n", 1)
_, after = remainder.split("## Supplemental capabilities\n", 1)
rows = ["", "| Upstream feature | Decision | What ships / what remains | Evidence |", "| --- | --- | --- | --- |"]
decisions = {"exclude-native": "Exclude native", "exclude-platform": "Omit platform-specific", "exclude-user": "Omit Dynamic Island", "retain": "Retain", "split": "Split native / extra"}
for feature in data["features"]:
    links = ", ".join(f"[{name}]({data['sources'][name]})" for name in feature["sources"])
    summary = f"{feature['nativeEquivalent']} {feature['portScope']} **{feature['status']}**"
    rows.append(f"| `{feature['id']}` | {decisions[feature['decision']]} | {summary} | {links} |")
_, design = after.split("## Remaining work\n", 1)
supplemental = "\n" + "\n".join(f"- **{f['id']}** — {f['status']}" for f in data["supplementalCapabilities"]) + "\n\n"
path.write_text(before + "## Complete upstream inventory\n" + "\n".join(rows) + "\n\n## Supplemental capabilities\n" + supplemental + "## Remaining work\n" + design)

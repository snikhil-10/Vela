#!/usr/bin/env python3
"""Type-check real Windows APIs on a non-Windows host. Not a WinUI build or UI test."""
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
assets = ROOT / "src/Vela.Windows/obj/project.assets.json"
if not assets.exists():
    raise SystemExit("First run: dotnet restore src/Vela.Windows/Vela.Windows.csproj -p:Platform=ARM64 -r win-arm64")
data = json.loads(assets.read_text())
target = next(v for k, v in data["targets"].items() if "/" not in k)
nuget = Path(next(iter(data["packageFolders"])))
refs = ET.Element("Project")
items = ET.SubElement(refs, "ItemGroup")
for package, info in target.items():
    if data["libraries"].get(package, {}).get("type") != "package":
        continue
    for name in info.get("compile", {}):
        if not name.endswith(".dll") or Path(name).name in {"WinRT.Runtime.dll", "Microsoft.Windows.SDK.NET.dll"}:
            continue
        path = nuget / package.lower() / name
        ref = ET.SubElement(items, "Reference", Include=path.stem)
        ET.SubElement(ref, "HintPath").text = str(path)
obj = ROOT / "tools/obj"
obj.mkdir(exist_ok=True)
ET.ElementTree(refs).write(obj / "WindowsReferences.props", encoding="unicode")
stubs = ["// Generated ONLY for C# type checking. Never included in the application."]
x = "{http://schemas.microsoft.com/winfx/2006/xaml}"
for path in (ROOT / "src/Vela.Windows").rglob("*.xaml"):
    if any(part in {"obj", "bin"} for part in path.relative_to(ROOT / "src/Vela.Windows").parts):
        continue
    root = ET.parse(path).getroot()
    cls = root.get(x + "Class")
    if not cls:
        continue
    ns, name = cls.rsplit(".", 1)
    stubs.append(f"namespace {ns} {{ public partial class {name} {{ private void InitializeComponent() {{ }}")
    for el in root.iter():
        field = el.get(x + "Name")
        if field:
            short = el.tag.split("}")[-1]
            typ = ("Microsoft.UI.Xaml.Shapes." if short in {"Path", "Rectangle", "Ellipse"}
                   else "Microsoft.UI.Xaml.Controls.Primitives." if short == "ToggleButton"
                   else "Microsoft.UI.Xaml.Controls.") + short
            stubs.append(f"private {typ} {field} = null!;")
    stubs.append("} }")
(obj / "XamlTypeStubs.cs").write_text("\n".join(stubs))
subprocess.run(["dotnet", "build", str(ROOT / "tools/CheckWindowsTypes.csproj"), "-c", "Release", "--nologo"], check=True, cwd=ROOT)
print("C# API check passed. XAML compilation and Windows runtime validation are still required.")

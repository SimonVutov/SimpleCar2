"""Check portable repository structure; runtime tests run in Unity."""
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
errors = []
guids = {}
for asset in (root / "Assets").rglob("*"):
    if asset.suffix == ".meta":
        match = re.search(r"^guid: (\w+)$", asset.read_text(), re.MULTILINE)
        if not match:
            errors.append(f"Missing GUID: {asset.relative_to(root)}")
        elif match[1] in guids:
            errors.append(f"Duplicate GUID: {asset.relative_to(root)} and {guids[match[1]]}")
        else:
            guids[match[1]] = asset.relative_to(root)
        if not Path(str(asset)[:-5]).exists():
            errors.append(f"Orphan metadata: {asset.relative_to(root)}")
    elif not Path(str(asset) + ".meta").exists():
        errors.append(f"Missing metadata: {asset.relative_to(root)}")

for source in (root / "Assets/Scripts").glob("*.cs"):
    classes = re.findall(r"public class (\w+)\s*:\s*MonoBehaviour", source.read_text())
    if classes and source.stem not in classes:
        errors.append(f"MonoBehaviour filename mismatch: {source.name}")

for scene in re.findall(r"path: (.+)", (root / "ProjectSettings/EditorBuildSettings.asset").read_text()):
    if not (root / scene).is_file():
        errors.append(f"Missing build scene: {scene}")

for source in list((root / "Assets").rglob("*.asmdef")) + list((root / "Assets").rglob("*.inputactions")):
    json.loads(source.read_text())
manifest = json.loads((root / "Packages/manifest.json").read_text())["dependencies"]
locked = json.loads((root / "Packages/packages-lock.json").read_text())["dependencies"]
for name, version in manifest.items():
    if name not in locked or locked[name]["version"] != version:
        errors.append(f"Package lock mismatch: {name}")

wrapper = (root / "Assets/Scripts/InputActions.cs").read_text()
embedded = re.search(r'FromJson\(@"(.*?)"\);', wrapper, re.DOTALL)
if not embedded or json.loads(embedded[1].replace('""', '"')) != json.loads((root / "Assets/Scripts/InputActions.inputactions").read_text()):
    errors.append("InputActions wrapper is out of sync; regenerate it in Unity")

if errors:
    raise SystemExit("\n".join(errors))
print(f"Project checks passed: {len(guids)} asset GUIDs, script names, build scenes, and package versions.")

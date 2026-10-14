#!/usr/bin/env python3
"""Build pinned Git institution plugins without referencing them from the shell project."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

root = Path(__file__).resolve().parents[1]
shared = {"AethericForge.Runtime.Abstractions.Interfaces", "AethericForge.Runtime.Institutions.Abstractions", "AethericForge.Runtime.Models"}
for spec in json.loads((root / "institution-modules.json").read_text())["Modules"]:
    assert re.fullmatch(r"[a-z0-9-]+", spec["Id"])
    source = (root / spec["SourcePath"]).resolve()
    assert source.is_relative_to(root)
    revision = subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if revision != spec["Revision"]:
        raise SystemExit("Institution source differs from its pinned revision: " + spec["Id"])
    subprocess.run(["git", "-C", str(source), "diff", "--quiet", "HEAD"], check=True)
    submodules = subprocess.check_output(["git", "-C", str(source), "submodule", "status", "--recursive"], text=True)
    if any(line and line[0] != " " for line in submodules.splitlines()):
        raise SystemExit("Initialize the institution's pinned submodules before building.")
    working = root / ".module-build" / spec["Id"]
    code, assets = working / "code", working / "assets"
    for project, output in [(spec["Project"], code), (spec["AssetHost"], assets)]:
        if output.exists(): shutil.rmtree(output)
        subprocess.run(["dotnet", "publish", str(source / project), "--configuration", "Release", "--output", str(output), "--nologo", "-m:1", "-p:UseAppHost=false"], check=True)
    bundle = root / "AethericForge.Web" / "modules" / spec["Id"]
    if bundle.exists(): shutil.rmtree(bundle)
    shutil.copytree(code, bundle)
    # Framework and institution contract assemblies bind to the shell's copies. Implementation
    # dependencies (including Mongo/Redis providers) remain private to the plugin load context.
    for path in bundle.rglob("*.dll"):
        if path.stem in shared or path.name.startswith(("Microsoft.", "System.")):
            path.unlink()
    if (bundle / "wwwroot").exists(): shutil.rmtree(bundle / "wwwroot")
    content = assets / "wwwroot" / "_content"
    shutil.copytree(content, bundle / "wwwroot" / "_content", dirs_exist_ok=True)
    stylesheets = []
    for stylesheet in spec["Stylesheets"]:
        asset = bundle / "wwwroot" / stylesheet
        if not asset.is_file() and stylesheet.endswith(".bundle.scp.css"):
            matches = list(asset.parent.glob(asset.name.replace(".bundle.scp.css", ".*.bundle.scp.css")))
            if len(matches) == 1: asset = matches[0]
        if not asset.is_file(): raise SystemExit("Missing module stylesheet: " + stylesheet)
        stylesheets.append(asset.relative_to(bundle / "wwwroot").as_posix())
    for key in ["DefinitionPath", "BindingsPath"]:
        dest = bundle / spec[key]
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / spec[key], dest)
    def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
    metadata = {key: spec[key] for key in ["Id", "Repository", "Revision", "Assembly", "DefinitionPath", "BindingsPath", "Stylesheets"]}
    metadata["Stylesheets"] = stylesheets
    metadata["DefinitionHash"] = digest(bundle / spec["DefinitionPath"])
    metadata["BindingsHash"] = digest(bundle / spec["BindingsPath"])
    metadata["Files"] = {p.relative_to(bundle).as_posix(): digest(p) for p in sorted(bundle.rglob("*")) if p.is_file()}
    if not (bundle / Path(spec["Assembly"]).with_suffix(".deps.json")).exists():
        raise SystemExit("Missing plugin dependency manifest.")
    (bundle / "module.json").write_text(json.dumps(metadata, indent=2) + "\n")
    print("Built pinned institution plugin: " + spec["Id"] + " at " + revision)

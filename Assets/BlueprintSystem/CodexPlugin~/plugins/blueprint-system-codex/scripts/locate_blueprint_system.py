#!/usr/bin/env python3
"""Locate the BlueprintSystem package in a Unity project."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


PACKAGE_NAME = "com.shadedclark.blueprint-system"


def read_package_name(package_json: Path) -> str:
    try:
        payload = json.loads(package_json.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return ""
    name = payload.get("name")
    return name if isinstance(name, str) else ""


def is_blueprint_package(path: Path) -> bool:
    package_json = path / "package.json"
    return package_json.is_file() and read_package_name(package_json) == PACKAGE_NAME


def candidates(project_root: Path) -> list[Path]:
    result: list[Path] = []

    assets_package = project_root / "Assets" / "BlueprintSystem"
    if is_blueprint_package(assets_package):
        result.append(assets_package)

    packages_root = project_root / "Packages"
    if packages_root.is_dir():
        for package_json in sorted(packages_root.rglob("package.json")):
            package_root = package_json.parent
            if is_blueprint_package(package_root):
                result.append(package_root)

    package_cache = project_root / "Library" / "PackageCache"
    if package_cache.is_dir():
        for package_json in sorted(package_cache.rglob("package.json")):
            package_root = package_json.parent
            if is_blueprint_package(package_root):
                result.append(package_root)

    unique: list[Path] = []
    seen: set[Path] = set()
    for item in result:
        resolved = item.resolve()
        if resolved not in seen:
            seen.add(resolved)
            unique.append(item)
    return unique


def as_assetish_path(project_root: Path, path: Path) -> str:
    try:
        return path.relative_to(project_root).as_posix()
    except ValueError:
        return str(path)


def build_payload(project_root: Path) -> dict[str, Any]:
    matches = candidates(project_root)
    package_root = matches[0] if matches else None

    docs: dict[str, str] = {}
    if package_root is not None:
        for key, relative in {
            "readme": "README.md",
            "guide": "GUIDE.md",
            "featureAgent": "Agents/FeatureImplementationEntryAgent.md",
            "blueprintAgent": "Agents/BlueprintFeatureAgent.md",
            "uiAgent": "Agents/UIImplementationAgent.md",
            "prefabAnnotationAgent": "Agents/PrefabAnnotationBlueprintAgent.md",
            "aiBehaviorTreeAgent": "Agents/AIBehaviorTreeAgent.md",
            "behaviorTreeGuide": "BehaviorTree/GUIDE.md",
            "behaviorTreeDesign": "BehaviorTree/BehaviorTreeDesign.md",
        }.items():
            path = package_root / relative
            if path.is_file():
                docs[key] = as_assetish_path(project_root, path)

    return {
        "success": package_root is not None,
        "projectRoot": str(project_root),
        "packageName": PACKAGE_NAME,
        "packageRoot": "" if package_root is None else as_assetish_path(project_root, package_root),
        "allPackageRoots": [as_assetish_path(project_root, item) for item in matches],
        "docs": docs,
    }


def main() -> None:
    project_root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").expanduser().resolve()
    print(json.dumps(build_payload(project_root), indent=2))


if __name__ == "__main__":
    main()

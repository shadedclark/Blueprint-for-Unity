#!/usr/bin/env python3
"""Install or sync the BlueprintSystem Codex companion plugin into a project."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path
from typing import Any
from urllib.parse import quote


PLUGIN_NAME = "blueprint-system-codex"
PACKAGE_NAME = "com.shadedclark.blueprint-system"
DEFAULT_MARKETPLACE_NAME = "personal"


def read_json(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"{path} must contain a JSON object.")
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")


def package_name(package_root: Path) -> str:
    package_json = package_root / "package.json"
    if not package_json.is_file():
        return ""
    try:
        payload = read_json(package_json)
    except (OSError, json.JSONDecodeError, ValueError):
        return ""
    name = payload.get("name")
    return name if isinstance(name, str) else ""


def is_project_root(path: Path) -> bool:
    return (path / "Assets").is_dir() or (path / "Packages" / "manifest.json").is_file()


def find_project_root_or_none(start: Path) -> Path | None:
    current = start.resolve()
    if current.is_file():
        current = current.parent
    for candidate in [current, *current.parents]:
        if is_project_root(candidate):
            return candidate
    return None


def find_project_root(start: Path) -> Path:
    return find_project_root_or_none(start) or start.resolve()


def resolve_project_root(project_root_arg: str | None, script_path: Path) -> Path:
    if project_root_arg:
        return find_project_root(Path(project_root_arg).expanduser())

    script_project_root = find_project_root_or_none(script_path)
    if script_project_root is not None:
        return script_project_root

    return find_project_root(Path.cwd())


def is_blueprint_package(path: Path) -> bool:
    return package_name(path) == PACKAGE_NAME


def find_blueprint_package(project_root: Path, script_path: Path) -> Path:
    script_package_root = script_path.resolve().parents[2]
    if is_blueprint_package(script_package_root):
        return script_package_root

    candidates = [
        project_root / "Assets" / "BlueprintSystem",
    ]

    for root in (project_root / "Packages", project_root / "Library" / "PackageCache"):
        if root.is_dir():
            candidates.extend(package_json.parent for package_json in sorted(root.rglob("package.json")))

    for candidate in candidates:
        if is_blueprint_package(candidate):
            return candidate

    raise FileNotFoundError(
        f"Could not find package '{PACKAGE_NAME}' from project root {project_root}."
    )


def load_marketplace(path: Path) -> dict[str, Any]:
    if path.is_file():
        payload = read_json(path)
        payload.setdefault("name", DEFAULT_MARKETPLACE_NAME)
        payload.setdefault("interface", {"displayName": "Personal"})
        payload.setdefault("plugins", [])
        return payload
    return {
        "name": DEFAULT_MARKETPLACE_NAME,
        "interface": {
            "displayName": "Personal",
        },
        "plugins": [],
    }


def upsert_marketplace_entry(marketplace: dict[str, Any]) -> None:
    plugins = marketplace.setdefault("plugins", [])
    if not isinstance(plugins, list):
        raise ValueError("marketplace.json field 'plugins' must be an array.")

    entry = {
        "name": PLUGIN_NAME,
        "source": {
            "source": "local",
            "path": f"./plugins/{PLUGIN_NAME}",
        },
        "policy": {
            "installation": "AVAILABLE",
            "authentication": "ON_INSTALL",
        },
        "category": "Engineering",
    }

    for index, existing in enumerate(plugins):
        if isinstance(existing, dict) and existing.get("name") == PLUGIN_NAME:
            plugins[index] = entry
            break
    else:
        plugins.append(entry)


def build_deeplink(marketplace_path: Path, *, share: bool) -> str:
    query = quote(str(marketplace_path), safe="")
    suffix = "&mode=share" if share else ""
    return f"codex://plugins/{PLUGIN_NAME}?marketplacePath={query}{suffix}"


def install(project_root: Path, marketplace_root: Path, dry_run: bool) -> dict[str, Any]:
    package_root = find_blueprint_package(project_root, Path(__file__))
    codex_plugin_root = package_root / "CodexPlugin~"
    source_plugin = codex_plugin_root / "plugins" / PLUGIN_NAME
    source_marketplace = codex_plugin_root / "marketplace.json"

    if not source_plugin.is_dir():
        raise FileNotFoundError(f"Missing source plugin directory: {source_plugin}")
    if not source_marketplace.is_file():
        raise FileNotFoundError(f"Missing source marketplace file: {source_marketplace}")

    target_plugin = marketplace_root / "plugins" / PLUGIN_NAME
    target_marketplace = marketplace_root / "marketplace.json"

    if not dry_run:
        target_plugin.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(source_plugin, target_plugin, dirs_exist_ok=True)

        marketplace = load_marketplace(target_marketplace)
        upsert_marketplace_entry(marketplace)
        write_json(target_marketplace, marketplace)

    return {
        "success": True,
        "dryRun": dry_run,
        "projectRoot": str(project_root),
        "packageRoot": str(package_root),
        "sourcePlugin": str(source_plugin),
        "targetPlugin": str(target_plugin),
        "marketplaceRoot": str(project_root),
        "marketplacePath": str(target_marketplace),
        "marketplaceAddCommand": f"codex plugin marketplace add {project_root}",
        "viewUrl": build_deeplink(target_marketplace, share=False),
        "shareUrl": build_deeplink(target_marketplace, share=True),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Install or sync the BlueprintSystem Codex companion plugin."
    )
    parser.add_argument(
        "project_root",
        nargs="?",
        default=None,
        help=(
            "Unity project root. Defaults to the project containing this script, "
            "then falls back to the current directory."
        ),
    )
    parser.add_argument(
        "--marketplace-root",
        default="",
        help="Marketplace root directory. Defaults to <project_root>/.agents/plugins.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the planned paths without copying files.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    project_root = resolve_project_root(args.project_root, Path(__file__))
    marketplace_root = (
        Path(args.marketplace_root).expanduser().resolve()
        if args.marketplace_root
        else project_root / ".agents" / "plugins"
    )
    result = install(project_root, marketplace_root, args.dry_run)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Install or sync the BlueprintSystem Codex companion plugin into a project."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import re
import shlex
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any
from urllib.parse import quote


PLUGIN_NAME = "blueprint-system-codex"
PACKAGE_NAME = "com.shadedclark.blueprint-system"
DEFAULT_MARKETPLACE_NAME = "personal"
DEFAULT_PLUGIN_VERSION = "0.1.0"


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


def utc_cachebuster() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")


def version_with_codex_cachebuster(version: str | None, cachebuster: str) -> str:
    base_version = version.split("+", 1)[0].strip() if isinstance(version, str) else ""
    if not base_version:
        base_version = DEFAULT_PLUGIN_VERSION
    return f"{base_version}+codex.{cachebuster}"


def refresh_plugin_cachebuster(plugin_root: Path, cachebuster: str) -> dict[str, Any]:
    manifest_path = plugin_root / ".codex-plugin" / "plugin.json"
    manifest = read_json(manifest_path)
    previous_version = manifest.get("version")
    next_version = version_with_codex_cachebuster(previous_version, cachebuster)
    manifest["version"] = next_version
    write_json(manifest_path, manifest)
    return {
        "manifestPath": str(manifest_path),
        "previousVersion": previous_version,
        "version": next_version,
    }


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


def title_from_slug(value: str) -> str:
    return " ".join(part.capitalize() for part in value.split("-") if part)


def slug_from_name(value: str) -> str:
    with_word_breaks = re.sub(r"(?<!^)(?=[A-Z])", "-", value)
    slug = re.sub(r"[^a-zA-Z0-9]+", "-", with_word_breaks).strip("-").lower()
    return re.sub(r"-{2,}", "-", slug) or "unity-project"


def default_marketplace_name(project_root: Path) -> str:
    return f"{slug_from_name(project_root.name)}-blueprint"


def load_marketplace(path: Path, marketplace_name: str) -> dict[str, Any]:
    if path.is_file():
        payload = read_json(path)
        if payload.get("name") in (None, "", DEFAULT_MARKETPLACE_NAME):
            payload["name"] = marketplace_name
        interface = payload.setdefault("interface", {})
        if isinstance(interface, dict) and interface.get("displayName") in (None, "", "Personal"):
            interface["displayName"] = title_from_slug(marketplace_name)
        payload.setdefault("plugins", [])
        return payload
    return {
        "name": marketplace_name,
        "interface": {
            "displayName": title_from_slug(marketplace_name),
        },
        "plugins": [],
    }


def resolved_marketplace_name(path: Path, default_name: str) -> str:
    if not path.is_file():
        return default_name
    try:
        payload = read_json(path)
    except (OSError, json.JSONDecodeError, ValueError):
        return default_name
    name = payload.get("name")
    if isinstance(name, str) and name not in ("", DEFAULT_MARKETPLACE_NAME):
        return name
    return default_name


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


def installed_skill_names(plugin_root: Path) -> list[str]:
    skills_root = plugin_root / "skills"
    if not skills_root.is_dir():
        return []
    return sorted(
        path.name
        for path in skills_root.iterdir()
        if path.is_dir() and (path / "SKILL.md").is_file()
    )


def unique_paths(paths: list[Path]) -> list[Path]:
    result: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        result.append(resolved)
    return result


def build_deeplink(marketplace_path: Path, *, share: bool) -> str:
    query = quote(str(marketplace_path), safe="")
    suffix = "&mode=share" if share else ""
    return f"codex://plugins/{PLUGIN_NAME}?marketplacePath={query}{suffix}"


def shell_join(command: list[str]) -> str:
    return " ".join(shlex.quote(part) for part in command)


def run_process(command: list[str]) -> dict[str, Any]:
    try:
        completed = subprocess.run(
            command,
            check=False,
            capture_output=True,
            text=True,
        )
    except FileNotFoundError as exc:
        return {
            "success": False,
            "command": shell_join(command),
            "returnCode": None,
            "stdout": "",
            "stderr": str(exc),
        }

    return {
        "success": completed.returncode == 0,
        "command": shell_join(command),
        "returnCode": completed.returncode,
        "stdout": completed.stdout.strip(),
        "stderr": completed.stderr.strip(),
    }


def register_marketplace(project_root: Path, codex_command: str) -> dict[str, Any]:
    command = [codex_command, "plugin", "marketplace", "add", str(project_root)]
    return run_process(command)


def open_url(url: str) -> dict[str, Any]:
    if sys.platform == "darwin":
        command = ["open", url]
    elif sys.platform.startswith("win"):
        command = ["cmd", "/c", "start", "", url]
    else:
        command = ["xdg-open", url]

    return run_process(command)


def install(
    project_root: Path,
    marketplace_root: Path,
    plugin_install_root: Path,
    dry_run: bool,
) -> dict[str, Any]:
    package_root = find_blueprint_package(project_root, Path(__file__))
    codex_plugin_root = package_root / "CodexPlugin~"
    source_plugin = codex_plugin_root / "plugins" / PLUGIN_NAME
    source_marketplace = codex_plugin_root / "marketplace.json"

    if not source_plugin.is_dir():
        raise FileNotFoundError(f"Missing source plugin directory: {source_plugin}")
    if not source_marketplace.is_file():
        raise FileNotFoundError(f"Missing source marketplace file: {source_marketplace}")

    target_plugin = plugin_install_root / PLUGIN_NAME
    compatibility_plugins = unique_paths(
        [
            target_plugin,
            marketplace_root / "plugins" / PLUGIN_NAME,
        ]
    )
    target_marketplace = marketplace_root / "marketplace.json"
    marketplace_name = resolved_marketplace_name(
        target_marketplace,
        default_marketplace_name(project_root),
    )
    cachebuster = utc_cachebuster()
    cachebuster_result: dict[str, Any] = {
        "success": False,
        "skipped": True,
        "reason": "dry-run",
    }

    if not dry_run:
        cachebuster_result = {
            "success": True,
            "installations": [],
        }
        for plugin_path in compatibility_plugins:
            plugin_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copytree(source_plugin, plugin_path, dirs_exist_ok=True)
            refreshed = refresh_plugin_cachebuster(plugin_path, cachebuster)
            cachebuster_result["installations"].append(refreshed)

        marketplace = load_marketplace(target_marketplace, marketplace_name)
        upsert_marketplace_entry(marketplace)
        write_json(target_marketplace, marketplace)
        marketplace_name = str(marketplace.get("name") or marketplace_name)

    return {
        "success": True,
        "dryRun": dry_run,
        "projectRoot": str(project_root),
        "packageRoot": str(package_root),
        "sourcePlugin": str(source_plugin),
        "targetPlugin": str(target_plugin),
        "compatibilityPlugins": [str(path) for path in compatibility_plugins],
        "marketplaceRoot": str(marketplace_root),
        "marketplacePath": str(target_marketplace),
        "pluginInstallRoot": str(plugin_install_root),
        "marketplaceName": marketplace_name,
        "pluginCachebuster": cachebuster_result,
        "installedSkills": installed_skill_names(target_plugin),
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
        help=(
            "Marketplace manifest directory. Defaults to <project_root>/.agents/plugins. "
            "With the default project marketplace, plugin folders are installed under "
            "<project_root>/plugins so `codex plugin marketplace add <project_root>` can resolve them."
        ),
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the planned paths without copying files.",
    )
    parser.add_argument(
        "--no-register",
        action="store_true",
        help="Do not run `codex plugin marketplace add <project_root>` after installing.",
    )
    parser.add_argument(
        "--no-open",
        action="store_true",
        help="Do not open the Codex plugin view URL after a successful install.",
    )
    parser.add_argument(
        "--codex-command",
        default="codex",
        help="Codex CLI command used for marketplace registration. Defaults to `codex`.",
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
    plugin_install_root = (
        marketplace_root / "plugins"
        if args.marketplace_root
        else project_root / "plugins"
    )
    result = install(project_root, marketplace_root, plugin_install_root, args.dry_run)
    if not args.dry_run and not args.no_register:
        registration = register_marketplace(project_root, args.codex_command)
        result["marketplaceRegistration"] = registration
    else:
        result["marketplaceRegistration"] = {
            "success": False,
            "skipped": True,
            "reason": "dry-run" if args.dry_run else "--no-register",
        }

    registration_succeeded = bool(result["marketplaceRegistration"].get("success"))
    can_open = args.no_register or registration_succeeded
    if not args.dry_run and not args.no_open and can_open:
        result["openViewUrl"] = open_url(result["viewUrl"])
    else:
        if args.dry_run:
            reason = "dry-run"
        elif args.no_open:
            reason = "--no-open"
        else:
            reason = "marketplace registration failed"
        result["openViewUrl"] = {
            "success": False,
            "skipped": True,
            "reason": reason,
        }

    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()

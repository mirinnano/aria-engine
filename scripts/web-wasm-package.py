#!/usr/bin/env python3
"""Deterministic helpers shared by the PowerShell and Bash WASM packagers."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def collect_glyphs(repo_root: Path, output: Path) -> None:
    engine_root = repo_root / "src" / "AriaEngine"
    sources = sorted((engine_root / "assets" / "scripts").rglob("*.aria"))
    sources.extend(sorted((engine_root / "assets" / "i18n").rglob("*.json")))
    sources.append(engine_root / "init.aria")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        "\n".join(source.read_text(encoding="utf-8-sig") for source in sources),
        encoding="utf-8",
        newline="\n",
    )


def resolve_under(root: Path, relative: str) -> Path:
    destination = (root / relative).resolve()
    try:
        destination.relative_to(root.resolve())
    except ValueError as error:
        raise ValueError(f"Path escapes package root: {relative}") from error
    return destination


def finalize(wwwroot: Path) -> None:
    wwwroot = wwwroot.resolve()
    manifest_path = wwwroot / "aria-web-assets.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    entries = manifest.get("assets")
    if manifest.get("version") != 1 or not isinstance(entries, list):
        raise ValueError("Unsupported or malformed aria-web-assets.json")

    listed_paths: set[str] = set()
    for entry in entries:
        logical_path = str(entry["logicalPath"]).replace("\\", "/").lstrip("/")
        asset_path = resolve_under(wwwroot, logical_path)
        if not asset_path.is_file():
            raise FileNotFoundError(f"Manifest asset is missing: {logical_path}")
        entry["logicalPath"] = logical_path
        entry["url"] = logical_path
        entry["size"] = asset_path.stat().st_size
        entry["sha256"] = sha256(asset_path)
        listed_paths.add(logical_path)

    packaged_paths = {"init.aria"}
    packaged_paths.update(
        path.relative_to(wwwroot).as_posix()
        for path in (wwwroot / "assets").rglob("*")
        if path.is_file()
    )
    if listed_paths != packaged_paths:
        missing = sorted(packaged_paths - listed_paths)
        extra = sorted(listed_paths - packaged_paths)
        raise ValueError(f"Asset manifest coverage mismatch; missing={missing}, extra={extra}")

    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )

    framework_root = wwwroot / "_framework"
    framework_files = sorted(
        path.relative_to(wwwroot).as_posix()
        for path in framework_root.iterdir()
        if path.is_file()
        and path.suffix not in {".br", ".gz"}
        and not path.name.endswith(".pdb")
    )
    if not framework_files:
        raise ValueError("No framework files found for the offline application shell")

    service_worker_path = wwwroot / "service-worker.js"
    cache_material = "\n".join(
        f"{sha256(path)}  {path.relative_to(wwwroot).as_posix()}"
        for path in sorted(wwwroot.rglob("*"))
        if path.is_file() and path != service_worker_path
    )
    cache_version = hashlib.sha256(cache_material.encode("utf-8")).hexdigest()[:16]

    service_worker = service_worker_path.read_text(encoding="utf-8-sig")
    framework_declaration = "const FRAMEWORK_SHELL = " + json.dumps(
        framework_files, ensure_ascii=False, indent=2
    ) + ";"
    marker = "const FRAMEWORK_SHELL = []; // __ARIA_FRAMEWORK_SHELL__"
    if marker not in service_worker or "__ARIA_ASSET_VERSION__" not in service_worker:
        raise ValueError("Service worker package markers are missing")
    service_worker = service_worker.replace(marker, framework_declaration)
    service_worker = service_worker.replace("__ARIA_ASSET_VERSION__", cache_version)
    service_worker_path.write_text(service_worker, encoding="utf-8", newline="\n")

    print(json.dumps({
        "assetManifestSha256": sha256(manifest_path),
        "cacheVersion": cache_version,
        "frameworkFiles": len(framework_files),
    }))


def main() -> None:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    glyphs = subparsers.add_parser("glyphs")
    glyphs.add_argument("--repo-root", type=Path, required=True)
    glyphs.add_argument("--output", type=Path, required=True)

    finalize_parser = subparsers.add_parser("finalize")
    finalize_parser.add_argument("--wwwroot", type=Path, required=True)

    args = parser.parse_args()
    if args.command == "glyphs":
        collect_glyphs(args.repo_root.resolve(), args.output.resolve())
    else:
        finalize(args.wwwroot)


if __name__ == "__main__":
    main()

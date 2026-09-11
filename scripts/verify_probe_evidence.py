#!/usr/bin/env python3
"""Verify the integrity and privacy contract of a packaged SmartDrag probe evidence directory."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

PRIVATE_KEYS = {"sourcepath", "fullpath", "outputpath", "filename", "displayname", "paths", "path"}
EXPECTED_FILES = {"manifest.json", "probe.jsonl", "probe.summary.json"}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def privacy_leaks(value: object, key: str = "") -> list[str]:
    leaks: list[str] = []
    if isinstance(value, dict):
        for child_key, child_value in value.items():
            leaks.extend(privacy_leaks(child_value, str(child_key)))
    elif isinstance(value, list):
        for child in value:
            leaks.extend(privacy_leaks(child, key))
    elif isinstance(value, str) and key.lower() != "logpath":
        looks_like_path = len(value) >= 3 and ((value[1] == ":" and value[2] in "\\/") or value.startswith("\\\\"))
        if looks_like_path or (key.lower() in PRIVATE_KEYS and ("\\" in value or "/" in value)):
            leaks.append(key)
    return leaks


def read_json(path: Path, label: str) -> tuple[dict | None, str | None]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return None, f"{label} is not valid UTF-8 JSON: {exc}"
    if not isinstance(value, dict):
        return None, f"{label} must contain a JSON object"
    return value, None


def read_jsonl(path: Path) -> tuple[list[dict], int, list[str]]:
    events: list[dict] = []
    malformed = 0
    leaks: list[str] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, 1):
            if not line.strip():
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                malformed += 1
                continue
            if not isinstance(record, dict):
                malformed += 1
                continue
            events.append(record)
            leaks.extend(privacy_leaks(record))
    return events, malformed, leaks


def fail(message: str) -> int:
    print(f"ERROR: {message}", file=sys.stderr)
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path, help="Evidence package directory")
    parser.add_argument(
        "--require-interaction",
        action="store_true",
        help="Require the retained analyzer summary to contain real drag/drop interaction evidence.",
    )
    args = parser.parse_args()
    package = args.package.resolve()
    if not package.is_dir():
        return fail(f"evidence package does not exist: {package}")

    actual_files = {path.name for path in package.iterdir() if path.is_file()}
    if actual_files != EXPECTED_FILES:
        return fail(f"package must contain exactly {sorted(EXPECTED_FILES)}; found {sorted(actual_files)}")

    manifest, error = read_json(package / "manifest.json", "manifest")
    if error:
        return fail(error)
    assert manifest is not None
    if manifest.get("schema") != 1 or manifest.get("package") != "SmartDragProbeEvidence":
        return fail("manifest schema/package marker is invalid")

    manifest_leaks = privacy_leaks(manifest)
    if manifest_leaks:
        return fail(f"manifest contains path-like privacy fields: {sorted(set(manifest_leaks))}")

    summary, error = read_json(package / "probe.summary.json", "analyzer summary")
    if error:
        return fail(error)
    assert summary is not None
    required_summary = {"mode", "events", "automaticChecksPassed", "manualVerificationRequired", "gateEligible"}
    missing = sorted(required_summary - summary.keys())
    if missing:
        return fail(f"analyzer summary is missing fields: {', '.join(missing)}")

    events, malformed, log_leaks = read_jsonl(package / "probe.jsonl")
    if malformed:
        return fail(f"raw JSONL contains {malformed} malformed/non-object lines")
    if log_leaks:
        return fail(f"raw JSONL contains path-like privacy fields: {sorted(set(log_leaks))}")
    if summary["events"] != len(events):
        return fail(f"summary event count {summary['events']} does not match raw JSONL count {len(events)}")
    if summary["mode"] != manifest.get("mode"):
        return fail("manifest mode does not match analyzer summary mode")
    for field in ("automaticChecksPassed", "manualVerificationRequired", "gateEligible"):
        if not isinstance(summary[field], bool) or manifest.get(field) != summary[field]:
            return fail(f"manifest and summary disagree on {field}")
    interaction = summary.get("interactionEvidencePresent")
    if not isinstance(interaction, bool) or manifest.get("interactionEvidencePresent") != interaction:
        return fail("manifest and summary disagree on interactionEvidencePresent")
    if args.require_interaction and not interaction:
        return fail("package contains no real drag/drop interaction evidence")

    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list) or {item.get("name") for item in artifacts if isinstance(item, dict)} != {
        "probe.jsonl", "probe.summary.json"
    }:
        return fail("manifest artifact list must contain exactly probe.jsonl and probe.summary.json")
    for artifact in artifacts:
        if not isinstance(artifact, dict):
            return fail("manifest artifact entry is not an object")
        name = artifact.get("name")
        if name not in {"probe.jsonl", "probe.summary.json"}:
            return fail(f"unexpected artifact name: {name!r}")
        target = package / name
        if artifact.get("bytes") != target.stat().st_size:
            return fail(f"byte count mismatch for {name}")
        if artifact.get("sha256") != sha256(target):
            return fail(f"SHA-256 mismatch for {name}")

    print("PROBE EVIDENCE VERIFY: PASS")
    print(f"Package: {package}")
    print(f"Mode: {manifest['mode']}; events: {len(events)}; interaction evidence: {'yes' if interaction else 'no'}")
    print(f"Automatic checks: {'pass' if summary['automaticChecksPassed'] else 'fail'}; manual review required: yes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

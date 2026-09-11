#!/usr/bin/env python3
"""Package one privacy-checked SmartDrag probe run for manual G1/G2 review.

Only the explicitly supplied JSONL and analyzer summary are copied. The package manifest contains hashes and operator
metadata, never absolute source paths or arbitrary workspace files.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import platform
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PRIVATE_KEYS = {"sourcepath", "fullpath", "outputpath", "filename", "displayname", "paths", "path"}


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


def git_revision() -> str | None:
    try:
        result = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        revision = result.stdout.strip()
        return revision or None
    except (OSError, subprocess.SubprocessError):
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path, help="Probe JSONL log")
    parser.add_argument("--summary", type=Path, help="Analyzer JSON summary (defaults to <log>.summary.json)")
    parser.add_argument("--out-dir", type=Path, required=True, help="New, empty evidence package directory")
    parser.add_argument("--surface", required=True, help="Explorer surface tested, e.g. explorer-folder or desktop")
    parser.add_argument("--dpi", default="unknown", help="Monitor scaling used during the run")
    parser.add_argument("--notes", default="", help="Short operator note without file paths or personal data")
    args = parser.parse_args()

    log = args.log.resolve()
    summary = (args.summary or Path(f"{log}.summary.json")).resolve()
    if not log.is_file():
        print(f"ERROR: log does not exist: {log}", file=sys.stderr)
        return 2
    if not summary.is_file():
        print(f"ERROR: analyzer summary does not exist: {summary}", file=sys.stderr)
        return 2
    if args.out_dir.exists() and any(args.out_dir.iterdir()):
        print(f"ERROR: output directory must be new or empty: {args.out_dir}", file=sys.stderr)
        return 2
    if "\\" in args.notes or "/" in args.notes:
        print("ERROR: --notes must not contain path separators or URLs", file=sys.stderr)
        return 2
    args.out_dir = args.out_dir.resolve()
    args.out_dir.mkdir(parents=True, exist_ok=True)

    try:
        summary_data = json.loads(summary.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERROR: invalid analyzer summary: {exc}", file=sys.stderr)
        return 2
    if not isinstance(summary_data, dict):
        print("ERROR: analyzer summary must be a JSON object", file=sys.stderr)
        return 2
    required_fields = {"mode", "events", "automaticChecksPassed", "manualVerificationRequired", "gateEligible"}
    missing = sorted(required_fields - summary_data.keys())
    if missing:
        print(f"ERROR: analyzer summary is missing fields: {', '.join(missing)}", file=sys.stderr)
        return 2

    raw_lines: list[dict] = []
    malformed = 0
    with log.open("r", encoding="utf-8") as handle:
        for line in handle:
            if not line.strip():
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                malformed += 1
                continue
            if isinstance(record, dict):
                raw_lines.append(record)

    leaks = [key for record in raw_lines for key in privacy_leaks(record)]
    if malformed or leaks:
        print(f"ERROR: log failed retention checks (malformed={malformed}, privacyLeaks={len(leaks)})", file=sys.stderr)
        return 1

    raw_target = args.out_dir / "probe.jsonl"
    summary_target = args.out_dir / "probe.summary.json"
    shutil.copyfile(log, raw_target)
    shutil.copyfile(summary, summary_target)
    manifest = {
        "schema": 1,
        "package": "SmartDragProbeEvidence",
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "mode": summary_data["mode"],
        "surface": args.surface,
        "dpi": args.dpi,
        "notes": args.notes,
        "gitRevision": git_revision(),
        "host": {"platform": platform.platform(), "python": platform.python_version()},
        "interactionEvidencePresent": summary_data.get("interactionEvidencePresent", False),
        "automaticChecksPassed": summary_data["automaticChecksPassed"],
        "manualVerificationRequired": summary_data["manualVerificationRequired"],
        "gateEligible": summary_data["gateEligible"],
        "privacyLeaks": len(leaks),
        "artifacts": [
            {"name": "probe.jsonl", "bytes": raw_target.stat().st_size, "sha256": sha256(raw_target)},
            {"name": "probe.summary.json", "bytes": summary_target.stat().st_size, "sha256": sha256(summary_target)},
        ],
    }
    (args.out_dir / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    verification = subprocess.run(
        [sys.executable, str(ROOT / "scripts" / "verify_probe_evidence.py"), str(args.out_dir)],
        cwd=ROOT,
        check=False,
    )
    if verification.returncode != 0:
        print("ERROR: generated evidence package failed independent verification", file=sys.stderr)
        return 1
    print(f"PROBE EVIDENCE PACKAGE: PASS")
    print(f"Directory: {args.out_dir}")
    print(f"Mode: {manifest['mode']}; interaction evidence: {'yes' if manifest['interactionEvidencePresent'] else 'no'}")
    print("Manual matrix verification remains required before changing gate state.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

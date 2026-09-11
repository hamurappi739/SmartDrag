#!/usr/bin/env python3
"""Verify a SmartDrag safe-preview publish directory without executing it."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path, PurePosixPath


EXPECTED_CHECKS = 21


def fail(message: str) -> int:
    print(f"SAFE PREVIEW VERIFY: FAIL — {message}")
    return 1


def normalized_name(value: object) -> str | None:
    if not isinstance(value, str) or not value or "\\" in value:
        return None
    path = PurePosixPath(value)
    if path.is_absolute() or any(part in ("", ".", "..") for part in path.parts):
        return None
    return path.as_posix()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path, help="safe preview publish directory")
    args = parser.parse_args()
    package = args.package.resolve()
    manifest_path = package / "manifest.json"

    if not package.is_dir():
        return fail("package directory does not exist")
    if not manifest_path.is_file():
        return fail("manifest.json is missing")

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        return fail(f"manifest cannot be read: {type(exc).__name__}")

    if not isinstance(manifest, dict):
        return fail("manifest root must be an object")
    if manifest.get("schema") != 1 or manifest.get("package") != "SmartDragSafePreview":
        return fail("unsupported package schema or package name")
    if manifest.get("runtime") != "win-x64":
        return fail("runtime must be win-x64")
    if manifest.get("nativeActivationEnabled") is not False:
        return fail("native activation is not explicitly disabled")
    if manifest.get("webpEncoder") != "unavailable-until-G3":
        return fail("WebP capability is not recorded as unavailable-until-G3")

    self_check = manifest.get("selfCheck")
    if not isinstance(self_check, dict) or self_check.get("expectedChecks") != EXPECTED_CHECKS or self_check.get("passed") is not True:
        return fail("manifest self-check metadata is not a passing 21-check record")
    self_check_name = normalized_name(self_check.get("path"))
    if self_check_name != "preview-self-check.json":
        return fail("self-check path must be preview-self-check.json")

    report_path = package / self_check_name
    try:
        report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    except FileNotFoundError:
        return fail("preview self-check report is missing")
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        return fail(f"preview self-check report cannot be read: {type(exc).__name__}")
    checks = report.get("Checks")
    if report.get("Passed") is not True or not isinstance(checks, list) or len(checks) != EXPECTED_CHECKS:
        return fail("preview self-check report is not a passing 21-check record")
    if any(not isinstance(check, dict) or check.get("Passed") is not True for check in checks):
        return fail("preview self-check contains a failed check")

    entries = manifest.get("files")
    if not isinstance(entries, list) or not entries:
        return fail("manifest files list is empty or invalid")

    expected: dict[str, tuple[int, str]] = {}
    for entry in entries:
        if not isinstance(entry, dict):
            return fail("manifest contains a non-object file entry")
        name = normalized_name(entry.get("name"))
        digest = entry.get("sha256")
        size = entry.get("bytes")
        if name is None or name == "manifest.json" or name in expected:
            return fail("manifest contains an unsafe or duplicate file name")
        if not isinstance(digest, str) or len(digest) != 64 or any(char not in "0123456789abcdefABCDEF" for char in digest):
            return fail(f"manifest has an invalid hash for {name}")
        if not isinstance(size, int) or size < 0:
            return fail(f"manifest has an invalid byte count for {name}")
        expected[name] = (size, digest.lower())

    actual = {
        file.relative_to(package).as_posix()
        for file in package.rglob("*")
        if file.is_file() and file.name != "manifest.json"
    }
    # Older bundles may reference the self-check report only from metadata; newer publishers include it in
    # the hash list. Accept both representations while always requiring the report itself to be present.
    actual_hashed = actual if self_check_name in expected else actual - {self_check_name}
    if actual_hashed != set(expected) or self_check_name not in actual:
        missing = sorted(set(expected) - actual_hashed)
        unexpected = sorted(actual_hashed - set(expected))
        if self_check_name not in actual:
            missing.append(self_check_name)
        return fail(f"manifest/file set mismatch (missing={missing}, unexpected={unexpected})")

    for name, (expected_size, expected_hash) in expected.items():
        path = package / Path(*PurePosixPath(name).parts)
        if path.stat().st_size != expected_size:
            return fail(f"byte count mismatch for {name}")
        if sha256(path) != expected_hash:
            return fail(f"SHA-256 mismatch for {name}")

    print(f"SAFE PREVIEW VERIFY: PASS — {len(expected)} files, self-check {EXPECTED_CHECKS}/{EXPECTED_CHECKS}, native activation disabled")
    return 0


if __name__ == "__main__":
    sys.exit(main())

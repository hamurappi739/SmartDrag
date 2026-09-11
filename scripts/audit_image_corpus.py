#!/usr/bin/env python3
"""Audit deterministic image fixtures without loading a concrete production codec.

This is a G3 preparation tool: it records container-level facts and intentional negative fixtures so codec
benchmarks can be compared against the same corpus. It never declares a codec accepted.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "tests" / "fixtures" / "images"
EXPECTED_NEGATIVES = {"animated-negative.gif", "not-an-image.png", "truncated.jpg"}


def _png(data: bytes) -> dict:
    result = {"container": "PNG", "valid": False, "frames": 1, "metadata": {"textChunks": 0}}
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        return result | {"error": "signature"}
    offset = 8
    saw_ihdr = False
    saw_iend = False
    while offset + 12 <= len(data):
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        end = offset + 12 + length
        if end > len(data):
            return result | {"error": "truncated-chunk"}
        payload = data[offset + 8 : offset + 8 + length]
        if chunk_type == b"IHDR" and length == 13:
            result.update(
                width=struct.unpack(">I", payload[0:4])[0],
                height=struct.unpack(">I", payload[4:8])[0],
                bitDepth=payload[8],
                colorType=payload[9],
            )
            saw_ihdr = True
        elif chunk_type in {b"tEXt", b"zTXt", b"iTXt"}:
            result["metadata"]["textChunks"] += 1
        elif chunk_type == b"IEND":
            saw_iend = True
            break
        offset = end
    if not saw_ihdr:
        return result | {"error": "missing-IHDR"}
    if not saw_iend:
        return result | {"error": "missing-IEND"}
    return result | {"valid": True, "error": None}


def _jpeg(data: bytes) -> dict:
    result = {"container": "JPEG", "valid": False, "frames": 0, "metadata": {"exif": False, "icc": False}}
    if not data.startswith(b"\xff\xd8"):
        return result | {"error": "signature"}
    offset = 2
    saw_eoi = False
    sof_markers = {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}
    while offset < len(data):
        if data[offset] != 0xFF:
            return result | {"error": "marker-sync"}
        while offset < len(data) and data[offset] == 0xFF:
            offset += 1
        if offset >= len(data):
            break
        marker = data[offset]
        offset += 1
        if marker == 0xD9:
            saw_eoi = True
            break
        if marker == 0xDA:
            # The entropy-coded scan is not a segment stream. For a container audit we only need
            # to confirm that an EOI marker exists after the scan begins.
            saw_eoi = data.rfind(b"\xff\xd9") > offset
            break
        if marker in {0xD8, 0x01} or 0xD0 <= marker <= 0xD7:
            continue
        if offset + 2 > len(data):
            return result | {"error": "truncated-segment-length"}
        segment_length = struct.unpack(">H", data[offset : offset + 2])[0]
        if segment_length < 2 or offset + segment_length > len(data):
            return result | {"error": "truncated-segment"}
        payload = data[offset + 2 : offset + segment_length]
        if marker == 0xE1 and payload.startswith(b"Exif\x00\x00"):
            result["metadata"]["exif"] = True
        elif marker == 0xE2 and payload.startswith(b"ICC_PROFILE\x00"):
            result["metadata"]["icc"] = True
        if marker in sof_markers and len(payload) >= 6:
            result.update(height=struct.unpack(">H", payload[1:3])[0], width=struct.unpack(">H", payload[3:5])[0])
            result["frames"] += 1
        offset += segment_length
    if not saw_eoi:
        return result | {"error": "missing-EOI"}
    if result["frames"] == 0:
        return result | {"error": "missing-SOF"}
    return result | {"valid": True, "error": None}


def _container(data: bytes, suffix: str) -> dict:
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return _png(data)
    if data.startswith(b"\xff\xd8"):
        return _jpeg(data)
    if data.startswith((b"GIF87a", b"GIF89a")):
        frames = data.count(b"\x2c")
        return {"container": "GIF", "valid": len(data) >= 13, "frames": frames, "metadata": {}, "error": None}
    return {"container": "Unknown", "valid": False, "frames": 0, "metadata": {}, "error": "unsupported-signature"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json-out", type=Path, default=ROOT / "artifacts" / "g3-corpus" / "audit-latest.json")
    args = parser.parse_args()
    manifest_path = CORPUS / "CORPUS_MANIFEST.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    records: list[dict] = []
    errors: list[str] = []
    for item in manifest.get("files", []):
        name = item["name"]
        path = CORPUS / name
        if not path.exists():
            errors.append(f"missing: {name}")
            continue
        data = path.read_bytes()
        facts = _container(data, path.suffix.lower())
        record = {
            "name": name,
            "suffix": path.suffix.lower(),
            "bytes": len(data),
            "sha256": hashlib.sha256(data).hexdigest(),
            **facts,
            "expectedNegative": name in EXPECTED_NEGATIVES,
        }
        records.append(record)
        if not record["expectedNegative"] and not facts["valid"]:
            errors.append(f"positive fixture did not parse: {name} ({facts.get('error')})")
        if record["expectedNegative"] and name == "animated-negative.gif" and facts["frames"] < 2:
            errors.append(f"animated negative fixture lost its multi-frame signal: {name}")

    summary = {
        "schema": 1,
        "tool": "audit_image_corpus.py",
        "codecDecision": "UNRESOLVED",
        "manifest": str(manifest_path),
        "fixtureCount": len(records),
        "validPositiveFixtures": sum(1 for r in records if r["valid"] and not r["expectedNegative"]),
        "intentionalNegativeFixtures": sum(1 for r in records if r["expectedNegative"]),
        "errors": errors,
        "fixtures": records,
    }
    args.json_out.parent.mkdir(parents=True, exist_ok=True)
    args.json_out.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"G3 CORPUS AUDIT: {'PASS' if not errors else 'FAIL'}")
    print(f"Fixtures: {len(records)}; positive parsed: {summary['validPositiveFixtures']}; negatives: {summary['intentionalNegativeFixtures']}")
    print(f"Report: {args.json_out}")
    for error in errors:
        print(f"- {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())

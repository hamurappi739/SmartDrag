#!/usr/bin/env python3
"""Summarize SmartDrag Windows Probe JSONL evidence without external dependencies."""
from __future__ import annotations

import argparse
import collections
import json
import sys
from pathlib import Path

_PRIVACY_KEYS = {"sourcepath", "fullpath", "outputpath", "filename", "displayname", "paths", "path"}


def _counter_value(value: object) -> int:
    """Read bounded counters defensively; malformed telemetry must fail closed, not crash analysis."""
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        return 0
    return value


def _data(event: dict) -> dict:
    data = event.get("data")
    return data if isinstance(data, dict) else {}


def _privacy_leaks(event: dict) -> list[str]:
    """Find path-like values in fields that must stay out of diagnostic evidence."""
    leaks: list[str] = []

    def visit(value: object, key: str) -> None:
        if isinstance(value, dict):
            for child_key, child_value in value.items():
                visit(child_value, str(child_key))
            return
        if isinstance(value, list):
            for child in value:
                visit(child, key)
            return
        if not isinstance(value, str) or key.lower() == "logpath":
            return
        looks_like_path = len(value) >= 3 and (
            (value[1] == ":" and value[2] in {"\\", "/"}) or value.startswith("\\\\")
        )
        if key.lower() in _PRIVACY_KEYS and (looks_like_path or "\\" in value or "/" in value):
            leaks.append(key)
        elif looks_like_path:
            leaks.append(key)

    visit(event, "")
    return leaks


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("log", type=Path)
    parser.add_argument("--json-out", type=Path, help="Write the machine-readable summary beside the human report.")
    parser.add_argument(
        "--require-interaction",
        action="store_true",
        help="Fail when the log contains no real drag/drop interaction (use for gate review, not startup smoke).",
    )
    args = parser.parse_args()

    if not args.log.exists():
        print(f"ERROR: log does not exist: {args.log}", file=sys.stderr)
        return 2

    events: list[dict] = []
    malformed = 0
    invalid_records = 0
    with args.log.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, 1):
            line = line.strip()
            if not line:
                continue
            try:
                record = json.loads(line)
                if not isinstance(record, dict):
                    invalid_records += 1
                    print(f"WARN: JSONL line {line_number} is not an object", file=sys.stderr)
                    continue
                if "data" in record and not isinstance(record["data"], dict):
                    invalid_records += 1
                    print(f"WARN: JSONL line {line_number} has non-object data", file=sys.stderr)
                events.append(record)
            except json.JSONDecodeError as exc:
                malformed += 1
                print(f"WARN: malformed JSONL line {line_number}: {exc}", file=sys.stderr)

    counts = collections.Counter(event.get("eventName") for event in events)
    started = next((event for event in events if event.get("eventName") == "probe.started"), None)
    mode = _data(started or {}).get("mode", "unknown")

    overlay_events = [e for e in events if e.get("eventName") == "overlay.shown"]
    focus_changes = [e for e in overlay_events if _data(e).get("foregroundChanged") is True]
    drops = [e for e in events if e.get("eventName") == "ole.drop"]
    copy_drops = [e for e in drops if _data(e).get("returnedEffect") == 1]
    mismatches = [e for e in drops if _data(e).get("preflightMatched") is False]
    preflight_unavailable = counts["candidate.suppressed.preflight-unavailable"]
    preflight_rejected = counts["candidate.suppressed.preflight-rejected"]
    p0_warning = counts["p0.signal-only-warning"] > 0
    drag_start_count = sum(
        1 for e in events
        if e.get("eventName") == "winevent.drag-signal"
        and _data(e).get("kind") == "Started"
    )
    native_boundary_failures = [
        e for e in events
        if str(e.get("eventName", "")).endswith(".exception")
        or e.get("eventName") in {"window.wndproc-exception", "overlay.show-failed"}
    ]
    move_effects = [
        e for e in events
        if e.get("eventName") in {"ole.drag-enter", "ole.drop"}
        and isinstance(_data(e).get("returnedEffect"), int)
        and (_data(e).get("returnedEffect") & 2) != 0
    ]
    stopped = next((event for event in reversed(events) if event.get("eventName") == "probe.stopped"), None)
    stopped_data = _data(stopped or {})
    dropped_log_entries = _counter_value(stopped_data.get("droppedLogEntries"))
    dropped_winevent_signals = _counter_value(stopped_data.get("droppedWinEventSignals"))
    invalid_g2_overlays = [
        e for e in overlay_events
        if mode == "G2ExplorerSelection"
        and _data(e).get("preflightState") not in {"Eligible", "Supported"}
    ]
    privacy_leaks = [leak for event in events for leak in _privacy_leaks(event)]

    print("SmartDrag probe evidence summary")
    print(f"log: {args.log}")
    print(f"mode: {mode}")
    print(f"events: {len(events)} (malformed: {malformed}, invalid records: {invalid_records})")
    print(f"drag starts: {drag_start_count}")
    print(f"overlays shown: {len(overlay_events)}")
    print(f"foreground changes while showing overlay: {len(focus_changes)}")
    print(f"OLE drops: {len(drops)} (COPY: {len(copy_drops)}, NONE/other: {len(drops)-len(copy_drops)})")
    print(f"preflight unavailable suppressions: {preflight_unavailable}")
    print(f"preflight rejected suppressions: {preflight_rejected}")
    print(f"preflight mismatches at Drop: {len(mismatches)}")
    print(f"diagnostic P0 signal-only evidence present: {'yes' if p0_warning else 'no'}")
    print(f"native-boundary/overlay failures: {len(native_boundary_failures)}")
    print(f"MOVE effects observed: {len(move_effects)}")
    print(f"dropped log entries: {dropped_log_entries}")
    print(f"dropped WinEvent signals: {dropped_winevent_signals}")
    print(f"privacy-sensitive path fields: {len(privacy_leaks)}")
    if mode == "G2ExplorerSelection":
        print(f"G2 overlays without eligible preflight: {len(invalid_g2_overlays)}")

    problems: list[str] = []
    if started is None:
        problems.append("probe.started event is missing")
    if stopped is None:
        problems.append("probe.stopped event is missing")
    if malformed:
        problems.append("malformed JSONL entries exist")
    if invalid_records:
        problems.append("JSONL records violate the object schema")
    if privacy_leaks:
        problems.append("diagnostic log contains path-like privacy-sensitive fields")
    if focus_changes:
        problems.append("overlay presentation changed foreground HWND")
    if mode == "G2ExplorerSelection" and p0_warning:
        problems.append("G2 log contains P0 signal-only warning")
    if mode == "G2ExplorerSelection" and mismatches:
        problems.append("G2 observed preflight/authoritative payload mismatch")
    if native_boundary_failures:
        problems.append("native callback/overlay failures were recorded")
    if move_effects:
        problems.append("OLE returned a MOVE effect; MVP must be COPY-or-NONE only")
    if dropped_log_entries:
        problems.append("probe dropped diagnostic log entries")
    if dropped_winevent_signals:
        problems.append("probe dropped WinEvent drag signals")
    if invalid_g2_overlays:
        problems.append("G2 showed overlay without eligible/supported preflight")
    if args.require_interaction and drag_start_count == 0 and not drops:
        problems.append("log contains no real drag/drop interaction; it cannot be gate evidence")

    summary = {
        "log": str(args.log),
        "mode": mode,
        "lifecycleComplete": started is not None and stopped is not None,
        "events": len(events),
        "malformed": malformed,
        "invalidRecords": invalid_records,
        "dragStarts": drag_start_count,
        "overlaysShown": len(overlay_events),
        "foregroundChanges": len(focus_changes),
        "oleDrops": len(drops),
        "copyDrops": len(copy_drops),
        "preflightUnavailableSuppressions": preflight_unavailable,
        "preflightRejectedSuppressions": preflight_rejected,
        "preflightMismatches": len(mismatches),
        "nativeBoundaryFailures": len(native_boundary_failures),
        "moveEffects": len(move_effects),
        "droppedLogEntries": dropped_log_entries,
        "droppedWinEventSignals": dropped_winevent_signals,
        "invalidG2Overlays": len(invalid_g2_overlays),
        "privacyLeaks": len(privacy_leaks),
        "interactionEvidencePresent": drag_start_count > 0 or bool(drops),
        "automaticRedFlags": problems,
        "manualVerificationRequired": True,
        "gateEligible": not problems and (not args.require_interaction or drag_start_count > 0 or bool(drops)),
        "automaticChecksPassed": not problems,
    }
    if args.json_out:
        args.json_out.parent.mkdir(parents=True, exist_ok=True)
        args.json_out.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    if problems:
        print("\nATTENTION")
        for problem in problems:
            print(f"- {problem}")
        return 1

    print("\nNo automatic red flags detected. Manual matrix verification is still required.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

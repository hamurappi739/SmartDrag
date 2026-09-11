# G1/G2 Evidence Tooling — Half-Phase Complete

Status: CLOSED for probe tooling only  
Closed: 2026-09-03  
Branch: main

## Objective

Make Windows evidence collection repeatable and fail-closed without pretending that automated startup output is
real Explorer evidence.

## Delivered

| Area | Result | Evidence |
|---|---|---|
| Repeatable launch | One PowerShell wrapper runs P0 or G2, selects the latest mode-specific JSONL, and invokes analysis | `scripts/run-probe.ps1` |
| Machine-readable report | Analyzer emits a stable UTF-8 JSON summary beside the raw log | `scripts/analyze_probe_log.py --json-out` |
| Interaction guard | Gate-mode analysis fails when a run contains no drag signal and no OLE drop | `--require-interaction` |
| Smoke separation | Startup/shutdown smoke remains available but intentionally omits the interaction requirement | `run-probe.ps1 -Smoke` |
| Evidence checklist | Runbook now documents wrapper, Python resolution, summary retention, and the smoke/evidence boundary | `docs/testing/PROBE_EVIDENCE_RUNBOOK.md` |

## Operator commands

```powershell
./scripts/run-probe.ps1 -Mode p0
./scripts/run-probe.ps1 -Mode g2
./scripts/run-probe.ps1 -Mode g2 -Smoke
```

The wrapper uses `SMARTDRAG_PYTHON` when set, otherwise `py -3`. A normal run writes a sibling
`smartdrag-probe-<mode>-<timestamp>.jsonl.summary.json` and returns non-zero for missing interaction or any
automatic red flag. Smoke mode is only a process/native-initialization check.

## Verification record

On the handoff Windows environment, the G2 smoke wrapper completed successfully and produced a JSON summary. Running
the same analyzer with `--require-interaction` against that startup-only log failed as designed with the explicit
“no real drag/drop interaction” guard. This proves the tooling boundary, not Explorer behavior.

## Explicit boundary

This half-phase does **not** close G1, G2, or G3. Real interactive Explorer sessions are still required to establish:

- drag coexistence and focus behavior (G1);
- strict pre-overlay qualification plus authoritative `CF_HDROP` matching (G2);
- a production WebP/codec decision with measured limits (G3).

Production native activation remains disabled until those empirical gates are accepted.

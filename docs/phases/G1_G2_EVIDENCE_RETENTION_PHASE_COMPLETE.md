# G1/G2 Evidence Retention — Phase Complete

Status: CLOSED for evidence packaging and retention  
Closed: 2026-09-03  
Branch: main

## Objective

Make a probe run portable for manual review while preserving the diagnostic privacy boundary and preventing accidental
overwriting of an earlier evidence bundle.

## Delivered

- `scripts/package_probe_evidence.py` accepts one raw JSONL log and its analyzer summary;
- the package is restricted to exactly three files: raw log, summary, and manifest;
- malformed records and path-like privacy fields fail packaging before any bundle is produced;
- the destination must be new or empty, avoiding silent evidence replacement;
- `manifest.json` records SHA-256, byte sizes, mode, interaction presence, git revision, host/runtime metadata, and
  explicit manual-verification-required state;
- runbook documentation now includes the exact packaging command and retention contract.

## Verification record

The current G2 startup smoke log was packaged successfully into `artifacts/probe-evidence/g2-smoke-20260903/`.
The resulting bundle contains `probe.jsonl`, `probe.summary.json`, and `manifest.json`; it correctly records that no
interaction evidence is present and keeps manual verification required.

## Explicit boundary

This phase does not accept G1/G2. A package is a tamper-evident, privacy-checked handoff unit, not a substitute for the
Explorer matrices, focus/coexistence observations, authoritative Drop checks, or human review.

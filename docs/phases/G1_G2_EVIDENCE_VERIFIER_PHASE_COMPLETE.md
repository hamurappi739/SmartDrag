# G1/G2 Evidence Verifier — Phase Complete

Status: CLOSED for retained-package verification  
Closed: 2026-09-03  
Branch: main

## Objective

Provide an independent, fail-closed check for probe evidence packages after capture, so hashes, structure, privacy,
and summary/manifest agreement are verified before a package reaches manual review.

## Delivered

- `scripts/verify_probe_evidence.py` validates the exact three-file package contract;
- SHA-256 and byte counts are recomputed for both retained artifacts;
- raw JSONL is reparsed and checked for malformed records and path-like privacy leaks;
- analyzer summary and manifest fields are compared for mode, interaction state, automatic checks, and manual-review
  requirements;
- optional `--require-interaction` fails closed for smoke-only packages;
- no gate state is changed by verification.

## Verification record

The verifier passed against both retained startup smoke packages:

```powershell
python ./scripts/verify_probe_evidence.py ./artifacts/probe-evidence/capture-p0-smoke
python ./scripts/verify_probe_evidence.py ./artifacts/probe-evidence/capture-g2-smoke
```

Both packages report two events, no interaction evidence, and `manualVerificationRequired: true`.

## Explicit boundary

Integrity verification is not G1/G2 acceptance. A package with no real drag/drop interaction remains diagnostic-only,
and a verified package still requires the applicable manual Explorer matrix.

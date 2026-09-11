# G3 Corpus Audit Preparation — Complete

Status: CLOSED for codec-neutral preparation  
Closed: 2026-09-03  
Branch: main

## Objective

Turn the existing deterministic image fixtures into a repeatable, machine-readable baseline for the future G3 codec
spike without selecting a library or weakening the pre-G3 dependency gate.

## Delivered

- `scripts/audit_image_corpus.py` parses PNG/JPEG/GIF container headers using only the Python standard library;
- positive fixtures report dimensions, frame counts and metadata signals (EXIF, ICC, PNG text chunks);
- intentional negative fixtures remain explicit (`animated-negative.gif`, `not-an-image.png`, `truncated.jpg`);
- SHA-256 and byte facts are emitted with a stable schema and `codecDecision: UNRESOLVED`;
- the audit is now a required step in `scripts/build.ps1` and writes an ignored report under `artifacts/g3-corpus/`;
- build-gate and corpus evidence docs describe the command and its non-acceptance boundary.

## Verification record

```powershell
python ./scripts/audit_image_corpus.py --json-out ./artifacts/g3-corpus/audit-latest.json
```

Observed result: 9 fixtures, 6 positive containers parsed, 3 intentional negatives classified, no errors. The report
records `codecDecision = UNRESOLVED` by design.

## Explicit boundary

This preparation does not prove WebP encoding, quality, alpha fidelity, metadata policy, peak memory, cancellation,
packaging, or licensing. Those remain open G3 acceptance criteria. No production codec dependency was added and native
activation remains gated by G1/G2/G3.

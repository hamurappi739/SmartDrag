# G1/G2 Operator Capture — Phase Complete

Status: CLOSED for capture automation only  
Closed: 2026-09-03  
Branch: main

## Objective

Make a complete probe evidence capture reproducible for both P0 and strict G2 modes without weakening the
interaction requirement or the diagnostic privacy boundary.

## Delivered

- `scripts/capture-probe-evidence.ps1` owns one capture flow from probe launch through analysis and evidence packaging;
- the wrapper supports `p0` and `g2`, explicit Explorer surface/DPI/operator notes, and a clearly separated `-Smoke` mode;
- output is restricted to a new or empty directory below repository `artifacts/`;
- a failed probe or missing analyzer summary prevents package creation;
- the existing analyzer and retention package remain authoritative for privacy, hashes, and gate eligibility.

## Verification record

Both startup smoke modes were captured successfully on the handoff Windows environment:

```powershell
./scripts/capture-probe-evidence.ps1 -Mode p0 -Smoke -Surface explorer-folder -Dpi "100%"
./scripts/capture-probe-evidence.ps1 -Mode g2 -Smoke -Surface explorer-folder -Dpi "100%"
```

Each package contains only the raw JSONL log, its analyzer summary, and `manifest.json`. Both correctly report no
interaction evidence and retain `manualVerificationRequired: true`.

## Explicit boundary

Smoke capture proves startup/shutdown only. It does not close G1 or G2. A non-smoke run still requires real Explorer
interaction and the corresponding manual matrix before any gate state or native activation policy changes.

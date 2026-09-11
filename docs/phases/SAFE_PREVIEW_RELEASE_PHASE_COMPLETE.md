# Safe Preview Release — Phase Complete

Status: CLOSED for preview packaging  
Closed: 2026-09-03  
Branch: main

## Objective

Turn the verified local preview into a reproducible Windows Release artifact with an executable self-check and an
explicit manifest, while preserving every unresolved production gate.

## Delivered

- `scripts/publish-preview.ps1` validates source/corpus invariants and restores the `win-x64` publish graph;
- Release output is written only under a validated repository `artifacts/` directory;
- the published executable runs the same 21-check preview self-check before packaging is declared successful;
- `manifest.json` records foundation, revision, runtime, self-contained mode, file hashes, native activation state, and
  the unresolved WebP codec state;
- existing output is never silently overwritten;
- release instructions are documented in `docs/release/SAFE_PREVIEW_PUBLISH.md`.

## Verification record

The framework-dependent Release bundle was published and executed successfully. Repository validation and corpus audit
passed, the output contained 23 hashed runtime files, and the published executable returned `PASS` for 21/21 checks.

## Explicit boundary

This is a safe preview release, not a production SmartDrag installer. Native activation remains disabled; G1/G2 live
Explorer evidence and G3 codec acceptance are still required before production packaging or user-wide hooks.

# Safe preview verification phase — complete

Date: 2026-09-04

## Outcome

Safe Preview bundles now have an independent, non-executing verifier. It validates the package manifest, exact file
set, file sizes, SHA-256 hashes, safety capability flags, and the embedded 21-check self-check report.

## Implemented

- added `scripts/verify_preview_publish.py`;
- supports both existing bundles where the self-check is metadata-only and new bundles where it is hash-listed;
- rejects unsafe/duplicate manifest paths, unexpected files, missing files, altered bytes, failed self-checks, enabled
  native activation, or a WebP claim before G3;
- wired `scripts/publish-preview.ps1` to run the verifier after writing `manifest.json`;
- added a bounded wait for the published self-check report before manifest generation;
- documented standalone verification for handoff and release operators.

## Verification

- published a fresh framework-dependent `win-x64` preview bundle;
- publisher-integrated verifier passed: 24 files, self-check 21/21;
- verifier passed against the retained 23-file 21-check bundle;
- verifier correctly rejected the retained pre-21-check bundle;
- no executable is launched by the standalone verifier.

## Scope boundary

This verifies package integrity and declared preview safety only. It does not promote the package to production, prove
Explorer coexistence, or resolve the G1/G2/G3 gates.

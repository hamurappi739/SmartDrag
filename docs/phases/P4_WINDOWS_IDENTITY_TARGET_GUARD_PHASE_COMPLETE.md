# P4 Windows identity target guard phase — complete

Date: 2026-09-04

## Outcome

The Windows generated-artifact identity service now rejects directories and reparse-point targets at the native-handle
boundary. A path changed from a regular generated file into a directory or reparse point cannot be treated as a
deletable artifact.

## Implemented

- reads `FILE_BASIC_INFO` from the same handle used for identity operations;
- rejects directory/reparse targets during identity capture;
- refuses directory/reparse targets during delete verification (as `IdentityMismatch` when the target can be opened,
  otherwise as a safe failure before disposition);
- preserves handle-bound identity comparison and `FILE_DISPOSITION_INFO` deletion;
- adds tests for directory capture, directory replacement, missing strong identity, and unknown identity schemes.

## Verification

- `SmartDrag.Windows.Tests`: 18 passed, 0 failed;
- no path-only deletion fallback was introduced.

## Scope boundary

Real symlink/junction, sharing-violation, read-only, and filesystem qualification on the supported Windows matrix
remains required before P4 production acceptance.

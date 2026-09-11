# Recovery journal integrity phase — complete

Date: 2026-09-04

## Outcome

The durable JSON recovery journal now validates the complete document shape before it can authorize startup cleanup.
Corrupt or ambiguous documents fail closed instead of being interpreted as recovery authority.

## Implemented

- bounded temporary-path length (`32,768` characters) on journal writes and reads;
- validation of the entries collection, non-empty reservation ids, fully-qualified paths, and non-default timestamps;
- duplicate reservation-id rejection;
- preservation of the existing revision, byte-size, pending-copy, and version checks;
- regression tests for duplicate ids, missing timestamps, and overlong paths.

## Verification

- `SmartDrag.Infrastructure.Tests`: 39 passed, 0 failed;
- malformed/ambiguous JSON remains on disk for diagnosis and never authorizes deletion;
- valid pending copies continue to promote correctly;
- no wildcard scanning or new deletion authority was introduced.

## Scope boundary

The real Windows reparse-point, sharing-violation, and filesystem-specific P4 matrix remains pending. This phase only
hardens deterministic journal parsing and authority boundaries.

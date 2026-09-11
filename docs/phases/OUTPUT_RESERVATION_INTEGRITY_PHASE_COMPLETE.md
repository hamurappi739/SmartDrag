# Output reservation integrity phase — complete

Date: 2026-09-04

## Outcome

`PhysicalOutputManager` now treats an `OutputReservation` as an authority-bearing contract rather than trusting
callers to have obtained it from `ReserveAsync`. `CommitAsync` and `AbandonAsync` fail closed when a reservation is
malformed, points at the source, mixes directories, or carries a temporary path that is not bound to its reservation
identifier.

The guard is deliberately narrow: it does not broaden output locations, change collision policy, or inspect arbitrary
filesystem content. It only prevents a foreign path from being moved or deleted through the output manager.

## Implemented

- shared reservation validation for commit and cleanup;
- required-field and path normalization checks;
- source-preservation checks for temporary/final paths;
- same-directory and collision-base consistency checks;
- reservation-id marker check for `.smartdrag-{id}.partial` temporary names;
- explicit configured-directory validation;
- regression tests proving foreign temporary files remain untouched;
- regression test proving a source-targeting reservation cannot overwrite the original.

## Verification

- `SmartDrag.Infrastructure.Tests`: reservation integrity tests pass;
- full solution build/test remains the required G0 gate;
- no native activation, codec capability, or deletion authority was enabled by this phase.

## Scope boundary

This closes the deterministic contract-hardening slice only. The broader Windows filesystem matrix (including real
reparse-point and sharing-violation execution) remains part of the pending P4 evidence work.

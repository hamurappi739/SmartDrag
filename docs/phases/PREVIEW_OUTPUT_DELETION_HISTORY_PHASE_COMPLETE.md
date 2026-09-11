# Preview output-deletion history phase

Status: closed.

This phase completes the user-facing lifecycle of the safe generated-output deletion command. The preview now keeps a
small, privacy-safe history entry after a successful identity-checked delete and marks that entry as `output deleted`.
The marker survives restart through `PreviewHistoryStore`, is shown in the active language, and does not expose the
absolute source or output path.

## Delivered

- added the persisted `OutputDeleted` state to preview history entries;
- propagated the state back onto live queue snapshots during history refresh, so a current terminal snapshot cannot erase
  the deletion marker;
- updated the history row to replace the stale output name with a localized deleted-result marker;
- updated the delete command flow to persist the marker only after the presentation authority reports success;
- retained the existing runtime at-most-once/identity checks, so a second delete is not offered or executed;
- added a persistence round-trip regression test for the new state.

## Verification

- `dotnet build .\SmartDrag.sln -c Debug --no-restore --verbosity minimal`: PASS, 19 projects, 0 warnings, 0 errors;
- `dotnet test .\SmartDrag.sln -c Debug --no-restore --verbosity minimal`: PASS, 163 tests, 0 failures;
- `scripts/validate_repo.py --allow-build-artifacts`: PASS;
- preview self-check: PASS, 21/21 checks.

This closes only the local preview history presentation. G1/G2 Explorer evidence, G3 codec acceptance, and native
activation remain unchanged and intentionally blocked by their existing gates.

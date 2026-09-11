# Preview input-state resilience phase

Status: closed.

The Preview now treats a new file as an explicit state transition instead of layering inspection on top of stale
selection state. Before qualification, the previous thumbnail, filename, metadata, authoritative payload, result
buttons, and completion state are cleared or dismissed safely. A failed dismissal blocks the new input rather than
allowing two visible states to compete.

## Delivered

- added a toolkit-neutral input transition policy covering busy, active drag, visible completion, invalid payload, and
  single-file inspection states;
- centralized clearing of stale selected-image state and restored the empty placeholder on all rejection paths;
- prevented a failed completion dismissal from admitting a new file;
- kept the existing overlay/session/runtime authorities as the only commit path;
- added six transition-policy regression cases.

## Verification

- dotnet build SmartDrag.sln: PASS, 19 projects, 0 warnings, 0 errors;
- dotnet test SmartDrag.sln: PASS, 185 tests, 0 failures;
- scripts/validate_repo.py --allow-build-artifacts: PASS;
- preview self-check: PASS, 21/21 checks.

This phase changes only Preview input-state presentation and admission sequencing. It does not enable native
activation or alter G1/G2/G3 safety gates.

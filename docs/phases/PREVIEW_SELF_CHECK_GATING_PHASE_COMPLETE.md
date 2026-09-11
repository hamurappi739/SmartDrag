# Preview self-check gating phase

Status: closed.

Self-check now participates in the same explicit Preview state model as image input:

- a check can start only when no picker, inspection, drag session, operation, completion, or other check is active;
- attempting to run it during a busy state gives a localized explanation instead of replacing the active progress/status;
- the running flag survives asynchronous execution and is cleared through the same availability projection on success or
  failure;
- the existing bounded aggregate report reader and safe report-folder action remain unchanged.

This prevents a diagnostic run from visually clobbering an active image operation and keeps self-check a read-only,
presentation-level diagnostic capability.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 64 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


# Preview overlay lifecycle phase — complete

Status: closed.

The WPF action overlay now has an explicit teardown boundary:

- `SourceInitialized` is detached using the exact handler instance that was attached;
- dispatcher-side close work is guarded when the UI dispatcher is already shutting down;
- an overlay disposal race cannot surface a late dispatcher exception to the process;
- the existing non-activating window style, explicit action geometry, and action authority are unchanged.

This phase only hardens UI resource lifetime. It does not enable native activation, change drag semantics, or add a
codec capability.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full solution tests: PASS, 230 tests, 0 failures;
- static repository validator with generated artifacts allowed: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

# Preview drop availability phase

Status: closed.

Drag feedback now reflects the same availability rules as the picker and self-check controls:

- a file-drop effect is advertised only for a real file payload while Preview is idle;
- inspection, picker, active drag, and operation states no longer show a misleading highlighted drop-zone;
- unsupported clipboard/data payloads remain rejected by the existing qualification path;
- drop rejection is visualized immediately instead of after the pointer is released;
- the policy is toolkit-neutral and covered by presentation regression tests.

This keeps drag/drop feedback honest without changing payload qualification, queue admission, output safety, or native
Explorer gates.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 69 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


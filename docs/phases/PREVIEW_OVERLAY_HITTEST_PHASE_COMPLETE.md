# Preview overlay hit-test phase

Status: closed.

Overlay action selection now uses explicit geometry for real action buttons instead of relying on the deepest WPF
visual returned by `InputHitTest`:

- only enabled action-button rectangles participate in resolution;
- decorative brand/payload/text elements are marked non-hit-testable;
- action geometry is translated into overlay coordinates and checked through a toolkit-neutral Core policy;
- invalid or unavailable geometry fails closed;
- overlapping targets resolve deterministically in presentation order;
- action buttons expose UI Automation help text while the overlay remains non-activating;
- the underlying authorization and commit gates are unchanged.

This removes the class of bug where a Grid/dimmer/container is reported under the pointer and the intended action is
not selected. Real Explorer/OLE coexistence evidence remains a separate G1/G2 gate.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full solution tests: PASS, 214 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

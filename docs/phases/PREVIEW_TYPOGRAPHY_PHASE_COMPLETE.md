# Preview typography phase

Status: closed.

Preview and the transient action panel now share the product typography contract:

- `Segoe UI Variable Text` is the preferred UI face with `Segoe UI` fallback;
- text formatting uses display mode with ClearType rendering for stable small-label readability;
- the change applies at both WPF roots, so header, cards, controls, history, and overlay labels do not drift;
- layout rounding and device-pixel snapping remain enabled for geometry.

No command, file, codec, or native activation behavior changed.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full sequential xUnit suite: PASS, 230 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


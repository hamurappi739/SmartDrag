# Preview overlay theme and accessibility phase

Status: closed.

The transient action panel now stays aligned with the main Preview surface when language or theme changes:

- action labels update both visible text and UI Automation Name/HelpText in the active language;
- the overlay brand mark and action icons use the correct Apple accent (`#007AFF` light / `#0A84FF` dark);
- existing non-activating behavior and explicit geometry hit-testing remain unchanged;
- no overlay visual can become an unintended drag/drop target.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full sequential xUnit suite: PASS, 225 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


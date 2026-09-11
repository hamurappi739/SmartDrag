# Preview accessibility live-status phase

Status: closed.

The Preview now exposes its changing state to UI Automation without exposing private paths or adapter details:

- current status is a polite live region;
- operation result is a polite live region;
- indeterminate processing progress has an automation name and live setting;
- names are refreshed with RU/EN language changes;
- existing visible text, keyboard order, and command authority remain unchanged.

This improves screen-reader feedback for inspection, processing, cancellation, completion, and safe failure states.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 69 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


# Preview history display policy phase

Status: closed.

The history sidebar now renders through a small, known display vocabulary:

- known terminal states and action identifiers remain localized as before;
- unknown/tampered states become a neutral `Operation` label;
- unknown/tampered action identifiers become `SmartDrag action`;
- source and output values are reduced to bounded file names at render time, even if an old JSON record bypassed storage normalization;
- row tooltips use the same sanitized source name and cannot reintroduce a private path;
- private paths and arbitrary technical strings cannot become visible history copy.

The policy is toolkit-neutral and complements the bounded, atomic history store. It does not alter history retention, clear-boundary, deletion, or runtime command authority.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory warning `NU1900` only);
- full solution tests: PASS, 199 tests, 0 failures;
- history-display privacy and vocabulary tests: PASS;
- static repository validator: PASS;
- headless Preview self-check: PASS, 21 checks.

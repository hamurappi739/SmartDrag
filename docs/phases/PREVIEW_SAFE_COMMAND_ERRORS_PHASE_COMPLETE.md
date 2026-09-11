# Preview safe command errors phase

Status: closed.

Completion-command failures now have a dedicated presentation policy. The WPF result surface no longer appends adapter-provided error text directly:

- Open, Copy, Delete, and Start Result Drag failures receive short localized messages;
- exception names, HRESULTs, private paths, and other adapter details stay outside normal UI text;
- an unexpected command shape falls back to one generic safe message;
- missing errors produce no extra line in the result.

Runtime error details remain available to the process diagnostics boundary, while the user-facing result remains readable and privacy-safe.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory warning `NU1900` only);
- full solution tests: PASS, 195 tests, 0 failures;
- command-error privacy regression tests: PASS;
- static repository validator: PASS;
- headless Preview self-check: PASS, 21 checks.

No runtime authority, output deletion, codec selection, or native activation was changed.

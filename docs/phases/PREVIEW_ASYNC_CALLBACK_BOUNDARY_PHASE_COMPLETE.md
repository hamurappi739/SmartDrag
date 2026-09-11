# Preview async callback boundary phase

Status: closed.

Remaining WPF `async void` event boundaries now fail closed:

- keyboard gestures catch unexpected drag/session and command failures;
- drop callbacks catch unexpected data-object/read failures;
- failures become localized user-safe status and bounded process-log entries;
- existing specialized clipboard messages remain intact;
- normal cancellation and current runtime authority are not changed.

This prevents an exceptional UI callback from escaping into the dispatcher and repeating the earlier “window disappears after
some idle time” failure mode.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 69 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


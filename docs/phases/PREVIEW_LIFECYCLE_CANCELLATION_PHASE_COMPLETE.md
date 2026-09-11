# Preview lifecycle cancellation phase — complete

Status: closed.

The Preview now treats window shutdown as an explicit cancellation boundary for every asynchronous UI callback that
can outlive the initiating gesture:

- action commit uses the shared preview lifetime token and exits quietly when the window is closing;
- completion dismissal uses the same token instead of an uncancellable wait;
- picker completion checks the lifecycle boundary before routing a selected path into the input pipeline;
- self-check completion does not write to a closing window and records unexpected failures in the bounded process log;
- report-folder shell failures are recorded without leaking adapter details to the user;
- expected shutdown cancellation is not rendered as a false action or completion error.

No runtime authority, native activation, codec capability, output policy, or destructive boundary was changed.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 69 tests, 0 failures;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

# Preview transient command status phase

Status: closed.

The same privacy boundary now covers transient status messages produced after the user presses Copy, Open folder, Delete, or Dismiss. Even when a lower adapter returns a user-message string, the Preview replaces it with the command-specific safe copy before rendering it.

This keeps the result card and its follow-up status chip consistent: no command path can bypass the safe completion-error policy through a secondary status assignment.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory warning `NU1900` only);
- full solution tests: PASS, 197 tests, 0 failures;
- static repository validator: PASS;
- headless Preview self-check: PASS, 21 checks.

No runtime authority, output deletion, codec selection, or native activation was changed.

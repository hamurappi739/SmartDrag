# Preview process resilience phase

Status: closed.

The safe Preview host now records a bounded lifecycle/error trail and protects the application boundary from a rare exception escaping `Application.Run`:

- startup and normal exit events are recorded;
- dispatcher, AppDomain, and unobserved-task exceptions continue to be contained and are logged;
- an exception escaping the WPF run loop is logged and returns a controlled non-zero exit code;
- the local log is capped at 256 KiB and rotated to one `.1` backup;
- individual details are capped at 8 KiB so a huge exception cannot grow the file without bound;
- all log writes remain best effort and never become a second process failure.

The log is stored under the existing per-user SmartDrag local-data directory. No UI text exposes exception details or absolute paths.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (NuGet vulnerability-feed warning `NU1900` only);
- full solution tests: PASS, 191 tests, 0 failures;
- process-log rotation and input validation tests: PASS;
- static repository validator: PASS.

This phase does not enable native activation or change runtime/image/deletion authority.

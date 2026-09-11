# P5 — Presentation / Runtime Wiring Test Matrix

Status in v0.9: **SAFE PREVIEW SELF-CHECK PASSED; PRODUCTION MANUAL MATRIX PENDING G1/G2**.

## Automated tests prepared

- default MVP settings validate;
- `PreserveSource=false` is rejected;
- invalid delay / non-finite overlay offset are rejected;
- production output policy is always non-destructive;
- running operation uses indeterminate progress;
- presentation contains only file display name, not full path;
- Cancelling disables repeated Cancel intent;
- measured compression savings are shown only from actual metrics;
- failure projection uses user-safe error text, not technical text;
- completion FIFO promotes the next item after Dismiss;
- duplicate completion does not duplicate a toast;
- completion-command failure is shown as user-safe inline error;
- invalid settings cannot prepare/commit a production drag;
- production graph does not expose JobQueue/ActionExecutor/CompletionCommandExecutor to the application-facing API.
- preview self-check cancels both a running and a queued operation while preserving source/output safety.

## Manual UX verification after G0/G1/G2

1. Commit an image action: OLE overlay disappears before processing begins.
2. Status chip appears without focus activation.
3. Cancel queued/running work and verify the source remains unchanged.
4. Start a second action while the first runs: first stays visible, queued-behind count increments.
5. Complete two jobs without dismissing: completions appear FIFO rather than overwriting each other.
6. Trigger a codec error: no full source path or technical exception is displayed.
7. Trigger clipboard busy: inline failure appears and processing/runtime remains healthy.
8. Verify no numeric progress percentage is displayed unless a future real progress contract is active.
9. Verify keyboard/accessibility path without activating the original drag overlay unexpectedly.

## Gate

P5 may be marked accepted only after G0 build/tests pass and the manual checks above are run on the chosen production
presentation implementation.

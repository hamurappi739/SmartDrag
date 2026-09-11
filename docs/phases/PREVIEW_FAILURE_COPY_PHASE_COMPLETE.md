# Preview failure copy phase

Status: closed.

All lower-layer preview failures now cross into the visible result through stable user-safe copy:

- failed image inspection/safety qualification no longer displays codec or guard details;
- action-panel preparation failures no longer expose orchestration reasons;
- rejected dispatches no longer echo runtime status strings or private paths;
- the user is told whether an action started and that the original file remains unchanged;
- technical context stays available to the bounded process diagnostics log.

This complements the completion-command error policy and closes the remaining direct reason/detail assignments in the Preview window.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory warning `NU1900` only);
- full solution tests: PASS, 197 tests, 0 failures;
- failure-copy privacy regression tests: PASS;
- static repository validator: PASS;
- headless Preview self-check: PASS, 21 checks.

No runtime authority, deletion capability, codec selection, or native activation was changed.

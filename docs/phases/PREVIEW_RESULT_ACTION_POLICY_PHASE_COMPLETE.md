# Preview result action policy phase

Status: closed.

Result-control visibility is now derived by one toolkit-neutral policy at the presentation boundary. The policy combines the commands granted by the runtime with the current output state:

- Copy, Open folder, and Delete appear only when the generated output still exists;
- a deleted or externally missing output cannot leave stale destructive/action controls visible;
- controls not granted by the runtime are never synthesized by the WPF window;
- Dismiss remains the only safe control for a completion with no usable output commands;
- an unexpected multi-output snapshot fails closed instead of throwing from the renderer.

This removes duplicated visibility decisions from the window and protects repeat input, completion FIFO, delete, and history flows from stale result state.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory warning `NU1900` only);
- full solution tests: PASS, 193 tests, 0 failures;
- result-action policy regression tests: PASS;
- static repository validator: PASS;
- headless Preview self-check: PASS, 21 checks.

No runtime authority, deletion capability, codec selection, or native activation was changed.

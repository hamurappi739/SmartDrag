# Preview window lifecycle and placement phase

Status: closed.

The Preview host now treats persisted window geometry as untrusted input and keeps the application usable after
monitor/layout changes:

- default minimum window size is 720×480, allowing the content ScrollViewer to handle compact screens;
- layout rounding and device-pixel snapping are enabled for stable WPF rendering;
- window coordinates and dimensions are persisted alongside the existing language/theme preferences;
- invalid, non-finite, or extreme geometry is discarded during preference normalization;
- off-screen or partially lost geometry is centered or clamped against the current work area with at least 25% visible;
- the close path records an explicit user-requested lifecycle event;
- fire-and-forget queue disposal and cross-dispatcher rendering are observed through `SafeTaskRunner`;
- delayed background failures are logged and can be surfaced by a callback instead of becoming unobserved task failures;
- the empty-state plus button exposes a localized UI Automation help text.

This phase does not enable native activation, change output authority, or expose technical paths in the UI.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full solution tests: PASS, 205 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

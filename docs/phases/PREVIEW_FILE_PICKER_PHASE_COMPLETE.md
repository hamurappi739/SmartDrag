# Preview file-picker phase

Status: closed.

The Preview empty state now has a real, owner-bound file-picker boundary instead of constructing the WPF dialog inside
the click handler:

- `IPreviewFilePicker` keeps the presentation flow independent from WPF dialog construction;
- the WPF adapter opens `OpenFileDialog` with the Preview window as owner, so the dialog cannot hide behind the app;
- title, filter, and initial directory are localized/configured at the Preview boundary;
- cancel is a normal no-op and picker failures become localized safe status plus bounded diagnostics;
- picking is rejected while input inspection, an active drag, or an operation is in flight;
- the selected path is still routed through the existing asynchronous inspection and queue authority;
- the same path is available from the visible button and `Ctrl+O`, preserving keyboard and UI-automation flows.

The picker boundary does not broaden file, codec, or native activation authority. It only supplies a local image path to the
existing guarded input pipeline.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- sequential solution tests: PASS, 218 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.


# SmartDrag MVP Presentation Specification — v0.9

## Purpose

This document defines the toolkit-neutral user-facing behavior between an accepted native drag and the end of a
SmartDrag operation. It does **not** choose WPF, WinUI, DirectComposition, or another rendering stack.

The canonical interaction remains:

`drag file -> choose action -> result`

The UI must stay quiet unless it has something actionable to show.

## Surface 1 — drag action overlay

The drag overlay is the only surface shown while the native Explorer drag is still alive.

Rules:

1. It appears only after strict preflight qualification and timing/travel thresholds.
2. It is non-activating and must not steal Explorer focus.
3. It contains only actions authorized by the exact `PreparedOverlaySession`.
4. It is offset from the cursor and never used as a global transparent drag interceptor.
5. Dropping on an action consumes the drag authorization before dispatch.
6. After commit, the OLE overlay disappears immediately. File processing never runs inside the native drag loop.
7. Ending/cancelling the native drag hides the overlay and invalidates the capability.

## Surface 2 — operation status

After a SmartDrag action is accepted, the drag overlay is gone and a separate compact status surface may appear.
This surface is **not** an OLE drop target.

MVP contents:

- action label;
- safe file display name only (never the full path);
- one of `Waiting…`, `Working…`, `Cancelling…`;
- indeterminate progress indicator;
- Cancel button only while Queued or Running;
- queued-behind count when more work is pending.

### No fake progress

The foundation does not expose a numeric percentage because the codec contract cannot yet prove meaningful progress.
A UI must not invent percentages based on elapsed time or file size. Numeric progress can only be added after a real
processor progress contract is designed and tested.

## Surface 3 — completion

Terminal results are projected one at a time in FIFO order. A newer result must not silently erase an older completion
that the user has not yet seen.

Success examples:

- `Compressed` + measured bytes saved when metrics exist;
- `Converted to WebP`;
- `Metadata removed`.

Failure rules:

- show only `AppError.UserMessage`;
- never show technical exception text, HRESULT details, or full paths;
- remind the user that the original file was not changed when appropriate.

Cancellation is neutral, not an error.

## Completion commands

Commands are not invented by the view. The view renders exactly the commands in `CompletionPresentationModel.Commands`.
Execution returns to `MvpPresentationCoordinator`, which routes through `ICompletionCommandExecutor`.

- `OpenContainingFolder`: capability-gated.
- `CopyResultPath`: capability-gated and requires a real owner HWND in the current Windows adapter.
- `StartResultDrag`: disabled until a production OLE drag-source exists.
- `DeleteGeneratedOutput`: only identity-safe generated artifacts; destructive authorization is consumable.
- `Dismiss`: local presentation intent.

While a completion command is executing, another command for the same toast is rejected. Errors are projected inline
from the user-safe error message.

After successful generated-output deletion, the toast becomes `Generated file deleted`, exposes only Dismiss, and
explicitly states that the original file was not changed.

## Concurrency model

SmartDrag may accept another drag while a previous job is processing. The job queue remains sequential in MVP.
Presentation shows the oldest non-terminal job and records how many additional jobs are queued behind it.

Completion notifications form their own FIFO queue and do not block processing.

## Dismissal / timing

v0.9 deliberately does **not** hard-code automatic timeout behavior. Completion commands are actionable, and a timer
chosen without usability evidence can make the product feel unreliable. Auto-dismiss policy is a later UX tuning
item and must preserve keyboard/accessibility interaction.

## Privacy

Presentation models must prefer file names over full paths. Raw full paths remain inside runtime/command boundaries
where they are needed to execute the user's requested operation.

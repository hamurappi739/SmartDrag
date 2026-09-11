# Future Cursor Entry Protocol

Status: **NOT ACTIVE YET**

This file exists so SmartDrag can later be handed to a coding agent without reconstructing project intent from chat history.

## Trust order

When documents conflict, use this order unless the human explicitly changes it:

1. `docs/canonical/SMARTDRAG_MASTER_CONTEXT.md` — product canon.
2. `PROJECT_STATE.md` — current gate/build/evidence state.
3. Accepted ADRs in `docs/decisions/` — architectural decisions.
4. `docs/handoff/SMARTDRAG_FUTURE_CURSOR_HANDOFF.md` — accumulated implementation context.
5. Focused architecture/testing/research documents.
6. Source comments.

A Provisional ADR or open question is not permission to invent an answer.

## First agent session — mandatory sequence

1. Read the trust-order documents above.
2. Run the repository build/test gate before editing features.
3. If compilation fails, fix only defects necessary to restore the documented architecture; do not redesign features opportunistically.
4. Record actual build/test results in `PROJECT_STATE.md`.
5. Do not work past the active hard gate unless its evidence is present.

## Hard prohibitions

Do not:
- add cloud file upload;
- add destructive in-place processing by default;
- inject code into Explorer;
- claim global OLE payload visibility that Windows does not provide;
- return `DROPEFFECT_MOVE` for MVP actions;
- pick a production overlay framework before P0 evidence;
- add ImageSharp, SkiaSharp, FFmpeg, PDF, archive, or video packages just because they appear in research notes;
- silently resolve an `OQ-*` item;
- widen the MVP to batch/PDF/video/settings polish while a preceding gate is open;
- bypass `DragInteractionCoordinator` / `DragWorkflowOrchestrator` by constructing `ActionRequest` in UI/Win32 code;
- make output recovery optional or scan user directories for SmartDrag-looking partial files;
- continue production startup when `StartupSafetyBootstrap.RuntimeMayStart` is false;
- replace ADR-034 identity-safe Delete with path-only/timestamp/size/hash-then-DeleteFile logic;
- wire a concrete image codec directly to action handlers instead of `GuardedImageProcessor`;
- set `ProductionActivationPolicy.NativeActivationEnabled=true` merely to demonstrate UI/native hooks;
- expose Start Result Drag before a dedicated production drag-source proof passes.
- expose raw JobQueue/ActionExecutor/CompletionCommandExecutor to UI simply for convenience;
- add an OutputPolicy parameter back to the application-facing production drag commit;
- fabricate numeric processing percentages without a real processor progress contract;
- display AppError.TechnicalMessage or full source paths in normal transient presentation.

## Change discipline

Any change that alters an accepted invariant, dependency direction, safety policy, or platform strategy requires a new ADR or explicit amendment to an existing ADR.

Implementation should remain small and testable. Every new production module needs:
- contract/ownership clarity;
- tests for failure/cancellation where relevant;
- no hidden source-file mutation;
- state/evidence update.

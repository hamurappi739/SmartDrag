# Production Drag → Job → Completion Orchestration v2

## Purpose

This is the production contract between G2 drag qualification, the transient overlay, job runtime, durable output safety, and completion. It deliberately excludes the unresolved mechanism used to obtain Explorer preflight evidence and the unselected G3 codec implementation.

## Flow

```text
native drag signal
  ↓
preflight evidence provider
  ↓
MvpPayloadQualifier
  ↓ Eligible only
DragInteractionCoordinator.TryPresentAsync
  ↓
DragWorkflowOrchestrator.PrepareOverlay
  ↓
PreparedOverlaySession
  - DragSessionId
  - exact preflight qualification
  - frozen OfferedActionIds
  - OverlayModel
  ↓
overlay shown; coordinator owns ONE active capability
  ↓
user enters action target and Drops
  ↓
OLE CF_HDROP authoritative snapshot
  ↓ Supported only
DragInteractionCoordinator.TryCommitAsync
  ↓ consumes/clears active capability BEFORE async dispatch
DragWorkflowOrchestrator.TryCommitDrop
  ├─ preflight path mismatch → reject
  ├─ action not offered → reject
  ├─ action no longer available → reject
  ├─ PreserveSource=false → reject
  └─ accepted → ActionRequest(RequestId = DragSessionId)
                      ↓
             CommittedActionDispatcher
                      ↓ idempotency
                   JobQueue
                      ↓ idempotency + cancellation
               ActionExecutor
                      ↓
                image handler
                      ↓
   durable recovery journal BEFORE reservation returns
                      ↓
  same-directory reservation-bound partial → codec → commit
                      ↓
               terminal JobSnapshot
                      ↓
              CompletionCoordinator
                      ↓ at most once per JobId
    CompletionProjector + concrete capabilities
                      ↓
              CompletionModel / UI
                      ↓
             CompletionCommandExecutor
              ├─ Open folder
              ├─ Copy result path
              ├─ Start result drag (GATED)
              ├─ Delete output (SAFETY GATED)
              └─ Dismiss
```

## Authorization invariants

1. Merely observing a native drag grants no permission to process a file.
2. Merely showing an overlay grants no permission to process a file beyond the frozen offered-action capability.
3. `IDropTarget::Drop` never constructs an arbitrary job from raw UI state.
4. A selected ActionId must be one captured in the prepared overlay session.
5. Authoritative Drop payload identity must match the preflight identity that caused the overlay to appear.
6. Capability changes between overlay show and Drop fail closed.
7. MVP commit refuses destructive output policy even if a caller passes one.
8. One coordinator owns one active prepared session; a newer drag supersedes stale authority.
9. Commit consumes the active capability before async hide/dispatch, so duplicate delivery cannot reuse it.
10. Only an accepted commit may be dispatched to `IJobQueue`.
11. Dispatcher and JobQueue independently enforce RequestId idempotency.

## Output/recovery invariants

1. `PhysicalOutputManager` cannot be constructed without an `IOutputRecoveryJournal`.
2. A recovery record is durable before the processor receives a temporary path.
3. The partial filename embeds the reservation id.
4. Final commit never overwrites an existing file.
5. Startup recovery can delete only exact journaled reservation-bound partials.
6. No user-directory wildcard cleanup is permitted.
7. An unclean recovery report blocks runtime startup.

## Completion invariants

1. Completion is projected only from a terminal immutable job snapshot.
2. `CompletionCoordinator` emits at most one completion per JobId.
3. Commands are offered from concrete runtime capabilities, not from the enum alone.
4. Command execution revalidates current terminal snapshot + projected availability.
5. Delete Generated Output is disabled until strong current file identity exists.
6. Start Result Drag is disabled until a production drag-source proof exists.

## Why this matters for Cursor handoff

A future coding agent must wire native/UI adapters into this chain instead of putting business rules inside `WndProc`, `IDropTarget`, completion buttons, or codec callbacks. Any shortcut that creates `ActionRequest` directly, starts processing without a recovery reservation, or exposes a gated completion command is an architecture violation.

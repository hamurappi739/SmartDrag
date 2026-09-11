# Presentation State and Intent Model — v0.9

## Independent state axes

The product must not be represented as one giant enum because a new drag, a running job, and an older completion can
coexist. The production UI therefore has independent surfaces:

```text
Native drag axis       : Hidden | ActionOverlayVisible
Operation axis         : None | Queued | Running | Cancelling
Completion axis        : None | CompletionVisible(+ pending FIFO)
```

The `SmartDrag.Presentation` project currently owns the operation and completion axes. Native drag overlay lifetime
remains owned by `DragInteractionCoordinator` / `IOverlayService` because it participates in OLE authorization.

## User intents

### Choose action

Authority: `IProductionDragInteraction.TryCommitMvpAsync`.

The UI supplies only:

- drag session ID;
- selected `ActionId` that was offered;
- authoritative OLE qualification;
- current capability/settings snapshots.

The UI cannot supply `OutputPolicy`.

### Cancel

Authority: `IJobQueue.TryCancel`, reachable to the view only through
`MvpPresentationCoordinator.TryCancelVisibleOperation`.

The presentation snapshot exposes `CanCancel=false` once the job enters Cancelling.

### Completion command

Authority: `ICompletionCommandExecutor`, reachable to the view only through
`MvpPresentationCoordinator.ExecuteVisibleCompletionCommandAsync`.

The coordinator checks that the command belongs to the currently visible completion before calling Runtime.

### Dismiss completion

This is presentation-local and advances the FIFO completion queue. It does not mutate the job or filesystem.

## Event boundary rule

`JobChanged`, `CompletionReady`, and `SnapshotChanged` observers are no-throw boundaries. A UI observer exception may
lose a visual refresh but may not destabilize processing or alter file safety.

`SnapshotChanged` is raised outside the coordinator lock to avoid holding runtime state while arbitrary UI code runs.

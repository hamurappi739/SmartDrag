# Production Composition Root

## Purpose

`ProductionRuntimeGraph` is the only intended wiring path for the deterministic SmartDrag runtime.

It accepts a clean `StartupSafetyBootstrapResult` and injected unselected implementations:

- production `IOverlayService`;
- concrete codec `IImageProcessor`;
- its `IImageInspector`;
- benchmark-approved `ImageSafetyLimits`;
- optional completion owner HWND.

It then constructs all safety-critical wrappers and orchestrators itself.

## Required graph

```text
StartupSafetyBootstrapResult (Acquired only)
        ↓
StartupSafetyLease / same JsonOutputRecoveryJournal
        ↓
WindowsGeneratedArtifactIdentityService
        ↓
PhysicalOutputManager
        ↓
GuardedImageProcessor ← inspector + codec + safety limits
        ↓
3 MVP image handlers
        ↓
ActionExecutor
        ↓
SequentialJobQueue
        ↓
DragWorkflowOrchestrator
        ↓
CommittedActionDispatcher
        ↓
DragInteractionCoordinator ← injected overlay

JobQueue
  ├─> CompletionCommandExecutor
  │      ├─ WindowsCompletionPlatformService
  │      └─ identity-safe deletion service
  └─> CompletionCoordinator
```

## Native activation boundary

Constructing the deterministic graph is not permission to install global drag hooks.

`ProductionActivationPolicy.NativeActivationEnabled` remains `false` while G0/G1/G2/G3 are unresolved. Future activation code must call the policy before registering WinEvent/OLE integration.

A coding agent must not change this constant merely to demonstrate a UI.

## Disposal

`ProductionRuntimeGraph.DisposeAsync` detaches completion observation, stops/disposes the job queue, then disposes the startup lease. This keeps the recovery journal alive until background output work is no longer possible.

## Transactional graph creation

`CreateDeterministicRuntimeAsync` takes ownership of the clean startup lease. If graph construction throws after the queue has been created, it detaches any completion coordinator, asynchronously disposes the queue, disposes the journal/mutex lease, and rethrows. A half-composed runtime must not leave a background worker or single-instance lock orphaned.


## v0.9 application-facing surface

The runtime graph intentionally hides JobQueue, DragWorkflow, CompletionCoordinator and CompletionCommandExecutor. The application-facing graph exposes `IProductionDragInteraction`, `MvpPresentationCoordinator`, `IFilePayloadSnapshotFactory`, and `AppCapabilities`. This is an API-level scope/safety barrier, not merely documentation.

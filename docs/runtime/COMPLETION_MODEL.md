# Completion Model

Status: **ACCEPTED FOUNDATION — v0.8**

Completion is a projection of a terminal job; it is not a callback from an image codec or output manager.

```text
Action handler
  -> ActionResult + ActionMetrics + GeneratedArtifact[]
  -> SequentialJobQueue
  -> immutable terminal JobSnapshot
  -> CompletionCoordinator (at-most-once terminal projection)
  -> CompletionProjector + runtime CompletionCapabilities
  -> CompletionModel
  -> UI
```

## Capability rule

The enum lists possible product commands; it does not grant UI availability. `CompletionProjector` intersects terminal state with concrete runtime capabilities and current output facts.

Current foundation target:

- Open containing folder — available through Windows adapter when a single output exists;
- Copy result path — available only when composition supplies a real non-zero clipboard owner HWND;
- Start result drag — **disabled** until a production OLE drag-source proof exists;
- Delete generated output — available only when an identity-safe deletion service is composed **and** exactly one generated output carries strong identity;
- Dismiss — always available.

Failure/cancellation currently expose only Dismiss.

## Generated artifact model

Historical generated-output ownership is represented by `GeneratedArtifact`, not a bare path list:

```text
GeneratedArtifact
  Path
  Identity?
    Scheme
    VolumeId
    ObjectId
```

`Identity=null` means SmartDrag created/committed an output but cannot later prove that the same filesystem object is still at that path. Destructive commands are therefore suppressed.

On Windows, ADR-034 defines `windows-file-id-v1` as `VolumeSerialNumber + FILE_ID_128`.

## Execution rule

`CompletionCommandExecutor` re-reads the current immutable terminal snapshot and re-projects allowed commands before execution. A UI-retained command cannot execute merely because a button was once visible.

Delete is not delegated to a generic platform path delete. It is routed to `IGeneratedArtifactDeletionService`, which must verify identity and delete using the same opened object handle.

## TOCTOU rule

Path equality is never destructive authority.

Strong identity is retained only if the identity of the temporary object before final move equals the identity observed at the committed final path. At later deletion, the current path is opened without following reparse points, identity is compared, and disposition is applied to that same handle.

## Destructive command lifetime

`DeleteGeneratedOutput` is consumable per `JobId` (ADR-037). Concurrent/repeated execution after success or a terminal identity outcome is rejected without a second deletion-service call. Cancellation and generic transient native/access failures may release the attempt marker for retry.

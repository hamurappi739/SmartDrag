# SmartDrag Architecture Foundation v1

## Non-negotiable invariants

1. Native Windows drag/drop has priority over SmartDrag.
2. SmartDrag fails open with respect to the native drag.
3. SmartDrag fails safe with respect to user files.
4. Source preservation is the default.
5. Processing is local-only.
6. Overlay presentation does not imply ownership of the native drag.
7. Processing does not run inside the live OLE drag loop.
8. Domain contracts do not expose Win32/UI/codec implementation types.
9. Production overlay framework is not selected before P0 interop proof.
10. Coding agents may not widen scope without an explicit decision.

## Runtime boundaries

```text
Windows/Explorer
   -> WinEvent drag hint
   -> DragCoordinator
   -> compact overlay (passive, offset)
   -> user intentionally enters overlay
   -> IDropTarget::DragEnter(IDataObject)
   -> authoritative CF_HDROP payload
   -> selected ActionId
   -> Drop
   -> immutable ActionRequest
   -> job/processor pipeline
   -> safe temporary output
   -> final output commit
   -> completion UI
```

## Dependency rule

`SmartDrag.Core` is the dependency floor. It contains domain types, policies, contracts, and pure state transitions only.

`SmartDrag.Windows`, `SmartDrag.Actions`, `SmartDrag.Imaging`, `SmartDrag.Infrastructure`, and future UI implementations may depend on Core. Core must not depend on them.


## Runtime foundation added in v0.4

`SmartDrag.Runtime` owns post-commit job lifecycle only. It depends on `SmartDrag.Core`, not Windows or UI.

`SmartDrag.Imaging` now owns the three image `IActionHandler` orchestrators, but not a concrete codec. They depend only on Core contracts (`IOutputManager`, action contracts) plus `IImageProcessor`.

`SmartDrag.Infrastructure` owns `PhysicalOutputManager`, including safe naming, partial artifacts, second collision check, and non-overwriting final move.

Dependency direction remains:

```text
Core <- Actions
Core <- Runtime
Core <- Imaging
Core <- Infrastructure
Core <- Windows
Core <- Overlay

App -> composition of all concrete modules
```

## v0.8 composition/identity refinement

New cross-layer rule:

```text
Core.Artifacts
   ↑                 ↑
Infrastructure       Windows.Artifacts
(output commit)      (FILE_ID_INFO / handle delete)
   ↑                 ↑
Imaging handlers -> Runtime completion
        \             /
         App.Composition
```

`SmartDrag.Core` remains platform-independent: it defines opaque artifact identity/deletion contracts but contains no Win32 structures.

`SmartDrag.Infrastructure` may request an identity provider but never knows how Windows identity is represented.

`SmartDrag.Windows` owns the platform implementation and destructive handle boundary.

`SmartDrag.Runtime` may offer Delete only from identity-backed terminal job facts.

`SmartDrag.App.Composition` is the only intended production wiring layer and must inject the same Windows identity service into both output commit and completion deletion.


## v0.9 presentation boundary

`SmartDrag.Presentation` is now the toolkit-neutral boundary between Runtime and a future UI implementation. It may observe `IJobQueue`/`CompletionCoordinator` internally during composition, but the view receives only immutable `SmartDragPresentationSnapshot` values and intent methods. Native drag overlay authorization remains separate in Orchestration.

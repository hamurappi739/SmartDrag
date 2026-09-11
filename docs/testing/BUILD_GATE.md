# G0 Build Gate

The generated repository has not yet been compiled because the generation environment has no .NET SDK (`dotnet`, `csc`, and `msbuild` are unavailable).

## Required environment

- Windows 11 preferred for P0/G2/P3;
- .NET 8 SDK;
- PowerShell;
- Python 3 for static/corpus validators;
- normal desktop Explorer session (not Windows Server Core / headless) for probe/manual checks.

## Command

```powershell
./scripts/build.ps1
```

## Pass criteria

1. repository static validator passes;
2. image corpus SHA-256 validator passes;
3. codec-neutral image corpus audit passes and writes `artifacts/g3-corpus/audit-latest.json`;
4. `dotnet --info` confirms a .NET 8 SDK;
5. solution restore succeeds;
6. all **19** projects in `SmartDrag.sln` build in Debug;
7. all xUnit tests pass;
8. no unresolved project references;
9. `SmartDrag.Windows.Probe --mode=p0` starts and writes a JSONL log;
10. `SmartDrag.Windows.Probe --mode=g2` starts without COM/PInvoke initialization failure;
11. no production image codec dependency has been introduced as a workaround;
12. no compiler/test error is suppressed by deleting a safety assertion, recovery gate, or ADR contract.

## First compile-risk review targets

Because Windows/.NET compilation is unavailable in the generation container, inspect these first if G0 fails:

### Existing Windows interop

- `src/SmartDrag.Windows/Shell/ExplorerSelectionSnapshotReader.cs` — late-bound `Shell.Application` automation and COM lifetime;
- `src/SmartDrag.Windows/Ole/IDropTargetNative.cs` — COM signature/marshalling;
- `src/SmartDrag.Windows/Ole/FileDropDataReader.cs` — `ComTypes.IDataObject` and `STGMEDIUM` handling;
- `tools/SmartDrag.Windows.Probe/*` — raw Win32 callback/delegate lifetime and message-loop plumbing.

### v0.7 additions

- `src/SmartDrag.Windows/Completion/WindowsCompletionPlatformService.cs` — `LibraryImport`, clipboard ownership transfer, Explorer invocation;
- `src/SmartDrag.Windows/Lifetime/NamedMutexSingleInstanceLease.cs` — dedicated owner-thread lifetime/abandoned mutex behavior;
- `src/SmartDrag.Infrastructure/Recovery/JsonOutputRecoveryJournal.cs` — async JSON, revision/pending promotion, overwrite move behavior;
- `src/SmartDrag.Infrastructure/PhysicalOutputManager.cs` — required durable journal integration;
- `src/SmartDrag.Orchestration/DragInteractionCoordinator.cs` — consumable active capability and async overlay lifecycle;
- `src/SmartDrag.Runtime/Completion*.cs` — capability projection, at-most-once terminal publication, command execution;
- new xUnit sources under Runtime/Infrastructure/Orchestration/Windows tests.

A compile failure in provisional Explorer preflight does not authorize replacing G2 with signal-only overlay behavior. A compile failure in recovery/completion code does not authorize making journaling optional or enabling gated commands. Fix the implementation while preserving accepted ADRs.

## Failure handling

Compiler/interop failures are fixed before manual UX conclusions are drawn. Do not reinterpret a compile failure as permission to widen product scope, weaken recovery/output safety, bypass the authorization chain, or select a concrete codec.

## v0.8 additions to first compile-risk review

- `src/SmartDrag.Windows/Artifacts/WindowsGeneratedArtifactIdentityService.cs` — `SafeFileHandle` `LibraryImport`, `FILE_ID_INFO` layout, `FILE_DISPOSITION_INFO` one-byte BOOLEAN, DELETE/share/reparse flags;
- `src/SmartDrag.Core/Artifacts/GeneratedArtifact.cs` — identity propagation contracts;
- `src/SmartDrag.Infrastructure/PhysicalOutputManager.cs` — pre/post commit identity comparison;
- `src/SmartDrag.Runtime/CompletionProjector.cs` + `CompletionCommandExecutor.cs` — identity-gated Delete;
- `src/SmartDrag.Imaging/GuardedImageProcessor.cs` — mandatory content/resource guard decorator;
- `src/SmartDrag.App/Composition/ProductionRuntimeGraph.cs` — graph ownership and disposal ordering.

G0 must run `tests/SmartDrag.Windows.Tests/Artifacts/WindowsGeneratedArtifactIdentityServiceTests.cs` on Windows. A P/Invoke failure does **not** authorize a path-based deletion fallback; keep Delete unavailable until the handle-bound implementation is corrected.


## v0.9 additions to first compile-risk review

- `src/SmartDrag.Presentation/MvpPresentationCoordinator.cs` — event ordering, locking/reentrancy, completion FIFO, command in-flight state;
- `src/SmartDrag.Presentation/MvpPresentationPolicy.cs` — safe filename/error projection and no fabricated numeric progress;
- `src/SmartDrag.Core/Settings/MvpSettingsPolicy.cs` + `MvpOutputPolicyFactory.cs` — validation and safe output factory;
- `src/SmartDrag.Orchestration/IProductionDragInteraction.cs` / `DragInteractionCoordinator.cs` — production commit no longer accepts arbitrary `OutputPolicy`;
- `src/SmartDrag.App/Composition/ProductionRuntimeGraph.cs` — narrowed public authority and presentation disposal order;
- all `SmartDrag.Presentation.Tests` sources.

A compile failure in this layer does not authorize exposing JobQueue/CompletionCommandExecutor to the view or reintroducing arbitrary output policy as a shortcut.

# Static Review — Foundation v0.8

Build status remains **UNVERIFIED BUILD** because the generation environment has no .NET SDK.

This review records issues intentionally found and corrected before checkpoint packaging.

## 1. Historical generated paths were insufficient authorization

v0.7 correctly disabled Delete because `GeneratedOutputPaths` could not survive path replacement safely.

v0.8 replaces the bare ownership fact with `GeneratedArtifact` + optional strong identity. Delete remains absent whenever identity is unavailable.

## 2. Post-commit-only identity capture still had a race

Capturing `FileId` only after `File.Move` would allow a narrow race where another actor replaces the final path before identity capture, causing SmartDrag to associate the replacement with its output.

Correction:

```text
capture partial identity
  -> move without overwrite
  -> capture final identity
  -> retain only if equal
```

Later deletion independently re-verifies current identity.

## 3. Verify-then-DeleteFile would still be TOCTOU

Opening a path, verifying identity, closing it, then calling `DeleteFile(path)` performs a second path lookup and can delete a replacement.

Correction: Windows deletion verifies and calls `SetFileInformationByHandle(FileDispositionInfo)` on the same open handle.

## 4. Reparse-point replacement must not be followed

Deletion opens the current path using `FILE_FLAG_OPEN_REPARSE_POINT`. If a generated path has become a symbolic link/reparse point, the identity of that object is compared rather than traversing to its target.

## 5. G3 execution guard existed but was bypassable by composition

The v0.6 policy contracts did not force handlers to use `MvpImageExecutionGuard`.

Correction: `GuardedImageProcessor` becomes the mandatory production decorator. The production composition root wires handlers only to the guarded processor.

## 6. Multiple valid composition paths would undermine invariants

The repository now has one intended `ProductionRuntimeGraph`. It composes the same recovery journal, output manager, guarded image processor, orchestration, job queue, identity service, and completion enforcement.

Native hooks are still not registered by that graph. `ProductionActivationPolicy.NativeActivationEnabled=false` is an explicit hold until empirical gates are accepted.

## 7. Remaining compile-risk items for G0

First Windows/.NET 8 build should pay particular attention to:

- `LibraryImport` signatures using `SafeFileHandle` and nested blittable `FILE_ID_INFO` structs;
- `SetFileInformationByHandle` marshalling for one-byte `FILE_DISPOSITION_INFO.DeleteFile`;
- `CreateFileW` long-path/reparse/share semantics;
- new `GeneratedArtifact` propagation through action -> queue -> completion tests;
- `ProductionRuntimeGraph` ownership/disposal ordering;
- all existing WinEvent/OLE and clipboard `LibraryImport` signatures.

No static review is evidence that these signatures compile or behave correctly. G0 remains mandatory.

# Completion Command Execution v2

Status: **ACCEPTED FOUNDATION — v0.8**

## Principle

A command enum value existing in Core does not mean the UI may show it. Commands are projected from concrete `CompletionCapabilities` and a terminal immutable `JobSnapshot`.

## v0.8 capability table

| Command | v0.8 target | Reason |
|---|---|---|
| Open containing folder | enabled on Windows | non-destructive shell action |
| Copy result path | conditional | explicit non-zero clipboard owner HWND required |
| Start result drag | disabled | production OLE drag-source not yet proven |
| Delete generated output | identity-gated | requires one strong `GeneratedArtifact` and handle-bound deletion service |
| Dismiss | enabled | local UI state only |

## Runtime enforcement

`CompletionCommandExecutor` re-projects the model at command execution time. It rejects:

- unknown job;
- non-terminal job;
- command not currently offered;
- missing/singular-output violation;
- identity-free generated output for Delete;
- unsupported command.

## Delete generated output

Delete is enabled only when all conditions hold:

```text
terminal Completed job
AND exactly one OutputPath
AND exactly one GeneratedArtifact
AND artifact.Path == OutputPath
AND artifact.Identity != null
AND IGeneratedArtifactDeletionService.IsSupported
```

The Windows deletion service then:

```text
open current path with DELETE + FILE_READ_ATTRIBUTES
and FILE_FLAG_OPEN_REPARSE_POINT
  -> read FILE_ID_INFO from same handle
  -> compare with captured identity
  -> mismatch: refuse
  -> match: SetFileInformationByHandle(FileDispositionInfo)
  -> close verified handle
```

No `DeleteFile(path)` occurs after identity verification.

## Windows adapter

`WindowsCompletionPlatformService` provides:

- Explorer reveal/select;
- CF_UNICODETEXT clipboard copy with bounded retry only when composition supplies an explicit clipboard owner HWND; Unicode HGLOBAL is prepared before `EmptyClipboard`;
- explicit unavailable result for result-drag.

`WindowsGeneratedArtifactIdentityService` separately provides identity capture and strong delete. Keeping destructive output handling out of the generic completion platform interface prevents path-based deletion from being introduced casually.

No completion command is executed from inside an OLE/WinEvent native callback.

## Destructive command lifetime

`DeleteGeneratedOutput` is consumable per `JobId` (ADR-037). Concurrent/repeated execution after success or a terminal identity outcome is rejected without a second deletion-service call. Cancellation and generic transient native/access failures may release the attempt marker for retry.

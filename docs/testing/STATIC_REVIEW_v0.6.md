# Static Review — SmartDrag Foundation v0.6

## Scope

Compile-independent review of changes introduced after v0.5. This is not a substitute for G0 and does not claim C# execution.

## Repository checks

- 17 `.csproj` files discovered and 17 listed in `SmartDrag.sln`;
- all `ProjectReference` targets exist;
- `SmartDrag.Orchestration` references only `SmartDrag.Core`;
- no concrete SkiaSharp/ImageSharp/FFmpeg package dependency is active;
- required ADR/orchestration/G3 documents are present;
- no `bin`, `obj`, or `.vs` directories are packaged;
- deterministic image-corpus SHA-256 validation passes.

## v0.6 code-boundary review

### Orchestration

- `PrepareOverlay` fails closed for invalid/unknown payload and no actions;
- prepared session captures offered ActionIds and hidden preflight authority;
- `TryCommitDrop` requires authoritative OLE qualification and preflight identity match;
- action must both have been offered and remain currently available;
- `PreserveSource=false` is rejected before ActionRequest creation;
- RequestId is the DragSessionId;
- accepted/rejected decision construction is internal to orchestration assembly;
- dispatcher rejects non-accepted decisions and is idempotent per request.

### Runtime

- JobQueue keeps sequential execution policy;
- duplicate equivalent RequestId returns original JobId;
- conflicting RequestId reuse throws instead of silently executing different work;
- cancellation/terminal snapshot logic remains unchanged by idempotency addition.

### Imaging

- extension is still only preflight evidence;
- G3 content-inspection contract exists separately from processor implementation;
- guard rejects unknown format, animation, malformed dimensions, source-size, dimension, and decoded-pixel excess;
- numeric production limits remain intentionally unset;
- Remove Metadata policy requires visible orientation normalization and ICC preservation.

## Known compile/runtime unknowns requiring G0

- C# source has not been compiled in this environment;
- `System.Collections.Frozen` usage requires the intended .NET 8 target to be confirmed by build;
- all xUnit source tests are unexecuted until G0;
- Windows-specific projects remain unexecuted here;
- no codec adapter exists, by design.

## Review result

`STATIC REVIEW PASSED WITH G0 REQUIRED`.

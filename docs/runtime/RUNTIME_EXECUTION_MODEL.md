# SmartDrag Runtime Execution Model v1

Status: **FOUNDATION IMPLEMENTED / BUILD UNVERIFIED**

## Purpose

This layer begins after the user has intentionally committed a SmartDrag action. It must not participate in native Explorer drag ownership.

Runtime flow:

```text
ActionRequest
  -> IJobQueue.Enqueue
  -> Queued
  -> Running
  -> IActionExecutor
  -> IActionHandler
  -> processor/output pipeline
  -> Completed | Failed | Cancelled
```

## Invariants

1. The drag/OLE callback path must never perform image processing.
2. MVP runs at most one processing job concurrently.
3. Cancellation is cooperative through `CancellationToken`.
4. Exceptions from action handlers are contained and converted to `ActionResult` failure.
5. A failed/cancelled job must never imply that the source file can be deleted.
6. Job state is queryable through immutable `JobSnapshot` values.
7. Queue implementation has no UI dependency.

## State transitions

Allowed runtime transitions:

```text
Queued -> Running -> Completed
Queued -> Running -> Failed
Queued -> Running -> Cancelling -> Cancelled
Queued -> Cancelled
```

A terminal job never becomes active again.

## Why single-consumer in MVP

Image codecs may create substantial transient memory pressure. Parallel processing would complicate:
- memory bounds;
- disk IO contention;
- cancellation UX;
- progress semantics;
- deterministic testing.

Throughput is not an MVP product requirement. `SequentialJobQueue` therefore uses a single consumer. The public `IJobQueue` contract does not prevent a later bounded-parallel implementation.

## Disposal

Disposing the queue:
- stops accepting new jobs;
- requests cancellation for non-terminal jobs;
- completes the channel;
- awaits worker termination;
- disposes per-job cancellation sources.

Action handlers are required to honor cancellation. A handler that ignores cancellation is a bug and can delay application shutdown.

## v0.5 completion projection

Terminal `JobSnapshot` now preserves input paths plus optional `ActionMetrics`. Completion UI is derived via `CompletionProjector`; processors and output managers never invoke completion UI directly. See `COMPLETION_MODEL.md` and ADR-020.


## v0.9 status presentation

Runtime state is projected without fake numeric progress. The oldest non-terminal job becomes the visible operation; additional non-terminal jobs contribute a queued-behind count. Terminal completions are queued independently and do not block processing.

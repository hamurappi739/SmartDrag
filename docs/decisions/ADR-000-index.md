# ADR Index

- ADR-001 — Cursor deferred
- ADR-002 — Domain core independent from Windows/UI
- ADR-003 — Fail-open toward native drag
- ADR-004 — Fail-safe toward user files
- ADR-005 — No Explorer injection / DoDragDrop detouring
- ADR-006 — IDropTarget is an intentional action target, not a global observer
- ADR-007 — WinEvent drag-start research path
- ADR-008 — Compact offset overlay
- ADR-009 — Native drop semantics for action selection
- ADR-010 — Single overlay HWND with internal hit testing
- ADR-011 — Never report MOVE in MVP
- ADR-012 — Windows proof before production UI stack
- ADR-013 — Per-monitor DPI v2
- ADR-014 — P0 code remains diagnostic until evidence gate passes

- [ADR-015](ADR-015.md) — MVP processing queue is sequential
- [ADR-016](ADR-016.md) — Outputs commit from SmartDrag-owned temporary artifacts
- [ADR-017](ADR-017.md) — Image codec dependency remains gated; SkiaSharp is current first candidate
- [ADR-018](ADR-018.md) — Production overlay qualification is fail-closed before presentation
- [ADR-019](ADR-019.md) — Payload identity uses preflight evidence plus authoritative OLE Drop revalidation
- [ADR-020](ADR-020.md) — Completion UI is projected from terminal immutable job snapshots

- [ADR-021](ADR-021.md) — Native callback boundaries are no-throw and fail closed
- [ADR-022](ADR-022.md) — Production drag-to-job commit is isolated in a portable orchestration layer
- [ADR-023](ADR-023.md) — A shown overlay is an immutable authorization snapshot
- [ADR-024](ADR-024.md) — G3 image behavior is codec-independent
- [ADR-025](ADR-025.md) — Remove Metadata preserves appearance by normalizing orientation and preserving ICC
- [ADR-026](ADR-026.md) — Image execution requires content inspection and configurable resource limits
- [ADR-027](ADR-027.md) — One drag session authorizes one idempotent ActionRequest
- [ADR-028](ADR-028.md) — Overlay authorization and accepted commit decisions are unforgeable capability objects

- [ADR-029](ADR-029.md) — Durable output recovery journaling is required before a reservation is issued
- [ADR-030](ADR-030.md) — Startup recovery is journal-driven only; never scan user directories for partials
- [ADR-031](ADR-031.md) — Completion commands are capability-gated; generated-output deletion remains disabled in v0.7
- [ADR-032](ADR-032.md) — One active drag interaction owns one consumable overlay authorization capability
- [ADR-033](ADR-033.md) — Production startup order is single-instance -> crash recovery -> hooks/UI/runtime
- [ADR-034](ADR-034.md) — Generated-output deletion requires Windows file identity and handle-bound disposition
- [ADR-035](ADR-035.md) — Every concrete image codec is wrapped by the execution guard
- [ADR-036](ADR-036.md) — One production composition root; native activation stays gate-blocked
- [ADR-037](ADR-037.md) — Destructive completion authorization is consumable per JobId


- [ADR-038](ADR-038.md) — Toolkit-neutral presentation state is a separate project.

- [ADR-039](ADR-039.md) — MVP operation progress is indeterminate until a truthful codec progress contract exists.

- [ADR-040](ADR-040.md) — Application-facing production APIs expose capabilities, not runtime internals.

- [ADR-041](ADR-041.md) — MVP settings are validated centrally and cannot disable source preservation.

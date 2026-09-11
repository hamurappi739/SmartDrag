# Static Review — Foundation v0.9

Build execution is unavailable in the generation environment. This review records issues found before packaging and does not replace G0.

## Findings corrected

1. **Presentation callbacks under lock:** initial coordinator draft invoked `SnapshotChanged` while holding the state lock. This could allow arbitrary UI code to stall/re-enter presentation state. Publishing is now performed outside the lock; observer exceptions are contained.
2. **Application authority leakage:** v0.8 graph publicly exposed JobQueue/DragWorkflow/CompletionCommandExecutor. v0.9 narrows the graph to safe drag/presentation/payload/capability surfaces.
3. **Caller-controlled output policy:** production drag commit previously accepted `OutputPolicy`. v0.9 application-facing commit no longer does; it uses `MvpOutputPolicyFactory`.
4. **Unvalidated settings:** timing/placement values now pass through central finite/range validation and PreserveSource cannot be disabled in MVP.
5. **Misleading progress risk:** no numeric operation progress field exists. Running work is explicitly indeterminate until a truthful processor progress contract is added.
6. **Presentation privacy:** operation presentation derives only a safe display filename; failure detail consumes `AppError.UserMessage`, not technical diagnostics.

## Remaining proof

- C# compilation and xUnit execution on Windows/.NET 8;
- actual renderer binding and focus/accessibility behavior;
- G1/G2 native evidence;
- G3 codec and resource limits;
- P4 Windows artifact-identity deletion proof;
- P5 manual presentation/runtime UX matrix.

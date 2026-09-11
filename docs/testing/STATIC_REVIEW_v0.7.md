# Static Review — Foundation v0.7

Build status remains **UNVERIFIED BUILD** because the generation environment has no .NET SDK. This review does not pretend to replace G0 compilation/xUnit execution.

## New layers reviewed

- durable output recovery contracts;
- JSON recovery journal;
- journal-integrated `PhysicalOutputManager`;
- startup recovery service;
- single-instance lifetime primitive;
- production startup safety bootstrap;
- active drag interaction coordinator;
- completion capability projection/execution;
- Windows Explorer reveal / Unicode clipboard adapter;
- new P3 tests and ADRs.

## Defects found during review and fixed before checkpoint

### 1. Thread-affine named mutex ownership

Initial v0.7 draft acquired the Windows named `Mutex` on the bootstrap calling thread and would have released it from whatever thread eventually disposed the lease. Because mutex ownership is thread-affine, an async continuation/shutdown on another thread can make `ReleaseMutex` fail.

**Fix:** `NamedMutexSingleInstanceLease` now owns/acquires/releases the mutex on a dedicated background lifetime thread. `Dispose` only signals that owner thread and joins it. This preserves abandoned-mutex crash semantics without requiring application async code to remain on one thread.

### 2. Completion delete TOCTOU

Historical `GeneratedOutputPaths` proves SmartDrag created a path at completion time, but not that the same file still occupies the path when a later Delete command is clicked.

**Fix:** v0.7 hard-clamps `CanDeleteGeneratedOutput=false` in Runtime. Strong Windows file identity / handle-based deletion requires a future ADR before enabling the command.

### 3. Recovery authority too broad in earlier backlog wording

Older output-safety docs suggested a future wildcard/age scan of SmartDrag-looking partial files.

**Fix:** rejected. v0.7 recovery is durable-journal-only and exact-path-only. It does not enumerate user folders for cleanup candidates.

### 4. Recovery must exist before processing authority

Optional journaling would still permit a crash after a processor creates a partial but before there is durable evidence.

**Fix:** `PhysicalOutputManager` requires an `IOutputRecoveryJournal`; `ReserveAsync` writes the record before returning a reservation. Journal failure returns `RecoveryJournalUnavailable` and processing receives no temporary path.

## Static gates executed

```text
STATIC VALIDATION PASSED
Projects in solution: 17
Project files discovered: 17
Pre-G3 codec dependency gate: clean
G2 payload qualification invariants: present
Native callback safety invariants: present
P2 orchestration commit gate: present
G3 codec-neutral image policy/corpus: present
v0.6 capability/idempotency guards: present
v0.7 recovery/completion/session/startup guards: present
```

Image corpus:

```text
IMAGE CORPUS VALIDATION PASSED
Fixtures: 9
```

## G0 caveat

C# source tests are prepared but have **not** executed in this environment. The first Windows/.NET 8 session must run `./scripts/build.ps1` and fix every compiler/test failure before manually validating G1/G2/P3.

### 5. Divergent same-revision journal copies

If valid main/pending copies share a revision but disagree on entries, selecting one arbitrarily could broaden or lose cleanup authority. v0.7 now fails closed on this state; runtime remains blocked until the journal state is resolved.

# SmartDrag Output Safety Model v1

Status: **ACCEPTED FOUNDATION**

## Core rule

The MVP never edits, deletes, truncates, or silently replaces the source file.

## Pipeline

```text
source
  -> reserve output names
  -> processor writes temporary artifact
  -> processor validates success
  -> commit temporary artifact to final path
  -> completion UI
```

## Temporary artifact

The temporary artifact is allocated in the same destination directory as the final output where possible.

Example:

```text
photo.png
.photo-compressed.smartdrag-<guid>.partial.png
photo-compressed.png
```

Keeping the temporary artifact in the target directory avoids a cross-volume final move for the default SameDirectory policy and simplifies cleanup/recovery semantics.

## Collision safety

`GenerateUniqueName` is checked twice:
1. during reservation;
2. immediately before commit.

The second check is mandatory because another process can create the preferred output between reservation and commit.

`File.Move(..., overwrite: false)` is the final guard. SmartDrag never uses an overwrite fallback.

## Configured directory

The domain model contains `ConfiguredDirectory` for future settings compatibility, but no UI exposes it in the first MVP slice. If the mode is selected programmatically, an explicit existing directory path is required.

## Cancellation/failure

If processing fails or is cancelled after a temporary artifact exists, the action handler must invoke `IOutputManager.AbandonAsync`. Cleanup deliberately does not inherit the cancelled operation token; once SmartDrag owns a partial artifact it must still attempt to remove it.

Failure to remove a partial file is surfaced as `CleanupFailed` and must be logged. It still must not cause source mutation.

## Crash recovery

Implemented in foundation v0.7. Every output reservation is recorded in a durable local recovery journal **before** the reservation is returned to a processor. The exact `ReservationId` is embedded in the partial filename.

Startup recovery runs only after acquiring the single-instance mutex and may delete only an exact journaled path whose filename still matches the reservation-bound SmartDrag partial convention.

It must never scan user directories or wildcard-delete temp-like files. If the journal is corrupt/unavailable, SmartDrag leaves possible orphaned partials rather than guessing. See `docs/recovery/CRASH_RECOVERY_MODEL.md` and ADR-029/030/033.

## v0.8 generated-artifact identity after commit

Successful output commit now produces `GeneratedArtifact`, not only a final path.

When Windows identity support is composed, `PhysicalOutputManager` attempts:

```text
identity(partial before move)
  -> File.Move(overwrite:false)
  -> identity(final after move)
  -> equal ? retain strong identity : identity=null
```

Identity failure never causes a committed non-destructive output to be deleted or rolled back. It only suppresses later destructive completion capability.

The recovery journal remains concerned only with known partial artifacts. It does not persist generated-artifact identities because completion jobs are not currently persisted across process restarts.

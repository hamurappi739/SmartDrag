# Crash / Partial-Output Recovery Model v1

Status: **ACCEPTED FOUNDATION**

## Threat model

SmartDrag can terminate after an output reservation is made but before normal `CommitAsync` / `AbandonAsync` cleanup completes. Power loss, process kill, codec crash, OS shutdown, or an unhandled implementation defect must not make recovery guess which user files are disposable.

## Durable authority

Before a processor receives `TemporaryOutputPath`, `PhysicalOutputManager` writes a local journal record.

```text
reservation id
+ exact partial path
+ creation timestamp
```

The journal lives under LocalApplicationData by default. It is local product state, never telemetry.

Source/final paths are intentionally omitted from the recovery record.

## Partial naming

The exact reservation id is embedded in the filename:

```text
.photo-compressed.smartdrag-0123456789abcdef....partial.png
```

Startup cleanup requires both journal membership and this naming proof.

## Startup algorithm

```text
acquire SmartDrag single-instance lease
  -> read journal
  -> for each record:
       invalid path/convention -> remove/ignore record, DELETE NOTHING
       missing partial         -> remove stale record
       valid existing partial  -> delete exact path, then remove record
       deletion failure        -> retain record for next startup
```

No directory enumeration, globbing, age-based guessing, source deletion, or final-output deletion is allowed.

## Corrupt journal

If the journal cannot be parsed/read, startup recovery returns a failure report and performs no guessed cleanup. Orphaning a SmartDrag partial is safer than broad filesystem deletion.

## Atomic journal writes

`JsonOutputRecoveryJournal` writes a sibling temporary journal and then replaces/moves it into place. It bounds file size and entry count to avoid unbounded startup input.

## Concurrency

Recovery is only valid after the named single-instance mutex is acquired. A second live SmartDrag process must not recover another process's active partial. The mutex is owned by a dedicated lifetime thread so async startup/shutdown cannot attempt `ReleaseMutex` from a different thread; an abandoned mutex after a crash explicitly grants the next owner permission to proceed to recovery.

## Startup blocking

An unclean recovery report is a startup gate, not merely a warning. `StartupSafetyBootstrap` returns `RecoveryBlocked`; production drag hooks/runtime must remain unregistered until recovery is clean.

# P1 Runtime / Output Safety Test Matrix

Status: code-level tests generated; execution waits for G0 build.

## Job queue

- one successful job: `Queued -> Running -> Completed`;
- failure: `Running -> Failed` with preserved `AppError`;
- cancellation while running: `Running -> Cancelling -> Cancelled`;
- cancellation while queued: job never executes;
- two jobs execute FIFO;
- max concurrent action execution is 1;
- unknown handler does not crash queue;
- handler exception is contained;
- enqueue after dispose fails;
- disposing queue requests cancellation.

## Output reservation

- source missing -> `SourceNotFound`;
- `PreserveSource=false` -> rejected;
- empty suffix is allowed when a changed extension still guarantees a distinct output path;
- SameDirectory places final and temp next to source;
- ConfiguredDirectory without path -> rejected;
- collision policy Fail -> `OutputCollision`;
- GenerateUniqueName -> `(2)`, `(3)`, etc.;
- computed output path can never equal source path.

## Commit

- temporary file missing -> `TemporaryOutputMissing`;
- successful commit removes temp path and creates final path;
- source contents remain byte-for-byte unchanged;
- collision created after reservation -> choose a new unique final path;
- never overwrite a collision;
- unauthorized destination -> typed failure;
- cancellation before commit -> no final move.

## Cleanup

- existing partial file is deleted;
- absent partial file counts as successful cleanup;
- source remains untouched;
- cleanup IO failure is reported rather than hidden.

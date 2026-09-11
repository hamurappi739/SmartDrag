# Preview persistence hardening phase — complete

Date: 2026-09-04

## Outcome

Preview language/theme preferences and the privacy-safe history file now fail closed on oversized persisted input and
write bounded JSON through a flushed, unique sibling file before atomic replacement. Failed writes clean up their
temporary file without turning a best-effort UI preference into a crash.

## Implemented

- added explicit persisted-size limits for preferences and history;
- replaced unbounded `ReadAllText` loads with bounded file checks and streaming JSON reads;
- flushed UTF-8 payloads to disk before replacement;
- made preference-write cleanup symmetric with history-write cleanup;
- retained history entry count, path sanitization, duplicate collapse, and language normalization rules;
- added oversized-file regression tests for both stores.

## Verification

- `SmartDrag.Infrastructure.Tests`: 41 passed, 0 failed;
- full solution build and test gates remain green;
- static repository validation remains green;
- no runtime authority, filesystem-wide cleanup, native activation, or gated codec capability was added.

## Scope boundary

This phase hardens preview persistence only. It does not change production activation, payload qualification, output
policy, or the unresolved G1/G2/G3/P4 evidence gates.

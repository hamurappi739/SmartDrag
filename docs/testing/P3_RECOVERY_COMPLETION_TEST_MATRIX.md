# P3 — Recovery / Completion / Session Coordination Test Matrix

## Automated source tests

### Recovery journal

- durable JSON round-trip;
- remove record;
- reservation records exact ReservationId-bound path before return;
- commit clears recovery record;
- abandon deletes partial and clears record;
- invalid journal path never authorizes deletion;
- missing partial only clears stale record;
- journal failure prevents a new output reservation.

### Completion

- successful projection offers only capabilities actually implemented;
- generated ownership alone does not expose delete while capability is disabled;
- delete requires both ownership + capability at projector level;
- Runtime executor clamps delete capability off;
- command not offered cannot execute;
- non-terminal job cannot execute completion commands;
- terminal completion is published at most once per JobId.

### Interaction coordination

- prepared session can commit once;
- repeated commit after capability consumption is rejected;
- newer drag supersedes stale prepared session;
- wrong DragSessionId cannot commit;
- native drag cancel/end clears active capability;
- overlay hide failure never preserves authorization.

### Single instance

- first named mutex lease acquires;
- second same-name lease is rejected while first remains alive.

## Windows manual checks after G0

1. Start SmartDrag bootstrap twice; only one instance may proceed.
2. Kill process while image partial is being written; restart; exact journaled partial is removed.
3. Place similarly named non-journaled file beside partial; restart; file remains untouched.
4. Corrupt recovery JSON; restart; no user-directory scan/deletion occurs and recovery failure is diagnosable.
5. Completion Open Folder selects generated result in Explorer.
6. Copy Result Path places exact full result path in Unicode clipboard.
7. No Delete Generated Output button exists in v0.7 production capability set.
8. No Start Result Drag button exists until its dedicated proof is accepted.

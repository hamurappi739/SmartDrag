# Output Recovery Journal Format v1

Schema: `schemas/output-recovery-journal.v1.schema.json`

The journal is local-only safety state. It is not telemetry and is not uploaded.

Example:

```json
{
  "version": 1,
  "revision": 12,
  "entries": [
    {
      "reservationId": "7bc2f0d0-5cd9-45e4-9d50-d9cb1e20d39e",
      "temporaryPath": "C:\\Users\\...\\.photo-compressed.smartdrag-7bc2f0d05cd945e49d50d9cb1e20d39e.partial.png",
      "createdAt": "2026-08-31T00:00:00+00:00"
    }
  ]
}
```

## Privacy minimization

The source path and preferred/final output path are not persisted. Only the exact partial path needed for cleanup is stored.

## Durability protocol

Main file:

```text
output-reservations.v1.json
```

Single pending file:

```text
output-reservations.v1.json.pending
```

Each mutation increments `revision`, writes+flushes `.pending`, then promotes it to the main path. On read/startup:

- if pending is valid and newer, promote pending;
- if main/pending have the same revision, their entries must be identical or recovery fails closed;
- if main is valid and pending is invalid/older, retain main and remove pending best-effort;
- if main is invalid but pending is valid, recover from pending;
- if neither copy is valid, fail closed and perform no guessed filesystem cleanup.

The fixed pending filename prevents unbounded accumulation of random write-temp files.

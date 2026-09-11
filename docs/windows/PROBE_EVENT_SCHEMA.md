# SmartDrag Windows Probe — JSONL Event Schema

The probe log is diagnostic evidence for G1/G2. It is not product telemetry and is not uploaded anywhere.

## Privacy invariant

Do **not** log full source paths or file names. Current events log counts, extensions, HWND/PID identifiers, timing, qualification states, and action IDs. If future debugging absolutely requires a path correlation, use an ephemeral one-way hash in a dedicated opt-in diagnostic build rather than raw paths.

## Events

### `probe.started`

Probe mode, process/thread id, log location, diagnostic warning.

### `winevent.drag-signal`

Observed WinEvent drag lifecycle signal:

- `kind` (`Started`, `Cancelled`, `Completed`);
- source HWND;
- process id/name resolved outside the WinEvent callback;
- source thread/object/child ids;
- Windows event time.

### `candidate.suppressed.non-explorer`

Drag signal came from a process outside the current Explorer-only MVP source policy.

### `p0.signal-only-warning`

Explicit evidence that the current session is using interop-only mode. Any log containing this event is invalid as proof of G2.

### `g2.explorer-selection`

Result of the provisional Explorer Shell selection read:

- success;
- matched root HWND;
- selected item count;
- extensions only;
- failure classification if applicable. Shell/COM exception messages are intentionally excluded.

### `g2.preflight-qualified`

Core qualification result from Explorer selection evidence:

- state;
- rejection reason;
- may-show-overlay decision.

### `candidate.suppressed.preflight-unavailable`

G2 could not obtain conservative preflight evidence; overlay was not shown.

### `candidate.suppressed.preflight-rejected`

Preflight evidence was obtained but did not satisfy current single PNG/JPEG MVP rules.

### `overlay.shown`

Contains:

- mode;
- elapsed drag duration;
- cursor travel;
- preflight state/reason if any;
- foreground HWND before/after;
- `foregroundChanged` boolean.

Any `foregroundChanged=true` requires investigation; a non-activating overlay must not steal foreground focus.

### `ole.drag-enter`

Contains source-allowed effects, returned effect, `CF_HDROP` format availability, preflight allowance, and hovered action id. No path extraction should occur for this event.

### `ole.drag-leave`

User left the SmartDrag target without dropping.

### `ole.*.exception`

Privacy-safe native OLE boundary failure: managed exception type and HRESULT only. Any such event is a gate red flag.

### `ole.drop`

Authoritative Drop evidence:

- source-allowed effects;
- returned COPY/NONE effect;
- whether `CF_HDROP` was read;
- authoritative qualification state/reason;
- file count and extensions;
- preflight/path match;
- action id.

### `probe.action-committed`

Diagnostic action commitment only. The P0/G2 probe never processes or modifies the file.

### `window.wndproc-exception` / `overlay.show-failed`

Native-window or overlay presentation failure. These are gate red flags. Diagnostics exclude exception messages.

### `probe.stopped`

Normal probe shutdown marker plus dropped diagnostic-entry and WinEvent-signal counters. Non-zero counters mean the evidence set is incomplete.

## Evidence interpretation

- G1 focuses on `overlay.shown.foregroundChanged`, native drag coexistence, `ole.drag-*`, and manual native-drop behavior.
- G2 requires absence of `p0.signal-only-warning`, conservative suppression for unsupported inputs, `Eligible`/`Supported` preflight for every shown overlay, and `preflightMatched=true` for committed actions.
- COPY-or-NONE is mandatory; any MOVE effect, native-boundary failure, dropped signal, or dropped log entry invalidates a clean automatic result.
- The analyzer also rejects path-like values under source/output/display path fields, protecting the privacy invariant
  if a future probe change accidentally logs raw file identity.
- A successful probe action is not proof that image processing works; G3 is separate.

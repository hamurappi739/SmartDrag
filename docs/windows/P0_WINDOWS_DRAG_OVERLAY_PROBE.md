# P0 — Windows Drag / Overlay Technical Proof

## Why P0 exists

The product only works if it can appear during Explorer drag without stealing focus, blocking Explorer, or changing the native drop destination unless the user deliberately enters SmartDrag.

This proof therefore precedes image processing and production UI selection.

## Implemented diagnostic components

- `WinEventDragSignalSource`
  - `EVENT_OBJECT_DRAGSTART`
  - `EVENT_OBJECT_DRAGCANCEL`
  - `EVENT_OBJECT_DRAGCOMPLETE`
  - `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`
- raw `WS_POPUP` overlay HWND
- `WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`
- `OleInitialize`
- `RegisterDragDrop`
- managed `IDropTarget`
- `CF_HDROP` extraction via `IDataObject`
- one-image validation for the probe
- action hit-test on `DragOver`
- `DROPEFFECT_COPY` only; never MOVE
- JSONL diagnostic timeline

## Important limitation

Before the cursor enters the SmartDrag overlay, the probe has only a drag lifecycle hint, not authoritative OLE payload data. Therefore the current P0 overlay may appear for an Explorer drag that later proves unsupported.

That is acceptable for the technical proof only. Production qualification must add conservative payload hinting or another documented source resolver before meeting the final UX invariant "payload supported".

## Manual run

```powershell
./scripts/run-probe.ps1
```

Then:

1. Focus Explorer.
2. Drag one local image file.
3. Keep moving long enough to exceed the activation threshold.
4. Confirm overlay appears near, not beneath, cursor.
5. Ignore SmartDrag and complete a normal Explorer drop.
6. Confirm native behavior is unchanged.
7. Repeat drag and intentionally move onto a SmartDrag tile.
8. Release.
9. Confirm the probe logs the selected action and source file stays untouched.
10. Test Esc cancellation.

## Do not interpret this probe as production code

The raw renderer, extension-only image classification, fixed threshold, and diagnostic filename logging are proof-specific and must be replaced/refined after P0 evidence is collected.

## v0.5 mode separation

`--mode=p0` is an interop-only signal mode and may show the diagnostic overlay without proving payload support. It exists solely to isolate G1 coexistence behavior. It is forbidden as production logic.

`--mode=g2` is the strict qualification experiment described in `G2_PREOVERLAY_QUALIFICATION.md`.

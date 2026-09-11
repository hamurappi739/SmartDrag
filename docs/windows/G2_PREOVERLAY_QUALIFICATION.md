# G2 — Pre-overlay Payload Qualification

## Goal

Prove a production-safe answer to this question:

> Before showing SmartDrag beside the cursor, can we conservatively establish that the Explorer drag represents exactly one supported image file without taking ownership of the native drag?

The canonical UX does not permit a normal overlay for unsupported payloads.

## Current experiment

The G2 probe path is:

```text
EVENT_OBJECT_DRAGSTART
  -> resolve Explorer source process
  -> GetAncestor(sourceHwnd, GA_ROOT)
  -> Shell.Application.Windows()
  -> match Explorer HWND
  -> ShellFolderView.SelectedItems()
  -> FolderItem.Path for each selected item
  -> PhysicalFilePayloadSnapshotFactory
  -> MvpPayloadQualifier
  -> overlay only when state == Eligible
```

At `IDropTarget::Drop`:

```text
IDataObject
  -> CF_HDROP
  -> actual path(s)
  -> MvpPayloadQualifier (Supported)
  -> exact single-path match against preflight
  -> COPY or NONE
```

## Why the implementation is still provisional

Microsoft documents `ShellFolderView.SelectedItems` as a way to obtain selected items in a local Shell view, but that does not prove that:

- the WinEvent HWND always maps cleanly to the correct Explorer ShellWindows entry;
- Windows 11 tabs expose the expected HWND/document pairing;
- Desktop drags are represented the same way;
- the current selection is always exactly the item(s) entering the drag;
- virtual items always have filesystem paths;
- querying selection during the OLE drag loop is sufficiently fast and stable.

These are empirical product requirements and must be tested, not inferred.

## Conservative rules

Before G3, only `.png`, `.jpg`, and `.jpeg` may qualify production overlay presentation. This is intentionally narrower than the eventual image roadmap. The codec gate may expand the source-format matrix later.

Any of the following suppresses the overlay:

- no selection evidence;
- no filesystem path;
- zero or multiple selected items;
- directory;
- missing path;
- unsupported extension;
- Shell automation failure.

At Drop, any path mismatch suppresses action commit and returns `DROPEFFECT_NONE`.

## Probe commands

```powershell
# P0 interop-only behavior. Does NOT satisfy G2.
./scripts/run-probe.ps1 -Mode p0

# G2 strict preflight experiment.
./scripts/run-probe.ps1 -Mode g2
```

Each normal run requires recorded drag/drop interaction and leaves a machine-readable `.summary.json` beside the
latest mode-specific JSONL log. For startup-only health checks use `-Smoke`; those runs are never gate evidence.

## Required evidence before accepting Explorer selection preflight

1. Windows 10 Explorer window, single PNG/JPEG.
2. Windows 11 Explorer window, single PNG/JPEG.
3. Windows 11 multiple tabs; drag from active tab only.
4. Two Explorer windows open at once.
5. Desktop PNG/JPEG.
6. File extensions hidden in Explorer settings.
7. Unsupported file (`.txt`, `.pdf`) never shows overlay in G2 mode.
8. Folder drag never shows overlay.
9. Multi-selection never shows overlay.
10. Selection changes immediately before drag.
11. Rename/move/delete race between preflight and Drop.
12. Drag ignored and dropped normally elsewhere.
13. Drag enters SmartDrag and exits again without Drop.
14. Explorer restart.
15. Mixed-DPI multi-monitor drag.

Record the JSONL timeline for every failure. Do not promote this mechanism to production merely because the basic single-window case works.

## Action-availability gate

Payload eligibility is necessary but not sufficient. Production orchestration must also resolve the `ActionRegistry` for the qualified payload and current capabilities. An overlay with zero available actions is forbidden. `OverlayEligibilityEvaluator` encodes this as a pure Core rule so the future Windows coordinator cannot accidentally show an empty shell.

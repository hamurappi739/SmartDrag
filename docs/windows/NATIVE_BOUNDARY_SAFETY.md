# Native Boundary Safety

This document is normative for Windows interop implementations.

## Rule

No ordinary managed exception may escape a callback that Windows/COM invoked. SmartDrag prefers a false negative over disturbing the user's native drag.

## WinEvent callback

Allowed work:

- map event id to SmartDrag signal kind;
- copy HWND/object/child/thread/time primitive fields;
- enqueue immutable `DragSignal`.

Forbidden work:

- Explorer/Shell COM traversal;
- filesystem access;
- process object lookup;
- UI;
- normal diagnostic serialization/I/O.

If capture/enqueue fails, increment `DroppedSignals` and return.

## WndProc

`ProbeWindow.WindowProc` catches callback failures and records only message id, managed exception type, and HRESULT. Exception messages are not logged because they can contain user-controlled text/paths.

A future production window host must preserve the same boundary even if the renderer changes.

## OLE `IDropTarget`

`DragEnter`, `DragOver`, `DragLeave`, and `Drop` are no-throw boundaries. On an exception:

```text
effect = DROPEFFECT_NONE (where applicable)
best-effort reset SmartDrag hover/target state
write privacy-safe diagnostic
return S_OK
```

SmartDrag never substitutes MOVE when COPY is unavailable.

`DragEnter` checks format availability only. Authoritative path extraction is deferred to `Drop`.

## Diagnostics

After `TimelineLogger` is constructed, `Write` is best-effort/no-throw. Dropped entries are counted and emitted at normal shutdown. Probe evidence with dropped native signals/log entries is not clean gate evidence.

## Privacy

Native-boundary diagnostics may contain:

- event/action ids;
- extension;
- counts;
- HWND/PID for local probe diagnosis;
- exception type/HRESULT.

They must not contain source full paths, file names, or exception messages from Shell/OLE boundaries.

# Windows API Evidence — P0

This file records external API facts that constrain SmartDrag's P0 design. It is not a substitute for empirical Explorer testing.

## WinEvent drag lifecycle

Microsoft documents these event constants:

- `EVENT_OBJECT_DRAGSTART = 0x8021`
- `EVENT_OBJECT_DRAGCANCEL = 0x8022`
- `EVENT_OBJECT_DRAGCOMPLETE = 0x8023`

Source: https://learn.microsoft.com/windows/win32/winauto/event-constants

`SetWinEventHook` supports `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`. The thread that calls `SetWinEventHook` must run a message loop. Out-of-context events are delivered on that same thread and callbacks can be re-entered, so the SmartDrag callback enqueues a small immutable signal and defers process/UI/log work to the normal message-loop tick.

Source: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwineventhook

## OLE drop target

`RegisterDragDrop(HWND, IDropTarget)` registers one specific window as an OLE drop target. Microsoft explicitly requires OLE initialization (`OleInitialize`) and a pumping message loop on the registering thread.

Source: https://learn.microsoft.com/windows/win32/api/ole2/nf-ole2-registerdragdrop

`IDropTarget::DragEnter` receives the actual `IDataObject` plus source-allowed drop effects. The target chooses an allowed effect or rejects the drop. `IDropTarget::Drop` communicates the final effect back to the source; a MOVE result may cause source cleanup. SmartDrag therefore reports COPY only when COPY is allowed, otherwise NONE.

Sources:
- https://learn.microsoft.com/windows/win32/api/oleidl/nf-oleidl-idroptarget-dragenter
- https://learn.microsoft.com/windows/win32/api/oleidl/nf-oleidl-idroptarget-drop

## Non-activating positioning

`SetWindowPos` with `SWP_NOACTIVATE` repositions/shows a window without activating it.

Source: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos

## DPI

For a per-monitor-aware window, `WM_DPICHANGED` provides a suggested new rectangle. Microsoft examples apply that rectangle with `SetWindowPos(... SWP_NOACTIVATE)` and update DPI-dependent resources.

Source: https://learn.microsoft.com/windows/win32/hidpi/wm-dpichanged

## Evidence still missing

The documentation proves API semantics, not Explorer behavior. P0 still must empirically prove:

1. Explorer folder views emit the selected WinEvent drag lifecycle signals.
2. Desktop icon drags emit usable source HWND/PID information.
3. A `WS_EX_NOACTIVATE` compact HWND remains a reliable registered OLE target.
4. Passive overlay presentation does not change Explorer foreground/drag behavior.
5. Mixed-DPI placement remains usable while an active drag crosses monitors.

## v0.5 — Shell preflight and drag-loop data access

`EVENT_OBJECT_DRAGSTART` documentation says the callback's `hwnd`, `idObject`, and `idChild` identify the dragged object. This supports using the event as a signal/evidence anchor, but it does not itself provide a filesystem path.

Source: https://learn.microsoft.com/windows/win32/winauto/event-constants

`AccessibleObjectFromEvent` can retrieve the `IAccessible` object that generated a WinEvent. This remains a diagnostic/fallback research path for OQ-002; accessible names are not treated as authoritative file paths.

Source: https://learn.microsoft.com/windows/win32/api/oleacc/nf-oleacc-accessibleobjectfromevent

`ShellFolderView.SelectedItems` is documented to return the selected items in a local Shell view. v0.5 uses it only as a G2 experiment after mapping the WinEvent source to an Explorer top-level HWND. The documentation does not prove Windows 11 tab/Desktop/race behavior, so this mechanism remains provisional.

Source: https://learn.microsoft.com/windows/win32/shell/shellfolderview-selecteditems

Microsoft's Shell data-object guidance states that a target can use `QueryGetData` during `DragEnter` to determine whether it accepts a format, and warns against rendering Shell data before drop because doing so can stall the drag cursor. v0.5 therefore checks `CF_HDROP` availability in `DragEnter` and extracts paths only in `Drop`.

Source: https://learn.microsoft.com/windows/win32/shell/dataobject

## v0.5 — DPI detail

For a per-monitor-aware window, `GetDpiForWindow` returns the DPI of the monitor where that window is located. `WM_DPICHANGED` supplies a suggested rectangle that should be applied when a top-level window crosses to a monitor with a different DPI.

Sources:
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdpiforwindow
- https://learn.microsoft.com/windows/win32/hidpi/wm-dpichanged

The current probe still needs empirical mixed-DPI validation because its first pre-show size calculation uses the overlay window's current DPI. A production implementation must not assume that this first calculation is perfect when the hidden window has not yet moved to the target monitor.

## v0.8 — Generated artifact file identity / handle-bound deletion

Official Microsoft API facts used by ADR-034:

1. `FILE_ID_INFO` returned for `GetFileInformationByHandleEx(..., FileIdInfo, ...)` contains `VolumeSerialNumber` and a 128-bit `FileId`; Microsoft documents that the pair uniquely identifies a file on one computer and can be compared across open handles.
   - https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info
   - https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex
2. `SetFileInformationByHandle(..., FileDispositionInfo, FILE_DISPOSITION_INFO{DeleteFile=TRUE})` marks the file represented by the supplied handle for deletion; the handle must have been opened with DELETE access.
   - https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle
   - https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_disposition_info
3. `CreateFile` with `FILE_FLAG_OPEN_REPARSE_POINT` opens the reparse-point object itself rather than following a symbolic-link target. `FILE_SHARE_DELETE` controls compatibility with delete/rename access by other opens.
   - https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew

Engineering consequence: SmartDrag's generated-output Delete verifies identity and applies disposition on the same open handle, and deliberately refuses any path-based fallback.

# Static Review — Foundation v0.5

This review is evidence of source/repository consistency only. It is **not** a C# compilation result.

## Review passes completed

- solution/project graph validated: 15 `.csproj` files and 15 solution entries;
- every `ProjectReference` resolves;
- no codec/FFmpeg dependency appears before G3;
- legacy probe-local image extension classifier removed; payload policy is centralized in Core;
- WinEvent callback contains no filesystem, process-object, COM automation, UI, or logging work and is a no-throw boundary with a dropped-signal counter;
- OLE `DragEnter` checks format availability only; path extraction occurs at `Drop`;
- all `IDropTarget` callbacks fail closed to `DROPEFFECT_NONE` instead of allowing managed exceptions to escape;
- raw WndProc is a no-throw boundary; callback faults are logged without exception messages/source paths;
- invalid filesystem path evidence is represented as unresolved instead of re-entering `Path` APIs in the failure path;
- Core payload normalization/matching also fails closed if a malformed path bypasses the physical snapshot factory;
- drop-effect policy remains COPY-or-NONE and never MOVE;
- production-style G2 mode cannot show overlay without `Eligible` preflight;
- authoritative OLE payload must match the preflight path before action commit;
- completion delete command is only offered when the terminal job marks the output as SmartDrag-generated;
- source/output size metrics flow processor -> action result -> job snapshot -> completion model;
- no `bin`, `obj`, or `.vs` directory is included.

## Known compile/runtime risks to verify at G0/G2

1. Late-bound `Shell.Application` COM members on current Windows 10/11.
2. ShellWindows HWND identity with Windows 11 tabbed Explorer.
3. `System.Runtime.InteropServices.ComTypes.IDataObject` marshalling through managed `IDropTarget`.
4. WinEvent source HWND shape for Desktop and Explorer child objects.
5. Hidden pre-position -> `GetDpiForWindow` behavior and `WM_DPICHANGED` sequencing across mixed-DPI monitors.
6. COM cleanup behavior when Explorer automation returns shared RCWs.

## Safety review

The v0.5 changes are deliberately biased toward false negatives:

```text
uncertain preflight -> no overlay
unsupported preflight -> no overlay
OLE format unavailable -> NONE
preflight / Drop mismatch -> NONE
exception while interpreting payload -> NONE
COPY not allowed by source -> NONE
```

No v0.5 code authorizes source modification or deletion.

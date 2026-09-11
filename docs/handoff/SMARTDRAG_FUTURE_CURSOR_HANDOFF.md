# SmartDrag — Future Cursor Handoff Package

Status: WORKING SPECIFICATION v0.9
Canonical product source: `SMARTDRAG_MASTER_CONTEXT(1).md`
Process rule: Cursor is intentionally NOT used during the current design/foundation phase.

## 0. Purpose of this package

This file accumulates product, architecture, Windows-integration, safety, testing, and implementation decisions so a coding agent can later enter a large, coherent project without reconstructing intent from chat history.

The handoff package must ultimately contain:
- canonical product invariants;
- scope and non-goals;
- numbered ADRs;
- runtime state machines;
- module contracts;
- Windows interop design;
- UX behavior;
- test matrices and acceptance criteria;
- open questions and rejected approaches;
- repository structure;
- implementation skeleton/code when created.

## 1. Product invariants

1. SmartDrag is Windows-first and turns a file drag into a fast local action flow.
2. Ordinary Windows drag-and-drop is more important than SmartDrag. If SmartDrag is uncertain or fails, the native drag must keep working.
3. MVP payload is one file from Explorer; MVP processing is images only.
4. MVP image actions: Compress, Convert to WebP, Remove Metadata.
5. Processing is local-only.
6. Source files are preserved by default.
7. No silent overwrite.
8. Overlay must not take foreground activation.
9. Overlay must be DPI-aware and multi-monitor-aware.
10. Windows-specific interop is isolated from domain/application logic.
11. File processing is detached from the live OLE drag lifecycle.
12. A coding agent may not widen scope without an explicit product decision.

## 2. Architecture foundation

Primary modules:
- DragMonitor
- DragCoordinator
- DragPayloadInspector
- ActionRegistry
- OverlayService
- ActionExecutor
- JobQueue
- OutputManager
- CompletionService
- AppProfiles
- Settings

Proposed solution layout:

```
SmartDrag/
  src/
    SmartDrag.App/
    SmartDrag.Core/
    SmartDrag.Windows/
    SmartDrag.Overlay/
    SmartDrag.Actions/
    SmartDrag.Imaging/
    SmartDrag.Infrastructure/
  tests/
    SmartDrag.Core.Tests/
    SmartDrag.Actions.Tests/
    SmartDrag.Imaging.Tests/
    SmartDrag.Windows.Tests/
  docs/
    architecture/
    decisions/
    ux/
    testing/
```

`SmartDrag.Core` must not depend on HWND, WPF/WinUI, ImageSharp types, registry APIs, FFmpeg, or concrete filesystem implementations.

## 3. Runtime state model

```text
Idle
 -> Candidate
 -> Inspecting
 -> Qualified
 -> OverlayVisible
 -> ActionCommitted
 -> Executing
 -> Completed
```

Terminal/side paths:
- Candidate -> Cancelled
- Inspecting -> Suppressed
- OverlayVisible -> Dismissed
- Executing -> Failed
- Executing -> Cancelled

Critical invariant: `OverlayVisible` does NOT mean SmartDrag owns the native drag.

## 4. Stable action IDs

- `image.compress`
- `image.convert.webp`
- `image.remove-metadata`

Action metadata and action execution are separate contracts.

## 5. Output safety

Default MVP policy:
- preserve source = true;
- output location = same directory;
- collision policy = generate unique filename;
- process into temporary artifact first;
- validate output;
- move/commit to final destination only after success;
- cancellation/failure cleans temporary artifacts.

No destructive in-place processing in MVP.

## 6. ADR registry

### ADR-001 — Cursor deferred
Status: Accepted

Do not use Cursor during the foundation phase. Build the product specification, architecture, UX rules, Windows interop proofs, contracts, tests, repository structure, and as much implementation skeleton as possible first.

### ADR-002 — Domain core independent from Windows/UI
Status: Accepted

`SmartDrag.Core` contains product models, state machines, contracts, and policies only.

### ADR-003 — Fail-open toward native drag
Status: Accepted

If SmartDrag cannot confidently participate, it hides/suppresses itself. Native Windows drag behavior must remain available.

### ADR-004 — Fail-safe toward user files
Status: Accepted

No source deletion or in-place mutation by default; no silent overwrite; temporary output cleaned on failure/cancel.

### ADR-005 — No Explorer injection / DoDragDrop detouring in product architecture
Status: Accepted unless a future explicit decision reverses it.

DLL injection, IAT patching, detouring `DoDragDrop`, or modifying Explorer internals are rejected as baseline techniques because they increase crash, security, antivirus, maintenance, and compatibility risk.

### ADR-006 — OLE IDropTarget is an intentional SmartDrag action target, not a global drag observer
Status: Accepted

`RegisterDragDrop` only supplies drag data while the cursor is over a registered target HWND. Therefore SmartDrag must not use a full-screen/invisible topmost drop target to spy on an existing drag; that would compete with the actual destination.

### ADR-007 — Primary drag-start research path uses WinEvent accessibility notifications
Status: PROVISIONAL / MUST PROVE

Investigate `SetWinEventHook` with:
- `EVENT_OBJECT_DRAGSTART`
- `EVENT_OBJECT_DRAGCANCEL`
- `EVENT_OBJECT_DRAGCOMPLETE`

Use `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`.

This is a supported out-of-process observation mechanism, but Explorer/desktop consistency must be proven on supported Windows versions. The event identifies an accessible dragged object, not the OLE `IDataObject`.

### ADR-008 — Overlay uses a compact offset HWND; it must not sit underneath the cursor by default
Status: Accepted

The overlay should appear near, but offset from, the cursor. This avoids stealing the current OLE target until the user deliberately moves onto a SmartDrag action.

### ADR-009 — SmartDrag action selection uses native drop semantics
Status: PROVISIONAL / STRONGLY PREFERRED

During an active file drag, the user selects a SmartDrag action by moving the dragged file over an action tile and releasing the mouse. The overlay HWND is registered as an OLE `IDropTarget`.

This preserves the user's mental model and avoids trying to click a separate UI while the source application owns the drag loop.

### ADR-010 — Single overlay HWND, internal action hit testing
Status: Accepted for proof

Use one compact overlay HWND registered as one `IDropTarget`. During `DragOver`, convert screen coordinates to overlay coordinates and determine the hovered action tile. Do not create one HWND per action unless testing proves necessary.

### ADR-011 — Never return MOVE for an MVP SmartDrag action
Status: Accepted

SmartDrag is non-destructive. If `DROPEFFECT_COPY` is not among source-allowed effects, the SmartDrag target should reject the drop (`DROPEFFECT_NONE`) rather than report `MOVE`.

### ADR-012 — Overlay proof before production UI stack
Status: Accepted

The first Windows proof should use the smallest native surface practical (raw HWND + simple drawing) and validate drag/focus/DPI behavior before selecting a production presentation stack.

### ADR-013 — Per-monitor DPI v2
Status: Accepted

The Windows process/window path should be Per Monitor v2 DPI aware. Screen-coordinate and DIP conversion must be explicit at boundaries.

## 7. Windows drag/drop facts that constrain the design

1. OLE drag/drop is source-target based: `IDropSource`, `IDropTarget`, and `DoDragDrop` cooperate during an operation.
2. A target registers an HWND with `RegisterDragDrop`.
3. `DragEnter` provides `IDataObject`; `DragOver` provides current screen coordinates but not a new data object; retain the data object only for the drag lifetime.
4. The target receives callbacks only when the cursor is over that target window.
5. `RegisterDragDrop` requires OLE initialization and a pumping message loop.
6. A generic global mouse hook does not expose the active OLE `IDataObject`.
7. Windows accessibility exposes drag start/cancel/complete events, but these still do not directly provide the OLE data object.

## 8. Proposed drag detector design

### 8.1 WinEventDragSignalSource

Responsibilities:
- install `SetWinEventHook` for drag start/cancel/complete;
- skip own process events;
- capture event timestamp, HWND, object ID, child ID, source PID/TID;
- enqueue a small immutable signal to the application thread;
- do NO heavy COM/file work inside the callback;
- guard callback delegate lifetime in managed code;
- support reentrancy/out-of-order event defense.

Output:

```csharp
public sealed record DragSignal(
    DragSignalKind Kind,
    nint SourceHwnd,
    int ObjectId,
    int ChildId,
    uint SourceThreadId,
    uint EventTimeMs);
```

### 8.2 Drag candidate qualification

On drag start signal:
- create candidate session;
- determine source process/window;
- begin temporary cursor sampling;
- attempt lightweight accessible/source classification;
- apply suppression rules;
- after minimum delay/travel threshold show overlay if confidence is high enough.

No payload file content is decoded during qualification.

### 8.3 Cursor tracking

Preferred first proof: `GetCursorPos` polling only while a drag candidate/session is active (e.g. 30–60 Hz), instead of a permanent low-level mouse hook.

Reasons:
- less invasive;
- enough for overlay placement;
- idle cost is zero when no drag is active;
- native OLE target callbacks provide exact coordinates once cursor enters SmartDrag.

## 9. Payload knowledge strategy

There are two confidence stages.

### Hint stage (before cursor reaches SmartDrag)

Potential sources, in order to research:
1. Accessible object identified by `EVENT_OBJECT_DRAGSTART` (name/role/state).
2. Explorer source-window selection resolver using documented Shell/UI Automation interfaces.
3. Conservative extension/category inference only when sufficiently reliable.

The hint stage may be wrong; it must never trigger processing.

### Authoritative stage (cursor enters SmartDrag overlay)

`IDropTarget::DragEnter` receives the actual `IDataObject`.

At this point:
- call `QueryGetData` for `CF_HDROP`;
- if supported, read the file drop list;
- MVP requires exactly one regular file;
- classify input;
- recompute available actions;
- if the prior hint disagreed, immediately update the overlay;
- unsupported input => `DROPEFFECT_NONE`.

The action execution request is built from the authoritative payload, never from an accessibility hint.

## 10. Overlay HWND design

Proof window style proposal:

Base:
- `WS_POPUP`

Extended:
- `WS_EX_TOOLWINDOW`
- `WS_EX_TOPMOST`
- `WS_EX_NOACTIVATE`
- `WS_EX_LAYERED` if alpha/rounded rendering requires it

Window movement/showing should use `SetWindowPos(..., HWND_TOPMOST, ..., SWP_NOACTIVATE | ...)`.

Do NOT assume `WS_EX_TRANSPARENT` means global click-through; its documented behavior is primarily paint-order related. Do NOT rely on `HTTRANSPARENT` as cross-process pass-through; documentation limits its pass-through behavior to underlying windows on the same thread.

Therefore the safest geometry is a compact overlay positioned away from the cursor until the user intentionally moves onto it.

## 11. Overlay interaction model

1. Native drag starts.
2. SmartDrag detects a high-confidence drag signal.
3. Compact overlay appears offset from cursor without activation.
4. Native destination under cursor remains unaffected because overlay does not cover the cursor.
5. User ignores SmartDrag -> native drag continues and SmartDrag disappears at cancel/complete.
6. User intentionally moves cursor onto overlay -> overlay becomes current OLE drop target.
7. `DragEnter(IDataObject)` authoritatively inspects payload.
8. `DragOver` highlights action tile under cursor.
9. User releases -> `Drop` commits selected action if valid.
10. SmartDrag reports `DROPEFFECT_COPY` only when copy is allowed.
11. Overlay hides.
12. Processing job starts outside the live drag lifecycle.
13. Completion UI appears when processing finishes.

## 12. IDropTarget behavioral contract

### DragEnter
- retain COM reference to current `IDataObject` for this drag only;
- inspect `CF_HDROP` availability;
- resolve authoritative file list;
- validate MVP cardinality/category;
- choose candidate action based on cursor coordinates;
- set effect to `COPY` only if payload/action is valid and source allows copy; otherwise `NONE`.

### DragOver
- no file decoding;
- hit-test action tile;
- update hover visuals;
- update effect (`COPY` or `NONE`).

### DragLeave
- clear retained data-object reference;
- clear hover state;
- keep or hide overlay according to coordinator state.

### Drop
- revalidate selected action and authoritative file list;
- create immutable `ActionRequest`;
- return non-destructive effect;
- release data object;
- detach processing from drag session;
- enqueue job.

## 13. DPI and monitor design

- set Per Monitor v2 awareness before UI creation;
- use `MonitorFromPoint` for current cursor monitor;
- use `GetMonitorInfo` work area for clamping;
- Win32 drag points are screen coordinates;
- domain/UI layout may use DIPs, with explicit conversion;
- respond to `WM_DPICHANGED` using suggested bounds and recalculate overlay layout;
- when cursor crosses monitors, recalculate scale and placement.

Placement engine inputs:
- cursor screen point;
- monitor work area;
- current DPI;
- overlay desired size;
- preferred offset/side;
- edge-safe fallback placement.

## 14. Focus and activation rules

The overlay must never call:
- `SetForegroundWindow`;
- `SetActiveWindow`;
- APIs or framework paths that activate it during a drag.

Show/reposition with non-activating flags.

Important unresolved tension: a strict `WS_EX_NOACTIVATE` overlay cannot use normal focus-based keyboard navigation. The master requirement for keyboard accessibility therefore needs an explicit alternate design/proof (for example a separate keyboard command surface or non-focus command mechanism). Do not silently break the no-focus invariant to satisfy keyboard navigation.

## 15. Rejected/unsafe baseline approaches

### Full-screen transparent drop target
Rejected.
Reason: it becomes the OLE target and steals native destination behavior.

### Tiny invisible HWND permanently under cursor
Rejected as baseline.
Reason: once cursor is over it, it can become the OLE drop target; this is exactly what SmartDrag must avoid until user intent is explicit.

### Explorer DLL injection / detouring `DoDragDrop`
Rejected.
Reason: unsupported/high-risk integration, crash/security/antivirus/maintenance cost.

### Always-on low-level global mouse hook as primary detector
Not selected for baseline.
Reason: it can tell us mouse motion/button state but not the OLE payload. Use only if proof demonstrates a specific missing signal that cannot be solved more safely.

## 16. Windows proof P0 — mandatory before production overlay

Create a standalone `SmartDrag.Windows.Probe` executable.

P0-1: WinEvent logger
- log drag start/cancel/complete event IDs;
- log source HWND/PID/TID/object IDs;
- optional accessible name/role outside callback;
- test Explorer window and desktop.

P0-2: Non-activating overlay
- raw compact HWND;
- topmost + noactivate + toolwindow;
- show/reposition without foreground change;
- draw simple action rectangles.

P0-3: OLE drop target
- `OleInitialize` on owning STA/message-loop thread;
- `RegisterDragDrop` on overlay HWND;
- inspect `CF_HDROP` during DragEnter;
- do not log full source paths; diagnostic logs record counts/extensions/action IDs only;
- reject everything except one local image file;
- return COPY only; never MOVE.

P0-4: End-to-end coexistence
- ignore overlay and perform normal Explorer drop;
- move onto overlay and drop onto fake action;
- verify source file remains;
- verify foreground window never becomes SmartDrag;
- verify cancel with Esc dismisses SmartDrag.

## 17. P0 test matrix

Windows environments:
- Windows 11 current supported build;
- Windows 10 only if project support policy later includes it.

Explorer surfaces:
- normal Explorer file list;
- desktop icons;
- two Explorer windows;
- drag into folder;
- drag to desktop;
- drag from search results if applicable;
- OneDrive-synced file placeholder/local file cases later.

DPI/monitor:
- 100%;
- 125%;
- 150%;
- two monitors same DPI;
- mixed DPI;
- negative virtual-screen coordinates (monitor left of primary);
- crossing monitor while dragging.

Drag lifecycle:
- short movement below threshold;
- long drag;
- Esc cancel;
- native drop without touching overlay;
- enter/leave overlay repeatedly;
- drop on valid action;
- drop on empty area inside overlay;
- source allows copy;
- source does not allow copy (must reject).

Stability:
- Explorer restart;
- SmartDrag restart;
- rapid repeated drags;
- overlay creation failure;
- WinEvent signal missing;
- invalid/late accessible object;

## 18. P0 acceptance criteria

P0 passes only if:
1. native Explorer drag remains functional when SmartDrag is ignored;
2. overlay never becomes foreground/active during passive presentation;
3. SmartDrag receives real `IDataObject` only when user intentionally enters overlay;
4. one-file `CF_HDROP` can be resolved reliably at DragEnter;
5. SmartDrag never reports MOVE;
6. Esc/native drop reliably ends/hides the session;
7. mixed-DPI positioning is correct enough to keep overlay on the intended monitor/work area;
8. no Explorer injection or source-process modification is required;
9. CPU usage returns to idle after session end;
10. missing drag-start events fail open (no interference with native drag).

## 19. Open questions — must not be guessed

OQ-001: Does current Windows Explorer reliably emit `EVENT_OBJECT_DRAGSTART`, `DRAGCANCEL`, and `DRAGCOMPLETE` for file drags from both folder views and desktop?

OQ-002: What accessible object/name/role does Explorer expose for single-file and multi-selection drags?

OQ-003: Can a documented Shell selection resolver map the WinEvent source to exact selected file paths reliably enough to pre-classify actions before DragEnter?

OQ-004: If the drag-start WinEvent is absent/inconsistent, what is the least invasive fallback signal?

OQ-005: How should the keyboard-accessibility requirement coexist with a non-activating drag overlay?

OQ-006: Does a raw Win32 overlay need a custom window region for transparent corners so OLE hit testing matches visuals?

OQ-007: Which production renderer (raw Win32/Direct2D, WPF hosted HWND, WinUI 3, other) preserves the proven interop behavior with the smallest maintenance cost?

## 20. Next steps after P0 design

1. Specify the exact probe project and P/Invoke/COM types.
2. Implement WinEvent logger skeleton.
3. Implement raw no-activate HWND host.
4. Implement minimal managed `IDropTarget` bridge.
5. Add diagnostic event timeline.
6. Run/record proof matrix.
7. Only after proof, select production overlay presentation stack.
8. Then proceed to authoritative image payload/action pipeline.



## 21. Generated repository checkpoint — v0.2

A concrete repository scaffold has now been generated.

### Build verification status

`UNVERIFIED BUILD` in the generation environment because no .NET SDK was installed there. This is explicit evidence debt, not a pass.

The first Windows machine with .NET 8 must run:

```powershell
./scripts/build.ps1
```

Any compiler/test failure becomes a blocking issue before P0 manual testing.

### Implemented code boundaries

- `SmartDrag.Core`: domain primitives, payload models, action contracts, safe output policy, pure drag-state reducer.
- `SmartDrag.Actions`: duplicate-safe registry, MVP action definitions, handler executor.
- `SmartDrag.Imaging`: contracts only; codec dependency intentionally deferred.
- `SmartDrag.Infrastructure`: deterministic output-name planning.
- `SmartDrag.Windows`: WinEvent drag signal source, OLE initialization/registration, managed IDropTarget contract, CF_HDROP reader, non-destructive drop-effect policy, JSONL timeline logger.
- `SmartDrag.Windows.Probe`: raw no-activate overlay, threshold qualifier, Explorer-only P0 filtering, action hit testing, authoritative CF_HDROP validation, fake action commit logging.
- xUnit tests for drag state transitions, action registry, and COPY-vs-MOVE policy.

### New invariant

The P0 probe must not process or mutate the dropped file. Its only purpose is to prove Windows coexistence and collect evidence. Image processing remains blocked behind the P0 gate.

### Evidence hygiene

Before a coding agent is given this repository, the handoff must clearly distinguish:

- accepted architecture decisions;
- provisional hypotheses;
- code that compiled/tested;
- code that is only statically generated;
- manual Windows proof results;
- remaining open questions.


## 22. P0 repository evidence discipline

The repository now contains `PROJECT_STATE.md`, `docs/testing/BUILD_GATE.md`, and `docs/research/WINDOWS_API_EVIDENCE.md`.

The P0 WinEvent callback was refined so it only captures/enqueues immutable signals. Process lookup, logging, coordinator work, and overlay changes are deferred to the normal message-loop timer. This follows the documented reentrancy constraints of WinEvent callbacks.

`SmartDrag.Core` targets plain `net8.0`; only Windows-specific assemblies target `net8.0-windows`. This preserves the architecture invariant that Core does not depend on the Windows platform surface.

Full source paths are intentionally excluded from probe JSONL diagnostics.

## 23. Runtime / output foundation checkpoint — v0.4

P0 remains the hard product/interop gate, but post-commit runtime foundations have now been implemented behind it so a successful Windows proof does not force a redesign.

### 23.1 New project: SmartDrag.Runtime

`SmartDrag.Runtime` depends only on `SmartDrag.Core` and contains `SequentialJobQueue`.

MVP job policy:
- one consumer / at most one action executing at once;
- thread-safe enqueue from multiple callers;
- immutable `JobSnapshot` query surface;
- lifecycle event for UI/completion observers;
- cooperative per-job cancellation;
- queued cancellation prevents handler execution;
- running cancellation enters `Cancelling` then terminal `Cancelled` when honored;
- observer exceptions are isolated so UI listeners cannot terminate the worker;
- disposing the queue closes intake and requests cancellation for non-terminal jobs.

This is ADR-015. Parallelism is deliberately not an MVP requirement.

### 23.2 Action executor hardening

`ActionExecutor` now:
- rejects duplicate `IActionHandler` registrations at composition time;
- returns a typed failure for unknown ActionId;
- maps cooperative cancellation to `OperationCancelled`;
- contains unexpected handler exceptions instead of allowing them to tear down the job worker.

Stable action IDs moved into `SmartDrag.Core.Actions.BuiltInActionIds` so definitions and concrete handlers share one canonical declaration:

```text
image.compress
image.convert.webp
image.remove-metadata
```

### 23.3 Output safety contracts

New Core contracts:
- `IOutputManager`;
- `OutputRequest`;
- `OutputReservation`;
- reservation / commit / cleanup result types.

`OutputPolicy` now carries the optional configured directory path required by its pre-existing `ConfiguredDirectory` mode.

### 23.4 PhysicalOutputManager safety rules

Implemented in `SmartDrag.Infrastructure`.

Mandatory behavior:
1. `PreserveSource=false` is rejected for MVP.
2. Source must still exist when reserving output.
3. Output may never resolve to the same path as source.
4. Convert-to-WebP may legitimately use an empty suffix because the changed extension keeps output distinct (`photo.png -> photo.webp`).
5. Generated output never overwrites an existing file.
6. `GenerateUniqueName` is checked during reservation and again at commit to close the collision race.
7. A SmartDrag-owned partial artifact is created conceptually in the destination directory and passed to the processor.
8. Finalization uses `File.Move(..., overwrite: false)`.
9. Cancellation/failure cleanup does not inherit the already-cancelled operation token; cleanup must still be attempted.
10. Source path is never deleted or truncated by output-management code.

Example temporary name:

```text
.photo-compressed.smartdrag-<guid>.partial.png
```

This is ADR-016.

### 23.5 Image action orchestration exists without a codec

`SmartDrag.Imaging` now contains real handlers:
- `CompressImageActionHandler`;
- `ConvertImageToWebPActionHandler`;
- `RemoveImageMetadataActionHandler`.

They implement the full safe orchestration:

```text
ActionRequest
  -> validate exactly one MVP input
  -> reserve output
  -> invoke IImageProcessor into temporary path
  -> on failure: abandon temp
  -> on cancellation: abandon temp, rethrow cancellation
  -> on success: commit with overwrite=false
  -> return final path
```

No real codec is package-referenced. The P0 probe still does not process files.

### 23.6 Codec research update

As of 2026-08-31:
- NuGet reports `SkiaSharp 4.151.1` as a stable package released 2026-08-05 under MIT;
- the Win32 native-assets package is large and must be measured in packaging tests;
- NuGet reports `SixLabors.ImageSharp 4.1.1` as current stable released 2026-08-20;
- Six Labors documentation states that starting with ImageSharp 4.0.0, direct package dependencies require a valid Six Labors license at build time, with commercial licensing conditions described separately.

Therefore ADR-017 is **Provisional**:
- no codec dependency before G3;
- SkiaSharp is the first candidate to benchmark because licensing is operationally simpler;
- behavior, metadata fidelity, memory, WebP, security, cancellation, and package-size proof can still reject it.

A future coding agent must not add ImageSharp/SkiaSharp/FFmpeg merely because a candidate is named in documentation.

### 23.7 Generated tests added

New source tests cover:
- runtime successful completion;
- FIFO single-concurrency behavior;
- running-job cancellation;
- failed action propagation;
- destructive output-policy rejection;
- same-directory output planning;
- extension-only conversion naming;
- collision appearing between reservation and commit;
- partial-file cleanup;
- configured-directory validation;
- image action commit orchestration;
- processor-failure cleanup;
- cancellation cleanup;
- multi-input rejection for non-batch MVP handlers;
- ActionExecutor exception containment and duplicate handler rejection.

They remain **UNEXECUTED** until G0 because this generation environment has no .NET SDK.

### 23.8 Static repository validator

`scripts/validate_repo.py` now provides a no-SDK integrity gate. At v0.4 generation it passes and verifies:
- every `.csproj` is valid XML;
- all ProjectReference targets exist;
- all projects appear in `SmartDrag.sln`;
- no codec or FFmpeg dependency has leaked in before G3;
- required canonical/handoff/runtime/test artifacts exist;
- stable MVP ActionIds are centrally declared;
- build-output directories are absent from the handoff package.

This static pass is useful evidence but is **not** equivalent to compiling C# or running xUnit.

## 24. New/updated open questions after v0.4

- OQ-008: Which codec passes G3? SkiaSharp is only the first candidate.
- OQ-012: Exact per-format Compress semantics.
- OQ-013: First shipping source-format matrix beyond mandatory PNG/JPEG evaluation.
- OQ-014: Decode limits for dimensions/pixels/memory/decompression-bomb defense.
- OQ-015: Startup recovery policy for stale SmartDrag-owned partial artifacts.

## 25. Next implementation order

Do not skip gates. Current intended order:

1. On a Windows/.NET 8 environment, pass G0 build/tests.
2. Run P0 Explorer coexistence matrix and record actual WinEvent/OLE evidence.
3. Resolve G2 pre-overlay qualification strategy from those observations.
4. Select production overlay renderer only after P0 proof.
5. Run G3 image-codec spike using a controlled image corpus.
6. Implement a concrete `IImageProcessor` only after G3.
7. Wire committed overlay action -> `ActionRequest` -> `SequentialJobQueue` in `SmartDrag.App`.
8. Add completion model/UI and cancellation UI.
9. Add startup partial-artifact recovery only after OQ-015 is resolved.

The repository is intentionally ahead in contracts/orchestration but not allowed to fake completion of the two highest-risk proofs: Windows coexistence and codec behavior.

## 26. Future coding-agent entry discipline

A dedicated `docs/handoff/CURSOR_ENTRY_PROTOCOL.md` now defines the eventual coding-agent trust order, first-session sequence, hard prohibitions, and ADR change discipline. `docs/handoff/ARTIFACT_INDEX.md` provides a navigable inventory of canon, state, decisions, tests, research, source ownership, and validation scripts.

These files are preparation only. Cursor remains intentionally unused at the current stage.

## 27. Foundation v0.5 — payload authority, G2 preflight, placement, completion

### 27.1 Critical correction: P0 signal-only overlay is not production behavior

The earlier Windows proof could present the overlay after an Explorer `EVENT_OBJECT_DRAGSTART` plus time/movement thresholds, before knowing the actual file payload. That is useful for isolating Win32/OLE coexistence, but it does **not** satisfy the canonical requirement that normal overlay presentation requires a supported payload.

ADR-018 therefore makes production qualification fail-closed. `SmartDrag.Windows.Probe` now has two explicit modes:

```text
--mode=p0  -> interop-only; signal-based presentation permitted for diagnostics
--mode=g2  -> strict preflight required before overlay presentation
```

A future coding agent must never copy the P0 relaxation into production.

### 27.2 Payload evidence has explicit authority levels

New Core contracts model:

```text
PayloadEvidenceSource:
  Unknown
  ExplorerSelectionSnapshot
  OleDataObject
  AccessibilityEvent

PayloadQualificationState:
  Unknown
  Eligible    // enough preflight evidence to show overlay
  Supported   // authoritative OLE Drop payload
  Rejected
```

The current MVP pre-G3 qualifier allows only exactly one existing regular `.png`, `.jpg`, or `.jpeg` path. This intentionally conservative matrix can only expand after G3 proves codec behavior. Payload qualification alone is not sufficient for production presentation: `OverlayEligibilityEvaluator` must also see at least one currently available ActionRegistry action. The fixed three probe tiles are a diagnostic-only exemption.

### 27.3 Two-stage identity contract

ADR-019 fixes the architecture:

```text
preflight evidence -> Eligible -> overlay may appear
                     ... native drag continues ...
OLE Drop CF_HDROP   -> Supported -> path must equal preflight path
                                     -> commit action
```

Mismatch, ambiguity, missing evidence, multiple files, directories, or unsupported extensions fail closed.

The current Explorer preflight implementation uses documented Shell automation (`ShellFolderView.SelectedItems`) after mapping the WinEvent source to its top-level HWND. This implementation is **PROVISIONAL** and must pass G2 across Windows versions, Explorer tabs, Desktop, hidden extensions, races, and virtual items.

### 27.4 Shell data rendering moved out of DragEnter

`ProbeDropTarget.DragEnter` now calls `QueryGetData` through `FileDropDataReader.CanRead` to establish `CF_HDROP` availability. It does not extract the complete path list merely for hover feedback. `TryReadPaths` is deferred to `Drop`, where the data must actually be incorporated/validated.

This reduces work inside the OLE drag loop and better matches Shell guidance.

### 27.5 WinEvent callback minimized further

The WinEvent callback no longer performs process-id lookup. It only maps event type, captures primitive event fields/timestamp, and enqueues an immutable `DragSignal`. Process lookup, Explorer filtering, Shell automation, logging, and UI work occur later on the message-pump/tick path.

### 27.6 Pure overlay placement engine

Placement math moved from the raw probe window into `SmartDrag.Core.Overlay.OverlayPlacementEngine`. It is unit-agnostic and tested for:

- preferred bottom-right placement;
- bottom/right edge flipping;
- negative virtual-desktop coordinates;
- overlays larger than the work area.

The Win32 probe still owns DPI conversion and monitor work-area discovery. Before its first visible placement it moves the hidden HWND to the cursor monitor, then queries `GetDpiForWindow`, reducing the known first-frame mixed-DPI ambiguity while keeping final behavior subject to G1 evidence. Production renderer selection remains gated by G1.

### 27.7 Completion data path exists before completion UI

`ActionResult` can now carry optional `ActionMetrics` (`SourceSizeBytes`, `OutputSizeBytes`). `SequentialJobQueue` preserves request input paths, result output paths, and metrics in immutable terminal `JobSnapshot` instances.

`CompletionProjector` converts terminal snapshots into `CompletionModel` without touching processing or filesystem code. Success can expose:

- Open containing folder;
- Copy result path;
- Start result drag;
- Delete generated output;
- Dismiss.

Failure/cancellation currently expose only Dismiss; retry semantics remain intentionally undesigned.

`DeleteGeneratedOutput` is additionally gated by explicit `GeneratedOutputPaths` ownership carried from `ActionResult` into the terminal job snapshot. Success alone is not permission to delete an arbitrary output path.

This creates the data path for the canonical/viral `15 MB -> 1.2 MB` completion presentation without coupling codecs to UI.

### 27.8 New G2 evidence gate

See:

- `docs/windows/G2_PREOVERLAY_QUALIFICATION.md`
- `docs/testing/G2_QUALIFICATION_TEST_MATRIX.md`
- ADR-018
- ADR-019

If the current Shell selection strategy fails Desktop or Windows 11 tabbed Explorer, the correct response is to research a conservative documented fallback or reduce first-release surface support. The forbidden response is to show the production overlay from drag-start alone.

### 27.9 Probe evidence is now machine-summarizable

`docs/windows/PROBE_EVENT_SCHEMA.md` defines the privacy-safe JSONL contract. `scripts/analyze_probe_log.py` summarizes event counts and automatically flags foreground changes, malformed entries, G2 logs contaminated by signal-only mode, and preflight/Drop mismatches. Manual matrix review remains mandatory.

### 27.10 Build status remains explicit

All new C# source is statically reviewed and repository integrity is validated, but no .NET SDK exists in the generation environment. G0 remains `UNVERIFIED BUILD`; xUnit source tests are not represented as executed.

### 27.11 Native boundaries are explicitly no-throw

ADR-021 makes WinEvent callbacks, the raw WndProc, and OLE `IDropTarget` callbacks no-throw boundaries. OLE faults force `DROPEFFECT_NONE`; diagnostic fault entries contain exception type/HRESULT only, not exception messages or source paths. `TimelineLogger.Write` is best-effort/no-throw after construction. Probe shutdown records dropped-log and dropped-WinEvent counters, and the analyzer treats native-boundary faults/dropped evidence/MOVE effects as red flags.

## 28. New/updated open questions after v0.5

- OQ-016: Windows 11 tab mapping reliability for `ShellFolderView.SelectedItems`.
- OQ-017: documented Desktop preflight strategy if ShellWindows mapping fails.
- OQ-018: whether first shipping MVP should explicitly exclude Desktop if G2 cannot be satisfied there.
- OQ-019: which modeled completion commands enter the first visual completion slice.

## 29. Next implementation order after v0.5

1. Pass G0 on an actual Windows/.NET 8 machine.
2. Run `--mode=p0` G1 coexistence matrix and retain logs.
3. Run `--mode=g2` G2 qualification matrix and determine exact supported Explorer surfaces.
4. Only after G1/G2 evidence, choose/implement production overlay renderer and source-preflight mechanism.
5. Run G3 controlled codec spike; do not package a production codec first.
6. Implement concrete `IImageProcessor` after G3.
7. Compose production App coordinator: qualified drag -> overlay -> authoritative Drop -> `ActionRequest` -> JobQueue.
8. Implement completion command handlers with generated-output ownership checks.

## 30. Foundation v0.6 — production orchestration and G3 policy

### 30.1 A shown overlay is now an authorization snapshot

`SmartDrag.Orchestration.PreparedOverlaySession` captures the exact state that justified showing SmartDrag:

```text
DragSessionId
PreflightQualification
OfferedActionIds
OverlayModel
```

This is not merely a view model. It is the authorization input for the final Drop gate.

### 30.2 Final Drop cannot directly create arbitrary work

`DragWorkflowOrchestrator.TryCommitDrop` is the mandatory deterministic gate between OLE and `ActionRequest`.

An accepted decision requires all of the following:

```text
prepared overlay session exists
AND authoritative qualification == Supported
AND CF_HDROP path matches preflight path
AND selected ActionId was offered in that exact overlay
AND ActionId is still available for authoritative payload/capabilities
AND OutputPolicy.PreserveSource == true
AND exactly one authoritative input path
```

Failure returns a typed reject status and **no ActionRequest**.

This closes several races that a future agent must not reintroduce:

- stale overlay from drag N executing against drag N+1;
- UI supplying an ActionId that was not offered;
- codec/capability disappearing after the overlay was shown;
- preflight selection changing before OLE Drop;
- destructive output policy entering through an adapter mistake.

### 30.3 Dispatch is separate from commit

`CommittedActionDispatcher` accepts only an `ActionCommitDecision` whose `IsAccepted` is true, then performs one `IJobQueue.Enqueue`.

Native/UI code should therefore follow:

```text
Drop evidence
  -> TryCommitDrop
  -> if rejected: DROPEFFECT_NONE / dismiss as appropriate
  -> if accepted: return permitted native effect + dispatch exactly once
```

The exact ordering around OLE return/dispatch must be finalized during Windows integration so processing never blocks the OLE callback; the safety rule is that no rejected decision reaches JobQueue.

### 30.4 Codec-neutral image execution guard exists

`SmartDrag.Imaging` now contains:

- `IImageInspector`;
- `ImageInspectionResult`;
- `ImageSafetyLimits`;
- `MvpImageExecutionGuard`;
- `MvpImageActionSemantics`.

The extension allowlist remains preflight-only. A real G3 codec adapter must inspect content before full decode and report format/dimensions/frame count/metadata facts.

Numeric memory/resource thresholds are deliberately not hard-coded into the library. G3 must benchmark them and composition/configuration supplies the accepted values.

### 30.5 Canonical image semantics

For all actions:

- source remains untouched;
- visible orientation is preserved;
- animation/multi-frame input is rejected in the first slice;
- ICC/color-management data is preserved when representable;
- pixel dimensions are preserved except an orientation-normalization width/height swap.

`Remove Metadata` specifically:

```text
EXIF orientation present
  -> normalize visible orientation into pixels if required
  -> remove orientation/non-visual EXIF/XMP/IPTC/text metadata
  -> preserve ICC where representable
```

A future codec implementation must not equate "remove metadata" with blindly stripping every ancillary block.

### 30.6 Deterministic G3 corpus

`tests/fixtures/images/` now contains nine committed fixtures generated by `tools/generate_image_corpus.py`:

- regular JPEG;
- regular PNG;
- EXIF Orientation=6 JPEG;
- ICC-profile JPEG;
- textual-metadata PNG;
- alpha PNG;
- animated GIF negative case;
- fake `.png` text file;
- truncated JPEG.

`CORPUS_MANIFEST.json` pins byte size and SHA-256. `scripts/validate_image_corpus.py` verifies fixture integrity without a production image codec.

Every future codec candidate must run the same G3 matrix against this corpus. Codec bugs discovered later should become new minimal fixtures before the fix is accepted.

### 30.7 Current codec research snapshot

As of 2026-08-31:

- SkiaSharp `4.151.1` is the current stable NuGet version observed and is identified by NuGet as MIT licensed; Win32 native assets must be measured for publish footprint.
- SixLabors.ImageSharp `4.1.1` is the current stable NuGet version observed and targets .NET 8+; Six Labors documents build-time license-key enforcement for direct ImageSharp 4 dependencies.

Neither is accepted. Do not add either package merely to make `IImageProcessor` concrete.

### 30.8 New ADRs

- ADR-022 — portable production orchestration layer;
- ADR-023 — prepared overlay session as authorization snapshot;
- ADR-024 — codec-independent image semantics;
- ADR-025 — metadata removal preserves orientation/ICC;
- ADR-026 — content inspection and configurable resource limits before full image processing;
- ADR-027 — one drag session -> one idempotent ActionRequest;
- ADR-028 — prepared/accepted authorization objects are non-forgeable capability objects.

### 30.9 Duplicate-delivery hardening

The one-action drag MVP uses `DragSessionId` as `ActionRequest.RequestId`. This gives every physical drag a stable idempotency key. `CommittedActionDispatcher` returns the same JobId for an equivalent repeated accepted decision and rejects a conflicting action for the same RequestId. `SequentialJobQueue` independently applies the same rule so bypassing the dispatcher cannot create duplicate work.

`PreparedOverlaySession` and `ActionCommitDecision` are constructed only inside `SmartDrag.Orchestration`; external Windows/UI adapters cannot manufacture an Accepted decision through normal typed code. The offered ActionId set is frozen and authorization preflight evidence is internal.

## 31. Updated open questions after v0.6

Windows G1/G2 questions remain unresolved. Image questions are narrowed:

- exact JPEG compression encoder settings require corpus evidence;
- exact WebP mode/quality/effort requires corpus evidence;
- exact resource limits require measured peak memory/runtime;
- PNG `Compress` no-benefit behavior remains a product decision;
- additional first-release image input formats remain out of scope until explicitly accepted.

The earlier metadata-orientation/ICC question is resolved by ADR-025.

## 32. Next implementation order after v0.6

1. Pass G0 on Windows/.NET 8 and fix every compile/test error before proceeding.
2. Run G1 P0 coexistence matrix with retained JSONL evidence.
3. Run G2 strict qualification matrix and decide exactly which Explorer/Desktop surfaces are supportable.
4. Wire the proven Windows adapter into `DragWorkflowOrchestrator`; do not duplicate commit logic in UI/OLE code.
5. Prove P2 end-to-end integration: one authorized Drop -> one accepted request -> one queued job; all stale/mismatch cases -> zero jobs.
6. Open G3 codec spikes against the common corpus, measure both candidate behavior and packaging/licensing constraints.
7. Select codec + numeric safety limits by ADR, then implement concrete `IImageInspector`/`IImageProcessor`.
8. Add first real completion visual surface and command adapters after runtime output ownership remains proven.

Until steps 1–6 have evidence, production startup remains intentionally blocked.

## 33. Foundation v0.7 — Recovery, interaction lifetime, and completion execution

### 33.1 Active interaction ownership

`DragInteractionCoordinator` is now the only application-level owner of the active `PreparedOverlaySession`. A prepared capability is bound to one `DragSessionId`, superseded by a newer drag, and consumed before async hide/dispatch during a commit attempt. A second commit after consumption is rejected. Native drag end/cancel clears the capability even if overlay cleanup fails.

This complements, rather than replaces, v0.6 RequestId idempotency. The layers are deliberately redundant:

```text
active capability consumed once
  -> commit gate
  -> dispatcher RequestId dedupe
  -> JobQueue RequestId dedupe
```

### 33.2 Durable output recovery

Every output reservation must be journaled before processing can start. Production uses `JsonOutputRecoveryJournal`; tests may use `InMemoryOutputRecoveryJournal`. The journal stores only reservation id, exact partial path, and timestamp.

Partial filenames are reservation-bound:

```text
.<target>.smartdrag-<ReservationId:N>.partial<extension>
```

Startup recovery may delete only exact journaled paths that still match this convention. It never enumerates user folders looking for `*.partial` files. Invalid records authorize zero filesystem deletion. Corrupt/unavailable journal state fails closed and may leave an orphan rather than guess.

### 33.3 Startup safety order

Production composition must run:

```text
named single-instance mutex
  -> durable journal
  -> stale-partial recovery
  -> OutputManager/runtime/actions
  -> Windows hooks + overlay
```

`StartupSafetyBootstrap` implements the safety bootstrap but `Program.Main` remains intentionally blocked until Windows evidence gates pass.

### 33.4 Completion commands are capabilities, not enum promises

`CompletionProjector` now requires a runtime `CompletionCapabilities` snapshot. `CompletionCoordinator` publishes one terminal completion per JobId, and `CompletionCommandExecutor` revalidates that a command is still offered at execution time.

v0.7 target capabilities:

- Open containing folder: enabled by Windows adapter;
- Copy result path: conditionally enabled by the Windows Unicode clipboard adapter only when composition supplies a real owner HWND;
- Start result drag: disabled until production OLE drag-source proof;
- Delete generated output: hard-disabled until strong file identity verification is implemented;
- Dismiss: always enabled.

Generated-output historical path ownership is not sufficient for deletion because the file at that path can be replaced after completion. Never re-enable delete using size/timestamp heuristics.

### 33.5 New ADRs

- ADR-029 — durable output recovery journaling before reservation;
- ADR-030 — journal-only recovery, never user-directory scans;
- ADR-031 — capability-gated completion and delete safety hold;
- ADR-032 — single consumable active drag authorization;
- ADR-033 — single-instance/recovery-before-hooks startup order.

## 34. Next implementation order after v0.7

1. Pass G0 on real Windows/.NET 8 and fix compile/test failures before accepting any new runtime feature.
2. Execute G1 and G2 with retained probe JSONL evidence.
3. Wire proven Windows G2 signals/OLE Drop into `DragInteractionCoordinator`; native callbacks must enqueue/hand off rather than block on processing.
4. Run P2/P3 integration assertions: one physical drag -> one active capability -> one accepted request -> one JobId -> one completion.
5. Kill the process during a real partial write and prove journal recovery leaves non-journaled neighboring files untouched.
6. Open G3 codec spikes only after G0 works; use the committed corpus and resource/packaging matrix.
7. Design strong Windows generated-artifact identity before enabling Delete Generated Output.
8. Prove a production result-drag source separately before enabling Start Result Drag.

### 34.1 Recovery failure is a runtime-start blocker

`StartupSafetyBootstrap` returns `RecoveryBlocked` / `RuntimeMayStart=false` whenever stale-partial recovery remains unresolved. Future composition must not treat this as a telemetry warning and proceed to hooks.

## 35. Foundation v0.8 — Strong generated-artifact identity and authoritative composition

### 35.1 Generated output is now an artifact, not a path ownership claim

`ActionResult` and terminal `JobSnapshot` now carry `GeneratedArtifact[]`:

```text
GeneratedArtifact
  Path
  Identity?
```

Bare path equality is explicitly insufficient for destructive operations.

Windows identity scheme v1 is `windows-file-id-v1` and stores the pair documented by `FILE_ID_INFO`:

```text
VolumeSerialNumber + 128-bit FileId
```

The identity is an object identity, not a content hash.

### 35.2 Strong identity is granted only across a verified commit

`PhysicalOutputManager` does not trust a post-move path lookup alone. With an identity provider composed it performs:

```text
capture identity(temporary partial)
        -> File.Move(temp, final, overwrite:false)
        -> capture identity(final)
        -> equal: GeneratedArtifact.Identity = identity
        -> mismatch/failure: GeneratedArtifact.Identity = null
```

The output itself remains successful if identity capture fails. The only lost capability is destructive completion.

This matters because associating identity only after the move still leaves a narrow replacement race between `File.Move` and identity capture.

### 35.3 Delete Generated Output is implemented without a second path lookup

ADR-034 satisfies ADR-031's future gate.

`CompletionProjector` offers Delete only for one output + one matching identity-backed generated artifact + a supported deletion service.

Windows execution:

```text
CreateFileW(path,
  FILE_READ_ATTRIBUTES | DELETE,
  FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
  OPEN_EXISTING,
  FILE_FLAG_OPEN_REPARSE_POINT)
        -> GetFileInformationByHandleEx(FileIdInfo)
        -> compare exact recorded identity
        -> mismatch: refuse, leave object untouched
        -> match: SetFileInformationByHandle(FileDispositionInfo, DeleteFile=TRUE)
        -> close same verified handle
```

Never replace this with:

```text
verify identity
close handle
DeleteFile(path)
```

because that reintroduces TOCTOU between verification and destructive path lookup.

`FILE_FLAG_OPEN_REPARSE_POINT` is intentional: if the output path was replaced by a symlink/reparse point, SmartDrag verifies that object instead of following it to a target.

### 35.4 G3 guard is now executable wiring, not only policy text

A gap found during v0.8 review: v0.6 defined `MvpImageExecutionGuard`, but nothing structurally prevented a future composition from injecting the raw concrete codec directly into image handlers.

`GuardedImageProcessor` now enforces:

```text
IImageInspector
+ ImageSafetyLimits
      -> MvpImageExecutionGuard
      -> rejected: inner codec never called
      -> allowed: delegate to concrete codec
```

Production handlers must receive the guarded processor, never the raw codec.

Numeric limits remain unresolved until G3 benchmarks; v0.8 does not invent them.

### 35.5 One deterministic production composition root

`SmartDrag.App.Composition.ProductionRuntimeGraph` is now the intended authoritative object graph after a clean startup safety bootstrap.

It wires the exact same durable recovery journal into `PhysicalOutputManager`, composes the Windows artifact identity service, wraps the injected future codec with `GuardedImageProcessor`, creates the three MVP handlers, action executor, sequential queue, drag orchestration, completion platform, identity-safe delete executor, and completion coordinator.

The production overlay and concrete codec remain injected because neither is accepted yet.

### 35.6 Native activation remains deliberately impossible by default

`ProductionRuntimeGraph` does not register WinEvent/OLE hooks.

`ProductionActivationPolicy.NativeActivationEnabled` remains compile-time `false`. This is a kill-switch, not a user setting. It must not be flipped simply to make a future Cursor session show a running app.

Enabling it requires accepted evidence for:

- G0 build/tests;
- G1 Explorer coexistence;
- G2 pre-overlay qualification;
- G3 concrete codec + numeric limits;
- corresponding checkpoint/ADR update.

### 35.7 New ADRs / test gate

- ADR-034 — generated-output delete requires Windows file identity and handle-bound disposition;
- ADR-035 — every concrete image codec is wrapped by the execution guard;
- ADR-036 — one production composition root; native activation remains gate-blocked;
- ADR-037 — destructive completion authorization is consumable per JobId;
- `docs/testing/P4_ARTIFACT_IDENTITY_COMPOSITION_TEST_MATRIX.md` — Windows identity/replacement/reparse/composition acceptance.

## 36. Current implementation order after v0.9

1. Pass G0 on real Windows/.NET 8. Fix compilation/marshalling/tests without weakening ADRs.
2. Execute P4 Windows identity tests early because v0.8 added new `CreateFileW` / `GetFileInformationByHandleEx` / `SetFileInformationByHandle` signatures.
3. Execute G1 P0 coexistence matrix and retain JSONL evidence.
4. Execute G2 strict qualification matrix and decide supported Explorer/Desktop surfaces.
5. Wire only the proven G2 adapter into `DragInteractionCoordinator`; keep native activation kill-switch false until evidence is accepted.
6. Open G3 codec spikes with the common corpus; select a codec and numeric limits by ADR.
7. Run the full deterministic graph with the selected codec behind `GuardedImageProcessor`.
8. Only then enable production native activation through an explicit decision/update.
9. Execute P5 presentation/runtime matrix on the selected UI implementation; do not invent numeric progress.
10. Result-drag remains a separate proof; PDF/archive/video/batch remain outside the MVP firewall.

### 36.1 Remaining open safety questions

- whether Windows file identity semantics and handle disposition behave acceptably on all release-target filesystems/network shares must be measured; failure must suppress Delete rather than fall back;
- exact image resource limits remain G3 evidence-driven;
- source-file identity across preflight/authoritative Drop/execution is not yet a destructive-safety requirement because sources are never modified, but may later be useful to prevent processing a replacement file and should not be silently conflated with generated-artifact identity;
- production overlay renderer/framework remains blocked by G1/G2 evidence.


# Foundation v0.9 additions

## Presentation boundary

`SmartDrag.Presentation` is the canonical toolkit-neutral post-commit UX layer. Cursor must not move these rules into a WPF/WinUI view model that directly calls filesystem/codecs/job executors. Views render immutable snapshots and send intents back through `MvpPresentationCoordinator`.

Operation progress is indeterminate until a truthful codec progress contract exists. Do not synthesize percentages. Full user paths must not be placed in status/completion display text.

## Safe application-facing surface

Production native/UI code receives `IProductionDragInteraction`, whose commit method has no `OutputPolicy` argument. `MvpOutputPolicyFactory` always preserves source for MVP. `ProductionRuntimeGraph` deliberately hides raw JobQueue, DragWorkflow, CompletionCoordinator and CompletionCommandExecutor from its public surface.

## Settings

Use `MvpSettingsPolicy` before runtime behavior. The current bounds are engineering guardrails, not UX tuning evidence. `PreserveSource=false` is invalid in MVP.

## New gates

P5 verifies presentation/runtime wiring after G0/G1/G2. See `docs/testing/P5_PRESENTATION_RUNTIME_TEST_MATRIX.md`.

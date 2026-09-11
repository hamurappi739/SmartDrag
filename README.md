# SmartDrag

> **Status:** Preview UX phase complete; gated Windows proof phase remains. G0 is verified; production native activation is intentionally blocked until G1/G2/G3 evidence exists.
>
> **Foundation:** v0.9
>
> **Latest local verification:** .NET SDK 8.0.424 built all 19 projects; 230 xUnit tests passed and the Preview self-check passed 21/21. See `PROJECT_STATE.md` for the evidence environment and remaining gates.

SmartDrag turns an ordinary Windows file drag into a compact local action surface:

**drag -> action -> result**

Canonical product context: `docs/canonical/SMARTDRAG_MASTER_CONTEXT.md`  
Accumulated future coding-agent handoff: `docs/handoff/SMARTDRAG_FUTURE_CURSOR_HANDOFF.md`

Safe preview Release packaging is available through `./scripts/publish-preview.ps1`; it produces a hash-manifested
bundle and reruns the 21-check self-check without enabling native activation.

For a complete diagnostic capture flow, use `./scripts/capture-probe-evidence.ps1 -Mode g2` (or `-Mode p0`). It
launches the probe, analyzes the newest mode-specific log, and creates a privacy-checked evidence package. Add
`-Smoke` only for startup/shutdown validation; smoke output is never G1/G2 gate evidence.

## Product MVP firewall

- Windows only;
- one file from Explorer;
- image payload;
- Compress;
- Convert to WebP (shown only after the G3 encoder capability is accepted);
- Remove Metadata;
- new output, source preserved;
- lightweight completion UI;
- native Explorer drag must continue to work when SmartDrag is ignored.

Not current scope: PDF/archive/video implementation, FFmpeg, cloud upload, full file manager, general automation platform, destructive in-place processing.

## Current engineering gates

### G0 — compile/test

```powershell
./scripts/build.ps1
```

Everything must restore/build/test on real .NET 8 before manual Windows evidence is trusted.

### G1 — Explorer coexistence

```powershell
./scripts/run-probe.ps1 -Mode p0
```

The wrapper retains a sibling JSON summary and requires real drag/drop interaction for a gate-mode run. This mode is
diagnostic-only. It proves drag/overlay coexistence and must never become production qualification logic.

### G2 — strict pre-overlay qualification

```powershell
./scripts/run-probe.ps1 -Mode g2
```

The current Explorer-selection preflight is provisional until the full G2 matrix passes.

The probe also supports `--smoke` for an automated native initialization/shutdown check. Smoke output is useful for
runtime health, but contains no drag interaction and is not accepted as G1/G2 evidence. Use
`./scripts/run-probe.ps1 -Mode g2 -Smoke`; smoke intentionally skips the interaction requirement.

### P2 — production commit gate

v0.6 adds `SmartDrag.Orchestration`:

```text
Eligible preflight
  -> exact offered ActionIds captured in PreparedOverlaySession
  -> user Drops on action
  -> authoritative OLE CF_HDROP
  -> exact preflight identity match
  -> action still offered/available
  -> PreserveSource=true
  -> ActionRequest
  -> JobQueue
```

UI/Win32 code is not allowed to bypass this gate.

### G3 — image codec

No concrete image codec is active yet. Codec-neutral semantics, content inspection/resource guards, a deterministic image corpus, and the mandatory `GuardedImageProcessor` production wrapper exist. SkiaSharp/ImageSharp remain evaluation candidates only.

## Repository map

```text
src/
  SmartDrag.App/             future composition root; startup blocked by gates
  SmartDrag.Core/            domain/state/payload/output/overlay contracts
  SmartDrag.Actions/         registry and exception-containing executor
  SmartDrag.Orchestration/   preflight/overlay/authoritative Drop commit gate
  SmartDrag.Presentation/    toolkit-neutral operation/completion UX state + intents
  SmartDrag.Runtime/         sequential cancellable job queue
  SmartDrag.Imaging/         action handlers + codec-neutral G3 contracts/policies
  SmartDrag.Infrastructure/  safe physical output reservation/commit
  SmartDrag.Overlay/         production overlay boundary, renderer deferred
  SmartDrag.Windows/         Win32/OLE integration

tools/
  SmartDrag.Windows.Probe/   G1/G2 diagnostic executable
  generate_image_corpus.py   deterministic G3 fixture generator

tests/
  SmartDrag.*.Tests/
  fixtures/images/           G3 shared corpus + SHA-256 manifest

docs/
  canonical/
  architecture/
  decisions/
  handoff/
  imaging/
  orchestration/
  research/
  runtime/
  testing/
  windows/
```

## Automated validation

```bash
python scripts/validate_repo.py
python scripts/validate_image_corpus.py
```

These validators check repository structure/invariants and committed fixture bytes. The complete G0 command is
`powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1`; it restores/builds all 19 projects and
runs the xUnit suite. The build script resolves Python 3 through `SMARTDRAG_PYTHON`, `py -3`, `python`, or
`python3`.

## Visual and functional preview

The current production activation remains gated, but the WPF preview host is available for UI and local image
pipeline work. The preview surface uses an Apple-inspired visual system: light system-gray canvas, generous white
cards, large corner radii, restrained typography, blue system accent, and a compact status/history sidebar. The header
includes the SmartDrag vector mark, an RU/EN language switch, and a Light/Dark appearance switch; both preferences are
remembered between launches. The floating action panel uses the same logo, language, theme, and action vocabulary, so
the interface stays consistent while a file is being processed. Russian is the default language for a clear first
launch in this handoff.

```powershell
.\scripts\run-preview.ps1
```

The launcher configures the repository-local .NET cache and uses a single MSBuild worker for a stable Windows start.
Use `-NoBuild` when the current Debug binaries are already built. To run the same pipeline without opening a window:

```powershell
.\scripts\run-preview.ps1 -SelfCheck
```

Для headless-проверки того же preview pipeline без открытия окна:

```powershell
dotnet run --project .\src\SmartDrag.App -- --self-check
```

Команда проверяет WIC inspection, safety guard, queue/completion, создание JPEG/PNG output, сохранность исходника,
persisted preferences/history, queue cancellation, handle-bound artifact deletion, kill-switch/WebP safety и безопасный отказ на truncated JPEG (21 проверка). JSON-отчёт сохраняется в
`artifacts/preview-self-check/latest.json`.

Drop one existing JPEG or PNG into the window, or use `Choose image` in the header when testing without drag/drop.
Before showing the action panel, preview inspects the actual image
header, displays a thumbnail with format, dimensions, and source size; files outside the provisional safety limits are rejected
without starting an action. Selected actions go through the same queue, dispatch, completion, and presentation layers
as the production graph. `Compress` and `Remove Metadata` create a new file through the guarded output pipeline;
the WebP action remains hidden in the current preview until a concrete encoder is selected and benchmarked through G3.
After a successful action, the result card offers `Copy path` and `Open folder` actions. The preview never overwrites
the source file. While a queued or running operation is visible, `Cancel` requests cancellation through the
presentation layer; the UI then reports the terminal cancelled state without touching the source.
The window also keeps the six most recent jobs in compact status-accented cards with their terminal state and output
filename, and additional drops are queued sequentially while the current operation is running.
The sidebar history is remembered between launches: at most six terminal operations are stored, only sanitized file
names are written (never absolute source/output paths), and a missing or corrupt local history file fails closed.
The visible safety profile shows the active provisional limits and locked source-preservation policy. Keyboard
shortcuts are `Esc` (cancel/dismiss/clear), `Ctrl+V` (paste one image file from the clipboard), `Ctrl+O`
(open the image picker), `Ctrl+L` (toggle RU/EN), and `Ctrl+Shift+T` (toggle Light/Dark). The drop card highlights
when a file is dragged over it.
The `Run self-check` button launches the same headless verification from inside the window and writes a separate
`artifacts/preview-self-check/ui-latest.json` report.
Primary controls expose localized accessibility names for keyboard and screen-reader navigation, and `Choose image`
is the default action when the window has focus.
The safety card also includes `Reset local data`: after confirmation it restores Russian/light defaults and clears the
saved preview history; it never deletes image files.

## Current implementation-agent rule

The repository is currently being implemented by Codex. Any future agent must start at
`docs/handoff/CURSOR_ENTRY_PROTOCOL.md`, read the accepted ADRs/gates first, and implement only inside the active
approved scope.

## v0.8 safety layer (retained)

- one active consumable drag authorization capability;
- durable reservation journal and journal-only crash recovery;
- single-instance mutex before recovery/hooks;
- generated outputs carry optional strong platform identity;
- Windows Delete is handle-bound and identity-gated, never path-only;
- image codecs must be wrapped by `GuardedImageProcessor`;
- one `ProductionRuntimeGraph` owns deterministic wiring;
- `ProductionActivationPolicy.NativeActivationEnabled=false` remains the explicit native-start hold.


## v0.9 presentation / application boundary

- toolkit-neutral `SmartDrag.Presentation` added;
- operation UX is Queued/Running/Cancelling with truthful indeterminate progress;
- terminal completions are FIFO and user-safe;
- future views receive display names, not full source paths, for transient status text;
- Cancel and completion commands return through runtime authority rather than being performed by views;
- `IProductionDragInteraction` exposes safe MVP commit with no caller-provided `OutputPolicy`;
- `ProductionRuntimeGraph` hides JobQueue/ActionExecutor/CompletionCommandExecutor from its public application surface;
- `MvpSettingsPolicy` validates timing/placement values and hard-requires source preservation;
- ADR-038..041 and P5 presentation/runtime test matrix added.

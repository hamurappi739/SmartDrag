# SmartDrag Project State

## Foundation checkpoint

`v0.9 + G0 verified`

The foundation has been handed to Codex for implementation. Historical references to a future Cursor handoff remain
archival context only; the safety gates and accepted ADRs remain binding.

## Current phase truth

The remaining hard empirical gates are unresolved:

- `G1 — Explorer coexistence`: prove drag observation + non-activating overlay does not break native Explorer drag/drop;
- `G2 — production pre-overlay qualification`: prove Explorer preflight is reliable enough to authorize overlay presentation;
- `G3 — image codec`: select/verify a concrete codec and numeric resource limits against the committed corpus.

The implementation phase Preview UX / Vertical Slice is closed. Its acceptance checklist and verification record live
in docs/phases/PREVIEW_UX_PHASE_COMPLETE.md. This closes the safe preview scope; it does not waive the empirical
Windows and codec gates below.

The G1/G2 evidence-tooling half-phase is also closed: `scripts/run-probe.ps1` now owns repeatable probe launch and
latest-log selection, `scripts/analyze_probe_log.py` emits a JSON summary, and gate-mode analysis rejects startup-only
logs that contain no real drag/drop interaction. This is tooling progress only; no live Explorer evidence has been
accepted and G1/G2 remain unresolved.

G3 codec-neutral preparation is closed: `scripts/audit_image_corpus.py` records container facts and intentional
negative fixtures in a stable report, and `scripts/build.ps1` runs it as part of the build gate. The report keeps
`codecDecision=UNRESOLVED`; no concrete codec or production WebP claim has been introduced.

G1/G2 evidence retention is closed: `scripts/package_probe_evidence.py` produces a privacy-checked, hash-manifested
bundle containing only one raw log and its analyzer summary. This improves handoff integrity but does not change the
unresolved live Explorer gate status.

The local P4 artifact-safety verification phase is closed: preview self-check now exercises Windows identity capture,
same-object deletion, and replacement protection. The broader filesystem/reparse-point qualification matrix remains
the authoritative follow-up for any future completion capability.

The safe preview release phase is closed: `scripts/publish-preview.ps1` creates a Release `win-x64` bundle, runs the
published 21-check self-check, and writes a hash manifest while asserting native activation remains disabled and WebP
remains unavailable until G3.

The G1/G2 operator-capture phase is closed for automation only: `scripts/capture-probe-evidence.ps1` now runs either
P0 or strict G2, retains the analyzer summary, and packages a privacy-checked evidence unit with surface/DPI metadata.
Smoke mode is explicitly startup-only and cannot satisfy interaction evidence; non-smoke runs still require the manual
Explorer matrices.

The preview input-resilience phase is closed: clipboard reads, payload rejection, asynchronous inspection, overlay
presentation, action dispatch, and self-check failures now fail closed with localized user-facing messages and no stale
authoritative payload. Copy/open result statuses also follow the active language.

The preview capability-gating phase is closed: WebP is no longer advertised as an enabled action while
`WebpEncodingAvailable=false`. The concrete WIC adapter still rejects direct WebP calls defensively, and G3 remains the
only authority allowed to turn that capability on.

The capability-hint phase is closed: the Safety & Scope card now states the currently available actions and explains the
G3 hold in the active language/theme. The hint is derived from the same capability object that controls action
availability, preventing UI copy from drifting from runtime truth.

The responsive-layout phase is closed: the preview keeps its two-column Apple composition on wide windows and switches
to a stacked drop-surface/history/safety flow below the compact-width threshold. Resize handling is idempotent and does
not change the underlying action or safety pipeline.

The evidence-verifier phase is closed for retained packages: `scripts/verify_probe_evidence.py` independently checks the
three-file structure, hashes, JSONL integrity, privacy boundary, and analyzer/manifest agreement. Verification never
promotes smoke output or changes G1/G2 state.

The output-reservation integrity phase is closed: `PhysicalOutputManager` now validates reservation shape before both
commit and cleanup, rejects source-targeting or mixed-directory contracts, and requires the reservation-bound partial
filename marker. Foreign temporary paths are left untouched. The broader Windows reparse-point/sharing matrix remains
pending P4 evidence.

The safe-preview verification phase is closed: `scripts/verify_preview_publish.py` independently validates package
manifest safety flags, the exact file set, byte counts, SHA-256 hashes, and the embedded 21/21 self-check. The publish
script runs this verifier after manifest creation and waits briefly for the published self-check report to materialize.
This verifies release integrity only; it does not change G1/G2/G3 or production-activation state.

The G1/G2 evidence-packaging verification phase is closed: `package_probe_evidence.py` now invokes the independent
evidence verifier before reporting package success, and normalizes the destination path before writing. A package that
fails structure, privacy, hash, or summary/manifest checks cannot be reported as created. Manual Explorer evidence and
all G1/G2 gate decisions remain unchanged.

The recovery-journal integrity phase is closed: `JsonOutputRecoveryJournal` now rejects ambiguous or malformed
documents (duplicate reservation ids, missing timestamps, invalid/overlong paths, or missing entries) before startup
recovery can consume them. Existing pending-copy promotion and journal-size limits remain intact; the Windows
reparse-point/sharing-violation P4 matrix is still pending.

The deterministic P4 target-guard phase is closed: the Windows identity service reads file attributes from the same
opened handle and rejects directories/reparse points during capture or deletion verification. A changed path is
reported as an identity mismatch rather than reaching `FILE_DISPOSITION_INFO`. Real symlink/junction and filesystem
qualification evidence remains pending.

The deterministic G3 image-guard input-boundary phase is closed: `GuardedImageProcessor` now rejects missing or
source-equal temporary paths before inspection and codec work. Existing content/resource gates remain authoritative;
no concrete codec or WebP capability was enabled.

The action-registry contract phase is closed: `ActionRegistry` now rejects incomplete action metadata at composition
time and keeps capability filtering centralized, including the pre-G3 WebP hold. Overlay and orchestration continue to
use the same authoritative availability path.

The orchestration-session integrity phase is closed: `PreparedOverlaySession` now binds the overlay to its drag session,
rejects duplicate or mismatched action offers, requires every presented action to be enabled, and freezes the action list
before it crosses the UI boundary. `TryCommitDrop` still rechecks authoritative capability availability immediately
before creating an `ActionRequest`.

The preview-persistence hardening phase is closed: preference and history loads reject oversized files before parsing,
writes are UTF-8, size-bounded, flushed, and atomically promoted from unique sibling files, and failed preference writes
clean up their temporary artifact. UI persistence remains isolated from runtime authorization and output policy.

The presentation-command snapshot phase is closed: terminal completion command lists are now read-only at runtime and
presentation boundaries, while command capability revalidation and the coordinator's in-flight guard remain authoritative.
UI observers can render or inspect completion actions without mutating the command surface used by execution.

The runtime job-snapshot integrity phase is closed: queue admission now copies caller-owned input lists, terminal state
copies executor-owned output and artifact lists, and all published job collections are read-only. Request-id idempotency,
sequential execution, cancellation, and completion authority remain unchanged.

The completion-command failure-boundary phase is closed: unexpected platform/deletion adapter exceptions are converted to
safe terminal failures, explicit caller cancellation still propagates, and failed deletion attempts remain retryable.
Identity-backed deletion and command-offer revalidation remain authoritative.

The drag-session lifecycle phase is closed: active-session reads are race-safe, stale native-end signals cannot clear a
newer session, and overlay presentation failures clear authorization state before error cleanup. Single-session
supersession and consume-before-await commit semantics remain in force.

The preview output-deletion history phase is closed: successful identity-checked deletion now persists a localized
`output deleted` marker, live history refresh preserves it, and the existing runtime at-most-once delete authority
continues to reject repeated destructive execution.

The preview history-clear phase is closed: the history card can hide recent operations without touching files, persists
the clear boundary across restart, accepts the previous JSON-array history format, and keeps the action localized and
keyboard/automation accessible.

The preview result-resilience phase is closed: missing generated outputs now produce a localized unavailable state
without stale copy/open/delete actions, and ordinary history writes preserve the persisted clear boundary.

The preview keyboard-accessibility phase is closed: controls have a deterministic tab order, custom buttons expose
visible focus rings, and Delete/history shortcuts route through the same confirmation and runtime authorities.

The preview input-state resilience phase is closed: new files clear stale selection/result state before inspection,
failed completion dismissal blocks competing input, and transition sequencing is covered by toolkit-neutral policy tests.

The preview queue-UX phase is closed: active status now shows safe source name, localized queued/running/cancelling
state, and the number of jobs waiting behind the visible operation without changing runtime authority.

The preview diagnostics-UX phase is closed: self-check now keeps the current report location, shows pass/fail state,
and offers safe localized report-folder access without exposing source/output paths.

Deterministic foundation layers now include:

- `P1` sequential execution + output safety;
- `P2` immutable drag-to-job authorization/commit + idempotent dispatch;
- `P3` active-session lifetime, durable crash recovery, completion execution;
- `P4` strong generated-artifact identity, handle-bound deletion, guarded codec composition;
- `P5` toolkit-neutral presentation/runtime wiring, safe user-intent routing, and central MVP settings policy.

## Build state

`G0 PASS — 2026-09-03`

Executed on Windows `10.0.19045` with .NET SDK `8.0.424` / MSBuild `17.11.48`:

```powershell
./scripts/build.ps1
```

Results:

- static repository validator: PASS;
- image corpus validator: PASS (9 fixtures);
- codec-neutral image corpus audit: PASS (9 fixtures; 6 positive containers, 3 intentional negatives);
- NuGet restore: PASS;
- Debug build: PASS (all 19 projects);
- xUnit: PASS (230 passed, 0 failed, 0 skipped);
- headless preview self-check: PASS (21 checks; JPEG/PNG WIC, guards, queue/completion/cancellation, artifact identity, output safety, preferences/history, native kill-switch, controlled WebP rejection);
- build warnings/errors: 1 / 0; the only warning is external NuGet advisory-feed lookup `NU1900` (the source feed was unreachable), with no compiler/analyzer warnings.

The Preview window lifecycle and placement phase is closed: geometry is normalized and persisted safely, off-screen
placements are recovered against the current work area, compact windows are supported down to 720×480, and delayed
fire-and-forget work is observed through `SafeTaskRunner`. Native activation remains disabled and the empirical G1/G2/G3
gates remain unchanged.

The Preview theme-token phase is closed: Light and Dark now share a tested toolkit-neutral palette applied by the WPF
adapter for runtime appearance changes. Theme switching remains UI-only and does not alter action capability or output
authority.

The Preview overlay hit-test phase is closed: action selection resolves explicit enabled button geometry, decorative
visuals cannot masquerade as actions, and invalid geometry fails closed. This is deterministic hardening only; real
Explorer/OLE coexistence and production overlay evidence remain behind G1/G2.

The Preview file-picker phase is closed: the empty-state and `Ctrl+O` flows use an owner-bound WPF adapter behind an
interface, reject selection while the input/operation pipeline is busy, treat cancel as a no-op, and route selected paths
through the existing guarded asynchronous input path. Picker construction errors are localized and logged without leaking
technical details or changing any capability gate.

The Preview input-availability phase is closed: the header picker and large `+` control now visibly disable whenever the
picker, inspection, drag session, or operation is busy; both use the same tested idle policy and localized help text.
Window shutdown cancels in-flight inspection/presentation work so late callbacks cannot target a closing dispatcher.

The Preview self-check gating phase is closed: diagnostics cannot replace an active picker, inspection, drag, operation,
completion, or another diagnostic run; a busy attempt receives localized guidance and the existing report pipeline remains
bounded and read-only.

The Preview overlay theme/accessibility phase is closed: action labels and UI Automation metadata follow the active
language, while the transient panel uses the correct light/dark accent and keeps explicit hit-test geometry.

The Preview result-state reconciliation phase is closed: operation and quiet snapshots clear stale completion controls,
output references, and result text; an idle window returns to the explicit ready-for-another-file state.

The Preview button-feedback phase is closed: custom Preview and overlay buttons share subtle hover, pressed, and disabled
visual states while retaining Apple-style focus rings and the existing command routing.

The Preview drop-availability phase is closed: drag-over effects and highlighting are advertised only for a real payload
while the surface is idle; busy, picker, active-drag, and operation states now fail closed before release.

The Preview typography phase is closed: Preview and overlay use the shared `Segoe UI Variable Text`/`Segoe UI` fallback
stack with display text formatting and ClearType rendering, while existing layout rounding remains active.

The Preview launcher phase is closed: `scripts/run-preview.ps1` is the canonical one-command WPF/self-check entry point,
uses repository-local dotnet caches, defaults to a single MSBuild worker, and propagates startup failures clearly.

The Preview async-callback boundary phase is closed: keyboard and drop `async void` handlers now contain unexpected
exceptions, show localized safe status, and write bounded diagnostics instead of allowing failures to escape into WPF.

The Preview accessibility live-status phase is closed: status, result, and processing progress expose localized UI
Automation names and polite live regions for screen-reader updates without leaking technical paths.

The Preview dispatch-handoff phase is closed: after an action commit, picker/drop input remains visibly busy until the
runtime operation snapshot records its JobId, eliminating the small asynchronous gap where a second input could slip in.

The Preview input-availability phase is closed: picker controls expose busy state through disabled visuals and automation
help text, while a lifetime cancellation boundary silences shutdown races and prevents late input callbacks.

G0 fixes made while preserving accepted architecture:

- `scripts/build.ps1` now fails when an external Python/.NET command returns a non-zero exit code instead of silently continuing;
- `scripts/build.ps1` resolves a working Python 3 in deterministic order (`SMARTDRAG_PYTHON`, `py -3`, `python`, `python3`) and reports a clear prerequisite error;
- the validator has an explicit build-only artifact allowance, so a previous restore/build does not make a later G0 run impossible; standalone validation stays strict;
- repository-local `NuGet.Config` gives restore a deterministic source rather than depending on a user profile configuration;
- Windows `LibraryImport` containers are partial and the Windows project explicitly enables required generated unsafe interop;
- a .NET 8-compatible recovery filename check replaced an unsupported `char` overload;
- Windows probe action IDs now use central `BuiltInActionIds`.
- `.gitignore` now restricts `/artifacts/` to a repository-root build-output directory, so it no longer hides
  source, tests, and documentation under `SmartDrag.*.Artifacts` from version control.
- G2 preflight extension classification now derives the suffix from the resolved full path rather than trusting
  display metadata; this preserves the hidden-extension contract and has a regression test.
- WPF overlay and Windows WIC JPEG/PNG adapters now exist for the safe `--preview` host. Preview actions now travel
  through the queue, dispatcher, completion, and presentation layers; the preview supports cancellation and
  successful results expose `Copy path` and `Open folder` actions. The window keeps a six-item recent-job history in
  compact state-accented cards and
  permits sequential queued drops, and displays a WIC-loaded thumbnail for the accepted input. The preview hides the
  WebP action while `WebpEncodingAvailable=false`, so the UI cannot advertise an unavailable encoder; native
  production activation remains disabled, and WebP/G3 codec acceptance is still open. The preview now exposes its
  provisional safety profile and supports `Esc`/`Ctrl+V` keyboard flows.
  A `Run self-check` button exposes the 21-check headless verification directly in the WPF window. The preview and
  transient action overlay now share an Apple-inspired light UI system with white rounded cards, blue accent actions,
  subtle shadows, and readable status hierarchy. The preview also includes a `Choose image` file-picker path for
  deterministic UI testing without Explorer drag/drop; picker selections are treated as authoritative local evidence.
  Drag-over feedback now highlights the drop card; `Ctrl+O` opens the same picker, `Ctrl+L` toggles RU/EN, and
  `Ctrl+Shift+T` toggles Light/Dark for keyboard-driven testing.
  The header now carries a vector SmartDrag logo, an RU/EN language switch, and a Light/Dark theme switch; both are
  persisted in a fail-closed UI preferences file, with Russian as the default preview language for this handoff.
  The transient action panel now receives the same language/theme state and carries a compact version of the brand mark,
  keeping the visual and vocabulary system consistent across both UI surfaces.
  The six-item sidebar history now persists terminal operations in a bounded, atomic, fail-closed local JSON store;
  only sanitized display names are retained, so absolute source/output paths never leave the process.
  The safety card now exposes an explicit, confirmed `Reset local data` command that restores Russian/light defaults and
  hides/clears saved preview history without touching image files.
  The Preview self-check result now derives its aggregate pass/fail state, check counts, and duration from a bounded
  JSON report, with a safe fallback for malformed or unavailable reports.
  The safe Preview process boundary now records bounded lifecycle/error events, rotates oversized logs, and converts
  an exception escaping the WPF run loop into a controlled exit instead of an unobserved failure.
  Result-action visibility is now centralized and fail-closed: stale or runtime-ungranted Copy/Open/Delete controls
  cannot survive a missing, deleted, or ambiguous output snapshot.
  Completion-command failures are localized at the Preview boundary; adapter exception text, HRESULTs, and private
  paths are no longer appended to the normal result surface.
  Inspection, overlay-preparation, and dispatch failures now use stable user-safe copy instead of echoing lower-layer
  reasons or technical details into the Preview result.
  Follow-up result-command status failures use the same command-specific safe copy, so secondary status chips cannot
  bypass the privacy boundary.
  History rendering now maps unknown persisted values to neutral localized labels and re-sanitizes source/output
  names at the UI boundary, preventing legacy or tampered JSON from leaking arbitrary text.

## v0.9 application-facing architecture

Production composition no longer exposes raw runtime authorities to future UI code.

Public graph surface:

```text
IProductionDragInteraction
MvpPresentationCoordinator
IFilePayloadSnapshotFactory
AppCapabilities
```

Not publicly exposed from the graph:

```text
JobQueue
ActionExecutor
DragWorkflowOrchestrator
CompletionCoordinator
CompletionCommandExecutor
```

`IProductionDragInteraction.TryCommitMvpAsync` intentionally has no `OutputPolicy` parameter. The single MVP policy is created internally through `MvpOutputPolicyFactory` and always preserves source.

## v0.9 presentation truth

`SmartDrag.Presentation` is toolkit-neutral and does not choose WPF/WinUI.

After action commit:

```text
native action overlay disappears
-> operation status: Waiting / Working / Cancelling
-> truthful indeterminate progress
-> Cancel only while Queued/Running
-> terminal completion FIFO
```

Numeric processing percentages are forbidden until a real codec progress contract exists. Completion size savings may show a percentage only when based on actual source/output byte metrics.

Presentation uses safe file display names, not full paths. Completion failures show `AppError.UserMessage`, not technical messages.

## MVP settings policy

Current guardrails:

- ActivationDelayMs: 80–1200;
- MinimumTravelDip: 4–96;
- CursorOffsetDip: 12–160;
- PreserveSource: required `true`.

These are engineering bounds, not final UX tuning evidence. Defaults remain 180 ms / 12 DIP / 28 DIP until real Windows testing changes them.

## Existing safety foundation retained

- OLE outcome is COPY-or-NONE, never MOVE;
- source is not modified by default;
- output uses journaled partial -> safe commit;
- recovery deletes only exact journal-authorized partials;
- startup is single-instance -> recovery -> runtime -> native hooks;
- generated-file Delete requires strong Windows identity and same-handle disposition;
- concrete codecs must sit behind `GuardedImageProcessor`;
- one physical drag maps to one idempotent logical request;
- `ProductionActivationPolicy.NativeActivationEnabled=false` remains a hard kill-switch.

## Required gates

### G0

```powershell
./scripts/build.ps1
```

Required: all 19 projects restore/build and all xUnit tests pass.

### G1/G2

Run the P0 and G2 Windows probe matrices and retain/analyze JSONL evidence.

### G3

Benchmark/select codec using the committed image corpus and matrix. Do not activate a codec by convenience.

### P4

Run strong artifact identity/replacement/reparse-point deletion tests on real Windows.

### P5

The safe preview implementation phase is complete. The formal production P5 gate remains pending until G1/G2 provide
the required live Windows evidence.

The safe preview/runtime verification phase is closed with a 21/21 self-check, including running and queued
cancellation. This is not production P5 acceptance; the eventual renderer still requires the manual matrix after G1/G2.

The Preview lifecycle-cancellation phase is closed: picker, action commit, completion dismissal, self-check, and
report-folder callbacks now respect the shared window lifetime boundary. Closing the window cancels pending work and
suppresses late UI writes; expected shutdown cancellation is not shown as a false user error. This hardens the safe
preview only and does not promote any empirical gate.

The Preview overlay-lifecycle phase is closed: the WPF overlay now detaches its real initialization handler and treats
dispatcher shutdown as an expected disposal boundary. Late close scheduling cannot become an unobserved process error;
non-activating overlay behavior and action authority remain unchanged.

The documentation truth-sync phase is closed: operator-facing README and Russian manual-checklist counts now match the
current 19-project/230-test baseline. Historical phase records retain their original evidence dates and counts.

Run `docs/testing/P5_PRESENTATION_RUNTIME_TEST_MATRIX.md` after G0/G1/G2, including status/cancel/FIFO/privacy checks on the eventual renderer.

## Explicit holds

1. Do not enable native production activation before accepted G0/G1/G2/G3 evidence.
2. Do not let UI/native adapters construct `ActionRequest` or arbitrary `OutputPolicy`.
3. Do not expose raw runtime authority simply to make UI implementation easier.
4. Do not fabricate numeric progress.
5. Do not display technical error text or full paths in normal transient presentation.
6. Do not replace identity-safe Delete with path-based deletion.
7. Do not replace journal recovery with wildcard scanning.
8. Do not wire codec implementations around `GuardedImageProcessor`.
9. Do not expand MVP to PDF/archive/video/FFmpeg/batch while current gates remain open.

## Next foundation checkpoint

The next checkpoint is **G1/P0 Explorer coexistence evidence**. Only after G1/G2 should the project select a production renderer; only after that should G3 select a production image codec and numeric resource limits.

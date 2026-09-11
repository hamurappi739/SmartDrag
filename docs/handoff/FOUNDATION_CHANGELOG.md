# SmartDrag Foundation Engineering Changelog

This changelog tracks architecture/foundation checkpoints before any coding agent is allowed to take over implementation. It is not a marketing changelog.

## v0.1 — Canon and architecture foundation

- imported the SmartDrag master context as canonical product source;
- fixed Windows-first, local-only, non-destructive MVP scope;
- defined `DragSession`, lifecycle state machine, Action Registry boundaries, overlay/output/job concepts;
- established fail-open behavior toward native Windows drag and fail-safe behavior toward user files;
- created the first future Cursor handoff document.

## v0.2 — Repository and Windows P0 scaffold

- created the real .NET solution/project structure;
- isolated Core from Win32/UI implementation details;
- added WinEvent drag lifecycle observation and raw Win32 overlay proof structure;
- added OLE initialization, `IDropTarget`, `CF_HDROP`, COPY-only effect policy;
- created P0 matrix and Windows API evidence docs.

## v0.3 — Handoff/validation hardening

- expanded ADR discipline and project-state/build-gate documents;
- corrected project dependency/TFM boundaries and callback responsibilities;
- added repository manifest/static integrity checks;
- made the package explicitly `UNVERIFIED BUILD` when generated without a .NET SDK.

## v0.4 — Runtime, output safety, and image orchestration

- added sequential cancellable `JobQueue` and terminal immutable job snapshots;
- added safe output reservation, SmartDrag-owned partial artifacts, collision recheck, non-overwriting commit, cancellation cleanup;
- added real MVP image action handlers behind codec-neutral `IImageProcessor`;
- centralized stable ActionIds;
- kept all concrete image codec dependencies behind Gate G3;
- added runtime/output/image unit-test sources and future Cursor entry protocol.

## v0.5 — Production qualification, completion data, and native-boundary safety

- separated P0 signal-only proof mode from strict G2 production-qualification experiment;
- added `Unknown / Eligible / Supported / Rejected` payload authority model;
- added conservative pre-G3 single PNG/JPEG qualification;
- added provisional Explorer `ShellFolderView.SelectedItems` preflight and authoritative OLE `CF_HDROP` revalidation;
- added preflight-vs-Drop path identity enforcement;
- moved Shell data extraction out of `DragEnter` and into `Drop`;
- added pure overlay placement logic and hidden target-monitor pre-position before first DPI query;
- added action metrics, generated-output ownership, and terminal completion projection;
- made delete-result eligibility depend on explicit SmartDrag output ownership;
- hardened WinEvent, WndProc, and OLE callbacks as no-throw native boundaries (ADR-021);
- removed exception-message/raw-source-path diagnostics from native boundaries;
- expanded probe analyzer to flag focus theft, MOVE effects, native-boundary faults, dropped evidence, G2 eligibility violations, and preflight mismatch;
- added G1/G2 evidence runbook and native-boundary safety specification.

## v0.6 — Production commit gate and codec-neutral G3 specification

- added portable `SmartDrag.Orchestration` and tests;
- made a shown overlay an immutable authorization snapshot binding drag id, preflight identity, and exact offered ActionIds;
- added final authoritative Drop commit gate before `ActionRequest` creation;
- added fail-closed checks for preflight mismatch, unoffered/stale actions, capability change, and destructive output policy;
- separated accepted commit dispatch from decision logic;
- hardened prepared-session/accepted-decision objects as non-forgeable orchestration capabilities;
- made one drag session equal one idempotency RequestId, with duplicate suppression in dispatcher and JobQueue;
- added codec-neutral image inspection/resource-limit contracts and pure execution guard;
- fixed canonical image semantics for orientation, ICC, metadata, animation, and format-preserving compression intent;
- created deterministic JPEG/PNG/metadata/ICC/alpha/malformed image corpus with SHA-256 manifest;
- added G3 correctness/performance/packaging/licensing matrix and refreshed current codec research;
- kept concrete codec packages prohibited until the G3 spike is explicitly opened.

## Current truth after v0.6

The repository now contains architecture, runtime, interop, orchestration, and codec-neutral image-policy foundations, but empirical gates remain intentionally unresolved:

1. G0/G1/G2 must be built and exercised on real Windows/.NET 8 hardware before the current Explorer/overlay mechanism is accepted as production-ready.
2. P2 orchestration must be wired only through the accepted commit gate once G2 is proven.
3. G3 must benchmark/select a codec and concrete resource limits before a production `IImageProcessor` is added.

A future coding agent must treat these as gates, not missing TODOs to bypass.

## v0.7 — Session coordination, completion execution, and crash recovery

- added `DragInteractionCoordinator` as the single owner of the active prepared-overlay capability;
- made authorization consumable before async hide/dispatch so duplicate native/UI delivery cannot reuse a drag session;
- added durable output-recovery contracts and JSON journal;
- changed temporary output naming so the exact `ReservationId` is embedded in the partial filename;
- made recovery journaling a prerequisite for issuing an output reservation;
- added journal-only startup cleanup that never scans user directories;
- added named-mutex single-instance lease and `StartupSafetyBootstrap` ordering recovery before hooks/runtime; mutex ownership is held/released on a dedicated lifetime thread to respect Windows thread-affinity;
- added runtime completion capabilities, at-most-once `CompletionCoordinator`, and `CompletionCommandExecutor`;
- added Windows Open Folder + Unicode clipboard completion adapter without choosing a UI framework; clipboard capability requires an explicit owner HWND and prepares data before clearing the clipboard;
- explicitly disabled Start Result Drag until a drag-source proof and Delete Generated Output until strong file identity exists;
- added ADR-029..ADR-033, recovery/completion specs, and P3 test matrix.

## Current truth after v0.7

Deterministic application/runtime safety now covers authorization lifetime, idempotent dispatch, output reservation/commit, crash cleanup authority, and completion-command gating. Empirical Windows G0/G1/G2 still remain hard gates, and the concrete G3 image codec remains intentionally unselected.

## v0.8 — Strong generated-artifact identity, guarded codec wiring, and composition root

- replaced bare generated-output ownership paths with `GeneratedArtifact` + optional strong `ArtifactIdentity`;
- added Windows `FILE_ID_INFO` identity service using volume serial + 128-bit file id;
- hardened final output authority by comparing temporary identity before `File.Move` with final identity after move;
- implemented identity-safe generated-output deletion by verifying the current object and applying `FileDispositionInfo` on the same open handle;
- opened reparse points themselves during delete verification so path replacement cannot redirect deletion to a target;
- enabled Delete completion only when exactly one generated artifact has strong identity and an identity-safe deletion service is composed;
- added `GuardedImageProcessor` so G3 content/resource policy cannot be bypassed by raw codec wiring;
- added the single intended `ProductionRuntimeGraph` composition root;
- kept native hook activation behind an explicit `ProductionActivationPolicy.NativeActivationEnabled=false` kill-switch until G0/G1/G2/G3 acceptance;
- added ADR-034..ADR-037 and the P4 artifact-identity/composition test matrix;
- made generated-output Delete consumable per JobId so duplicate UI/message delivery cannot perform repeated destructive attempts.

## Current truth after v0.8

The deterministic runtime now has a defined composition path all the way from clean startup lease through safe outputs, guarded image processing, job/orchestration, and completion identity handling. Empirical Windows drag gates and the concrete image codec are still intentionally unresolved; production native activation remains blocked.


## v0.9 — Presentation and safe application-facing wiring

- added toolkit-neutral `SmartDrag.Presentation` + tests;
- separated native drag overlay lifetime from post-commit status/completion surfaces;
- added indeterminate operation projection and completion FIFO;
- routed Cancel/completion intents through the existing runtime authorities;
- added central MVP settings validation and a single safe output-policy factory;
- narrowed `ProductionRuntimeGraph` so application/UI code does not receive JobQueue/ActionExecutor/CompletionCommandExecutor;
- production commit surface no longer accepts arbitrary `OutputPolicy`;
- added ADR-038..041 and P5 presentation/runtime test matrix.


## Current truth after v0.9

The deterministic foundation now includes a narrow application-facing authority surface and a toolkit-neutral presentation layer. UI implementation can be deferred without leaving future views responsible for job execution, output policy, cancellation safety, or completion-command authority. G0/G1/G2/G3 and Windows P4/P5 evidence remain unresolved hard gates; native activation stays blocked.

## v0.9.x — Preview release hardening

- added a safe Release preview publisher with a published self-check and hash manifest;
- added retained probe-evidence capture and independent integrity/privacy verification tooling;
- gated unavailable WebP capability from both the preview action registry and user-facing copy;
- added fail-closed clipboard/drop handling, responsive preview layout, and localized capability hints;
- hardened `PhysicalOutputManager` so commit and cleanup reject forged or source-targeting reservations before any move/delete;
- added regression coverage for foreign temporary paths and source-preserving output contracts.
- added an independent safe-preview package verifier for manifest, hash, file-set, and self-check integrity;
- wired publishing to verify its own generated bundle and tolerate delayed self-check report materialization.
- made probe-evidence packaging invoke its independent verifier before reporting success;
- normalized evidence output paths before writing to make operator artifacts deterministic.
- hardened durable recovery-journal parsing against duplicate ids, missing timestamps, and overlong paths;
- added fail-closed regression coverage for ambiguous recovery documents.
- added same-handle `FILE_BASIC_INFO` target classification for Windows artifact identity;
- rejected directory/reparse targets before identity-backed deletion and added replacement regressions.
- hardened the mandatory image-processor decorator against missing or source-equal processing paths;
- added regressions proving invalid requests cannot reach inspection or the inner codec.
- added fail-fast validation for action display metadata and supported-input declarations;
- kept capability filtering centralized so unavailable WebP cannot drift back into the overlay.
- hardened `PreparedOverlaySession` against duplicate, mismatched, disabled, or mutable overlay action state;
- added regression coverage proving drag-session binding and read-only action snapshots at the UI boundary.
- bounded preview preference/history files before parsing and flushed UTF-8 payloads before atomic replacement;
- added oversized-persistence regressions and symmetric temporary-file cleanup for preference writes.
- made terminal and presentation completion command lists read-only snapshots;
- added regressions proving UI projection cannot mutate the command surface used by completion execution.
- made the sequential queue own request inputs and copy terminal outputs/artifacts before publication;
- added regression coverage for caller mutation and read-only job snapshot collections.
- contained unexpected completion platform/deletion adapter exceptions behind safe terminal results;
- preserved caller cancellation semantics and made failed deletion attempts retryable with regression coverage.
- made active drag-session reads race-safe and protected newer sessions from stale native-end signals;
- added overlay presentation-failure cleanup regression coverage to prove authorization is cleared before error hide.
- persisted successful preview output deletion in the privacy-safe history model;
- added a localized `output deleted` history marker that survives live refresh and restart;
- added regression coverage for the deletion-state round trip while retaining runtime at-most-once deletion authority.
- added a persisted history clear boundary with backward-compatible loading of the previous JSON-array format;
- added a localized, keyboard-accessible history clear action that never deletes image files;
- added regression coverage for clear-boundary persistence and legacy history migration.

## Current truth after v0.9.x

- added fail-closed result rendering when a generated output disappears outside the preview;
- hid stale copy/open/delete actions and marked live unavailable results in history;
- preserved the history clear boundary across legacy save-overload calls with regression coverage.
- added deterministic Preview tab order and visible keyboard focus rings for custom Apple-style buttons;
- added guarded Delete and Ctrl+Shift+H shortcuts with localized footer guidance;
- kept all destructive shortcuts behind confirmation and existing runtime command revalidation.
- added a toolkit-neutral Preview input transition policy for busy, drag, completion, invalid-payload, and inspection states;
- cleared stale thumbnail, filename, metadata, payload, and result state before admitting a new file;
- added regression coverage for dismissal gating and single-file input sequencing.
- exposed localized queued-behind count and safe source display name in the active Preview status;
- kept cancel visibility and operation state tied to the existing presentation/runtime projection;
- added queue UX regression coverage without changing sequential execution authority.
- added a localized self-check report-folder action with pass/fail availability handling;
- kept report location scoped to the current window and cleared it on local-data reset;
- contained shell-opening failures without exposing technical paths or broadening runtime authority.
- made Preview self-check copy dynamic by reading aggregate counts and duration from the bounded report JSON;
- added localized pass/fail summaries that distinguish a partial check failure from a clean run;
- kept malformed, inaccessible, and oversized reports on a safe fallback path without technical detail leakage.
- added bounded Preview lifecycle/error logging with one rotated backup and capped exception details;
- contained exceptions escaping the WPF run loop and converted them to a controlled non-zero process exit;
- kept all process-log writes best effort so diagnostics cannot become a second failure.
- centralized Preview result-action visibility around runtime-granted commands and actual output availability;
- hid stale output controls for deleted, missing, or ambiguous results while retaining safe Dismiss behavior;
- added regression coverage for command filtering and output-state transitions.
- localized completion-command failures at the Preview boundary instead of displaying adapter-provided details;
- added privacy regressions covering exception names, HRESULTs, and private paths in command errors.
- replaced direct Preview assignments of guard, preparation, and dispatch reasons with stable localized user copy;
- added regressions proving lower-layer status strings and private paths cannot reach the normal result surface.
- routed transient Copy/Open/Delete/Dismiss status failures through the same localized privacy boundary;
- closed the secondary status path that could otherwise echo adapter-provided text after a result command.
- added a toolkit-neutral history display policy with known state/action vocabulary and bounded file-name rendering;
- protected the sidebar against legacy or tampered history values that contain arbitrary technical strings or paths.
- aligned history tooltips with the sanitized row renderer so private paths cannot reappear on hover.
- added bounded, persisted Preview window geometry with off-screen recovery and a 720×480 compact-screen floor;
- enabled layout rounding/device-pixel snapping and routed delayed fire-and-forget cleanup/render work through
  `SafeTaskRunner` so lifecycle failures remain observed and diagnosable;
- added localized UI Automation help text for the empty-state image picker button.
- centralized Preview Light/Dark colors in a shared toolkit-neutral palette;
- routed runtime WPF theme application, progress, drop-zone, history, warning, error, and cancel surfaces through the
  active palette and added token-parity/contrast regression tests.
- replaced overlay action lookup through arbitrary deepest visual hit-tests with explicit enabled button geometry;
- isolated decorative overlay visuals from hit-testing and added deterministic Core policy regression coverage.
- isolated Preview file picking behind an owner-bound WPF adapter and a toolkit-neutral idle policy;
- routed picker selections through the existing guarded asynchronous input pipeline and converted dialog failures to
  localized privacy-safe status with regression coverage for busy/drag/operation rejection.
- made the header picker and empty-state `+` control visibly follow the shared idle policy with localized automation help;
- added a Preview lifetime cancellation boundary so closing the window cancels inspection/presentation work and suppresses
  late dispatcher callbacks without changing runtime or capability authority.
- gated Preview self-check behind the same explicit idle projection so diagnostics cannot clobber active operation status;
- added regression coverage for picker/drag/operation/completion/running-state rejection and preserved bounded reports.
- synchronized overlay action UI Automation labels with localized visible text;
- aligned transient brand/icon accents with the active Light/Dark Apple palette while retaining non-activating hit-test safety.
- reconciled Preview operation/quiet snapshots so stale completion actions and output references cannot remain visible;
- restored an explicit localized ready state after the presentation surface becomes quiet.
- added shared hover/pressed/disabled feedback triggers to Preview and overlay custom button templates;
- kept focus rings, theme colors, hit-test geometry, and command authority unchanged.
- aligned drag-over effects/highlighting with the shared idle policy so busy Preview states no longer promise acceptance;
- added toolkit-neutral drop-availability regression coverage without changing payload or native authority.
- aligned Preview and overlay to `Segoe UI Variable Text` with `Segoe UI` fallback and display/ClearType text rendering;
- preserved pixel-snapped layout and all existing runtime/capability boundaries.
- added `scripts/run-preview.ps1` as the canonical stable WPF/self-check launcher with local caches and clear exit codes;
- documented `-NoBuild` and `-SelfCheck` paths without changing production activation gates.
- contained unexpected keyboard/drop async callback failures behind localized status and bounded process diagnostics;
- preserved specialized clipboard errors, cancellation semantics, and runtime authority.
- exposed status, result, and indeterminate progress as localized polite UI Automation live regions;
- kept screen-reader text privacy-safe and left keyboard/command routing unchanged.
- kept Preview input busy through action-commit handoff until the first operation snapshot records the JobId;
- recalculated picker/drop availability only after authoritative operation state is visible.
- made Preview picker, dispatch, completion, and self-check callbacks shutdown-aware through one lifetime token;
- recorded self-check/report-folder failures in bounded diagnostics while keeping expected close cancellation quiet.
- made WPF overlay event teardown deterministic and guarded cross-thread disposal when the dispatcher is already closing.
- synchronized operator-facing build/test counts with the current 19-project, 230-test verification baseline while
  preserving historical phase evidence.

The preview is now a truthful, testable local surface with reproducible packaging and stronger output-contract
defenses. Native Explorer coexistence (G1/G2), concrete codec selection (G3), and the broader Windows P4/P5 evidence
matrices remain intentionally pending; no gated capability was enabled by preview work.

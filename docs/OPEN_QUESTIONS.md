# Open Questions

These are deliberately unresolved. Coding agents must not silently invent answers.

## Windows / overlay

- **OQ-001:** Does current Windows Explorer reliably emit drag start/cancel/complete WinEvents from folder views and desktop?
- **OQ-002:** What accessible object/name/role is exposed for one-file and multi-selection drags?
- **OQ-003:** Can documented Shell/UI Automation APIs resolve exact selected paths early enough for pre-overlay action filtering?
- **OQ-004:** If WinEvent drag-start is incomplete, what is the least invasive fallback signal?
- **OQ-005:** How should keyboard accessibility coexist with a non-activating drag overlay?
- **OQ-006:** Does production overlay need a custom region so OLE hit-testing matches rounded/transparent visual corners?
- **OQ-007:** Which renderer preserves the proven interop behavior with lowest maintenance cost?
- **OQ-016:** Does `ShellFolderView.SelectedItems` map reliably from WinEvent source HWND on Windows 11 tabbed Explorer?
- **OQ-017:** What documented preflight strategy covers Desktop drags if ShellWindows matching does not?
- **OQ-018:** Should the first shipping MVP explicitly support only Explorer folder windows if Desktop preflight cannot satisfy G2?

## Image G3

- **OQ-008:** Which image codec library passes G3? SkiaSharp and ImageSharp remain candidates; no dependency is accepted.
- **OQ-009:** Exact JPEG Compress quality/subsampling/effort settings after corpus benchmarking.
- **OQ-011:** Exact WebP lossy/lossless/quality/effort decision after alpha/photo corpus benchmarking. Animated conversion is already out of scope.
- **OQ-012:** What measured Compress policy meets the product promise without surprising degradation, especially when PNG lossless optimization has little/no benefit?
- **OQ-013:** Whether any image input formats beyond static JPEG/PNG belong in the first shipping release. Current G3 baseline is JPEG/PNG only.
- **OQ-014:** Exact source-byte/dimension/decoded-pixel limits after peak-memory/runtime measurement. The guard exists; production numbers remain intentionally unset.

`OQ-010` from earlier checkpoints is resolved by ADR-025: Remove Metadata normalizes visual orientation before stripping orientation metadata and preserves ICC when representable.

## Runtime / product polish

- **OQ-020:** Should a PNG Compress result that is not smaller be discarded as `NoBenefit`, and what user-visible completion behavior should that produce?
- **OQ-021:** Which measured image safety limits become the default MVP configuration after G3 benchmark data exists?

## Recovery / completion follow-ups

- **OQ-023:** What production OLE drag-source implementation should power Start Result Drag after the main G1/G2 interaction is proven?
- **OQ-024:** What minimal user-facing UX should appear when startup recovery is blocked and automatic cleanup cannot safely complete? Runtime must remain disabled in that state.

`OQ-015` is resolved by ADR-029/030/033: recovery is automatic at startup, journal-driven, exact-path-only, with no age scan.

`OQ-019` is resolved by ADR-031/034/037: completion commands are capability-gated; Delete has an identity-safe implementation but remains proof-gated; Start Result Drag remains unimplemented.

`OQ-022` is resolved by ADR-034: generated-output deletion uses Windows volume serial + 128-bit file ID and handle-bound delete disposition; production acceptance still requires P4 evidence.


## Presentation follow-ups

- **OQ-025:** Should successful completion auto-dismiss after a measured usability interval, and how should pointer/keyboard interaction pause that timer? v0.9 intentionally has no hard-coded timeout.
- **OQ-026:** Should post-commit operation/completion status reuse the visual shell of the drag overlay or use a separate non-OLE HWND? Authority/state remain separate either way.

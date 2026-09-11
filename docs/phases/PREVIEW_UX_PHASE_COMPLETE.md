# Preview UX / Vertical Slice — Phase Complete

Status: CLOSED for the safe preview scope  
Closed: 2026-09-03  
Branch: main

## Objective

Deliver a usable, understandable Apple-inspired SmartDrag preview that demonstrates the real local image
pipeline without enabling unproven global Windows activation.

## Acceptance checklist

| Area | Accepted result | Evidence |
|---|---|---|
| First-run comprehension | The main card explains Add -> Choose -> Get and offers drag, picker, paste, and keyboard paths | src/SmartDrag.App/Preview/PreviewWindow.cs |
| Brand/UI system | Vector SmartDrag mark, rounded cards, blue accent, readable hierarchy, Light/Dark palette | assets/smartdrag-logo.svg, preview and overlay adapters |
| Language | Russian default; RU/EN toggle; runtime actions, statuses, completion, picker, safety profile, history, and reset are localized | PreviewWindow.cs, WpfOverlayService.cs |
| Theme | Light/Dark toggle, persisted preference, contrast-safe status/error/success colors, theme-aware drag and overlay hover | PreviewWindow.cs, WpfOverlayService.cs |
| Input routes | JPEG/PNG drag-over feedback, deterministic picker, Ctrl+V, Ctrl+O, Ctrl+L, Ctrl+Shift+T, Esc | PreviewWindow.cs |
| Real processing path | WIC inspection, guard, sequential queue, action dispatch, completion, cancellation, output safety, and result actions are wired | PreviewSelfCheck.cs, runtime graph |
| History | Six bounded terminal entries persist safely; only sanitized names are stored; cards show state, file, and time | PreviewHistoryStore.cs |
| Reset | Confirmed reset restores RU/Light and clears saved preview history without deleting images | PreviewWindow.cs |
| Accessibility | Primary controls have localized automation names; picker is the default focused action | PreviewWindow.cs |
| Self-check | 21 deterministic checks cover image processing, safety, output, queue cancellation, artifact identity, preferences, history round-trips, native activation policy, and controlled WebP rejection | artifacts/preview-self-check/latest.json |

## Verification record

The phase was closed only after these commands passed on Windows 10.0.19045 / .NET SDK 8.0.424:

    dotnet build .\SmartDrag.sln -c Debug --no-restore -p:NuGetAudit=false
    dotnet test .\SmartDrag.sln -c Debug --no-build --no-restore --verbosity minimal
    python scripts\validate_repo.py --allow-build-artifacts
    python scripts\validate_image_corpus.py
    dotnet run --project .\src\SmartDrag.App --no-build -- --self-check=artifacts/preview-self-check/latest.json

Observed result: 19 projects build cleanly, 139 xUnit tests pass, static validation passes, 9 image fixtures pass,
and the current preview self-check reports 21/21.

## Explicit boundary

Closing this phase does not claim production readiness. The following gates remain open and must not be bypassed:

- G1/G2: live Explorer coexistence, focus, native drop, and strict pre-overlay qualification evidence;
- G3: production WebP/codec selection, benchmarked limits, and codec acceptance;
- native production activation remains disabled until those gates are accepted.

The next implementation phase can therefore start from a stable preview UX baseline instead of reworking presentation
fundamentals.

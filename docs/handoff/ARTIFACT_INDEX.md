# SmartDrag Handoff Artifact Index

Read in this order when a coding agent is eventually allowed into the project:

1. `docs/canonical/SMARTDRAG_MASTER_CONTEXT.md` — canonical product source.
2. `PROJECT_STATE.md` — current gate truth; never infer progress from file count.
3. `docs/handoff/CURSOR_ENTRY_PROTOCOL.md` — mandatory agent behavior.
4. `docs/decisions/ADR-000-index.md` — accepted/provisional engineering decisions.
5. `docs/handoff/SMARTDRAG_FUTURE_CURSOR_HANDOFF.md` — accumulated architecture and rationale.
6. `docs/architecture/ARCHITECTURE_FOUNDATION.md` — module boundaries.
7. `docs/orchestration/PRODUCTION_DRAG_TO_JOB_FLOW.md` — authorization/commit path.
8. `docs/recovery/CRASH_RECOVERY_MODEL.md` — reservation journal and startup cleanup authority.
9. `docs/completion/COMPLETION_COMMAND_EXECUTION.md` — capability-gated completion commands.
10. `docs/artifacts/GENERATED_ARTIFACT_IDENTITY.md` — strong output identity and handle-bound deletion.
11. `docs/composition/PRODUCTION_COMPOSITION_ROOT.md` — authoritative deterministic runtime wiring.
12. `docs/imaging/G3_IMAGE_PROCESSING_SPEC.md` — codec-independent image behavior.
13. `docs/ux/MVP_PRESENTATION_SPEC.md` + `PRESENTATION_STATE_AND_INTENT_MODEL.md` — toolkit-neutral UX/runtime contract.
14. `docs/settings/MVP_SETTINGS_POLICY.md` — current validated settings/output-policy boundary.
15. `docs/testing/BUILD_GATE.md` — G0.
16. `docs/testing/P0_TEST_MATRIX.md` + `G2_QUALIFICATION_TEST_MATRIX.md` — Windows evidence.
17. `docs/testing/P2_ORCHESTRATION_TEST_MATRIX.md` + `P3_RECOVERY_COMPLETION_TEST_MATRIX.md` + `P4_ARTIFACT_IDENTITY_COMPOSITION_TEST_MATRIX.md` — deterministic/runtime safety gates.
18. `docs/testing/G3_IMAGE_CODEC_TEST_MATRIX.md` + `tests/fixtures/images/` — codec acceptance.
19. `docs/testing/P5_PRESENTATION_RUNTIME_TEST_MATRIX.md` — post-G0 presentation/runtime wiring gate.
20. `docs/phases/PREVIEW_UX_PHASE_COMPLETE.md` — closed preview UX phase acceptance checklist and evidence.
21. `docs/phases/G1_G2_TOOLING_HALF_COMPLETE.md` — closed probe-tooling half-phase; live Explorer evidence remains open.
22. `docs/phases/G3_CORPUS_AUDIT_PREP_COMPLETE.md` — closed codec-neutral corpus-audit preparation.
23. `docs/phases/G1_G2_EVIDENCE_RETENTION_PHASE_COMPLETE.md` — closed privacy-checked evidence packaging phase.
24. `docs/phases/P5_PREVIEW_RUNTIME_VERIFICATION_COMPLETE.md` — closed safe preview/runtime verification phase.
25. `docs/phases/P4_ARTIFACT_SAFETY_VERIFICATION_COMPLETE.md` — closed local identity-bound artifact verification phase.
26. `docs/phases/SAFE_PREVIEW_RELEASE_PHASE_COMPLETE.md` — closed reproducible safe-preview packaging phase.
27. `docs/phases/G1_G2_OPERATOR_CAPTURE_PHASE_COMPLETE.md` — reproducible P0/G2 evidence capture automation.
28. `docs/phases/PREVIEW_INPUT_RESILIENCE_PHASE_COMPLETE.md` — fail-closed clipboard/drop/error handling in preview.
29. `docs/phases/PREVIEW_CAPABILITY_GATING_PHASE_COMPLETE.md` — truthful action availability before G3 codec acceptance.
30. `docs/phases/PREVIEW_CAPABILITY_HINT_PHASE_COMPLETE.md` — visible explanation of current and gated actions.
31. `docs/phases/PREVIEW_RESPONSIVE_LAYOUT_PHASE_COMPLETE.md` — adaptive wide/compact preview composition.
32. `docs/phases/G1_G2_EVIDENCE_VERIFIER_PHASE_COMPLETE.md` — independent integrity/privacy verification for evidence packages.
33. `docs/handoff/FOUNDATION_CHANGELOG.md` — why the foundation evolved.
34. `docs/phases/OUTPUT_RESERVATION_INTEGRITY_PHASE_COMPLETE.md` — fail-closed reservation contract hardening.
35. `docs/phases/SAFE_PREVIEW_VERIFICATION_PHASE_COMPLETE.md` — independent package integrity and safety verification.
36. `docs/phases/G1_G2_EVIDENCE_PACKAGING_VERIFICATION_PHASE_COMPLETE.md` — mandatory verification after evidence packaging.
37. `docs/phases/RECOVERY_JOURNAL_INTEGRITY_PHASE_COMPLETE.md` — fail-closed durable recovery document validation.
38. `docs/phases/P4_WINDOWS_IDENTITY_TARGET_GUARD_PHASE_COMPLETE.md` — directory/reparse target guards in Windows identity handling.
39. `docs/phases/G3_IMAGE_GUARD_INPUT_BOUNDARY_PHASE_COMPLETE.md` — fail-closed image-processing path validation.
40. `docs/phases/ACTION_REGISTRY_CONTRACT_PHASE_COMPLETE.md` — fail-fast action metadata and capability contract validation.
41. `docs/phases/ORCHESTRATION_SESSION_INTEGRITY_PHASE_COMPLETE.md` — frozen overlay authorization snapshot and exact action-set binding.
42. `docs/phases/PREVIEW_PERSISTENCE_HARDENING_PHASE_COMPLETE.md` — bounded, flushed, fail-closed preview preferences and history persistence.
43. `docs/phases/PRESENTATION_COMMAND_SNAPSHOT_PHASE_COMPLETE.md` — immutable completion command snapshots across runtime and UI projection.
44. `docs/phases/RUNTIME_JOB_SNAPSHOT_INTEGRITY_PHASE_COMPLETE.md` — owned request and read-only published job collections.
45. `docs/phases/COMPLETION_COMMAND_FAILURE_BOUNDARY_PHASE_COMPLETE.md` — fail-closed completion adapter errors and retryable failed deletion attempts.
46. `docs/phases/DRAG_SESSION_LIFECYCLE_PHASE_COMPLETE.md` — race-safe active sessions, stale-signal protection, and overlay-failure cleanup.
47. `docs/testing/MANUAL_TEST_CHECKLIST_RU.md` — единый ручной чек-лист Preview, G1/G2 probe и формат передачи evidence.
48. `docs/phases/PREVIEW_OUTPUT_DELETION_HISTORY_PHASE_COMPLETE.md` — persisted deleted-result state, localized history marker, and verification.
49. `docs/phases/PREVIEW_HISTORY_CLEAR_PHASE_COMPLETE.md` — persisted clear boundary, legacy history migration, and safe clear action.

50. docs/phases/PREVIEW_RESULT_RESILIENCE_PHASE_COMPLETE.md — stale-output handling, safe action visibility, and history durability.
51. docs/phases/PREVIEW_KEYBOARD_ACCESSIBILITY_PHASE_COMPLETE.md — deterministic tab order, focus rings, and guarded shortcuts.
52. docs/phases/PREVIEW_INPUT_STATE_RESILIENCE_PHASE_COMPLETE.md — stale-selection clearing, dismissal gating, and transition policy.
53. docs/phases/PREVIEW_QUEUE_UX_PHASE_COMPLETE.md — localized queue state and queued-behind visibility.
54. docs/phases/PREVIEW_DIAGNOSTICS_UX_PHASE_COMPLETE.md — self-check result state and safe report-folder access.
55. docs/phases/PREVIEW_DYNAMIC_DIAGNOSTICS_PHASE_COMPLETE.md — bounded aggregate self-check parsing with localized counts and duration.
56. docs/phases/PREVIEW_PROCESS_RESILIENCE_PHASE_COMPLETE.md — bounded lifecycle/error logging and controlled process-boundary exit.
57. docs/phases/PREVIEW_RESULT_ACTION_POLICY_PHASE_COMPLETE.md — centralized fail-closed visibility for result controls.
58. docs/phases/PREVIEW_SAFE_COMMAND_ERRORS_PHASE_COMPLETE.md — localized command failures without adapter-detail leakage.
59. docs/phases/PREVIEW_FAILURE_COPY_PHASE_COMPLETE.md — safe localized copy for inspection, preparation, and dispatch failures.
60. docs/phases/PREVIEW_TRANSIENT_COMMAND_STATUS_PHASE_COMPLETE.md — privacy-safe status copy for follow-up result commands.
61. docs/phases/PREVIEW_HISTORY_DISPLAY_POLICY_PHASE_COMPLETE.md — bounded, localized, fail-closed history rendering.
62. docs/phases/PREVIEW_WINDOW_LIFECYCLE_AND_PLACEMENT_PHASE_COMPLETE.md — safe persisted geometry, compact layout floor, and observed background lifecycle work.
63. docs/phases/PREVIEW_THEME_TOKEN_PHASE_COMPLETE.md — shared Light/Dark visual tokens with parity and contrast tests.
64. docs/phases/PREVIEW_OVERLAY_HITTEST_PHASE_COMPLETE.md — explicit action geometry, decorative hit-test isolation, and fail-closed overlay selection.
65. docs/phases/PREVIEW_FILE_PICKER_PHASE_COMPLETE.md — owner-bound system picker, idle gating, and guarded input routing.
66. docs/phases/PREVIEW_INPUT_AVAILABILITY_PHASE_COMPLETE.md — visible picker busy state, disabled controls, and shutdown cancellation.
67. docs/phases/PREVIEW_SELF_CHECK_GATING_PHASE_COMPLETE.md — idle-only diagnostics and non-clobbering self-check UX.
68. docs/phases/PREVIEW_OVERLAY_THEME_ACCESSIBILITY_PHASE_COMPLETE.md — localized automation metadata and theme-aligned overlay accents.
69. docs/phases/PREVIEW_RESULT_STATE_RECONCILIATION_PHASE_COMPLETE.md — stale result cleanup and explicit quiet-state rendering.
70. docs/phases/PREVIEW_BUTTON_FEEDBACK_PHASE_COMPLETE.md — consistent hover, pressed, and disabled button visuals.
71. docs/phases/PREVIEW_DROP_AVAILABILITY_PHASE_COMPLETE.md — honest drag-over effects and idle-only drop highlighting.
72. docs/phases/PREVIEW_TYPOGRAPHY_PHASE_COMPLETE.md — shared variable-font stack and crisp WPF text rendering.
73. docs/phases/PREVIEW_LAUNCH_SCRIPT_PHASE_COMPLETE.md — canonical one-command Preview/self-check launcher.
74. docs/phases/PREVIEW_ASYNC_CALLBACK_BOUNDARY_PHASE_COMPLETE.md — safe keyboard/drop callback containment and diagnostics.
75. docs/phases/PREVIEW_ACCESSIBILITY_LIVE_STATUS_PHASE_COMPLETE.md — localized live regions for status, result, and progress.
76. docs/phases/PREVIEW_DISPATCH_HANDOFF_PHASE_COMPLETE.md — fail-closed post-commit handoff and input gating.
77. docs/phases/PREVIEW_LIFECYCLE_CANCELLATION_PHASE_COMPLETE.md — shutdown-aware async callbacks and quiet cancellation.
78. docs/phases/PREVIEW_OVERLAY_LIFECYCLE_PHASE_COMPLETE.md — deterministic overlay event teardown and dispatcher-safe disposal.
79. docs/phases/DOCUMENTATION_TRUTH_SYNC_PHASE_COMPLETE.md — current build/test evidence synchronized across operator docs.

## High-risk source boundaries

- `src/SmartDrag.Windows/` — native callbacks, OLE, Windows shell/clipboard/lifetime.
- `src/SmartDrag.Orchestration/` — overlay authorization and commit/session lifetime.
- `src/SmartDrag.Infrastructure/PhysicalOutputManager.cs` — non-destructive output and durable reservation rule.
- `src/SmartDrag.Infrastructure/Recovery/` — deletion-capable crash recovery; never broaden authority.
- `src/SmartDrag.Runtime/SequentialJobQueue.cs` — idempotent execution/cancellation.
- `src/SmartDrag.Runtime/Completion*` — terminal projection and command capability revalidation.
- `src/SmartDrag.Imaging/` — codec-neutral until G3 acceptance; production codec must pass through `GuardedImageProcessor`.
- `src/SmartDrag.Windows/Artifacts/` — strong generated-file identity and handle-bound destructive boundary.
- `src/SmartDrag.App/Composition/` — only intended deterministic production wiring path.
- `src/SmartDrag.Presentation/` — user-visible state/intent boundary; must remain toolkit-neutral and must not gain filesystem/codec/native authority.

## Current forbidden shortcuts

- no Cursor-driven scope expansion;
- no Explorer injection/detouring;
- no MOVE drop effect;
- no UI/interop-created ActionRequest bypass;
- no UI/native-provided arbitrary OutputPolicy;
- no fake numeric processing progress or technical/full-path presentation leaks;
- no user-directory wildcard cleanup;
- no generated-output delete by path-only ownership or verify-then-DeleteFile;
- no result-drag before proof;
- no concrete image codec before G3 and never bypass `GuardedImageProcessor`;
- no native activation while `ProductionActivationPolicy.NativeActivationEnabled=false`;
- no PDF/archive/video/FFmpeg implementation.

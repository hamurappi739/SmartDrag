#!/usr/bin/env python3
"""Static repository integrity checks that do not require the .NET SDK."""
from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []
allow_build_artifacts = "--allow-build-artifacts" in sys.argv[1:]


def fail(message: str) -> None:
    errors.append(message)


# 1. Every project in the tree should be listed in the solution, and every solution path should exist.
sln = ROOT / "SmartDrag.sln"
solution_text = sln.read_text(encoding="utf-8")
solution_projects = {
    path.replace("\\", "/")
    for path in re.findall(r'Project\("\{[^}]+\}"\) = "[^"]+", "([^"]+\.csproj)"', solution_text)
}
actual_projects = {
    str(path.relative_to(ROOT)).replace("\\", "/")
    for path in ROOT.rglob("*.csproj")
}

for project in sorted(solution_projects):
    if not (ROOT / project).exists():
        fail(f"Solution references missing project: {project}")

for project in sorted(actual_projects - solution_projects):
    fail(f"Project exists but is not listed in SmartDrag.sln: {project}")

# 2. Parse project XML and validate ProjectReference paths.
for project_path in sorted(ROOT.rglob("*.csproj")):
    try:
        tree = ET.parse(project_path)
    except ET.ParseError as exc:
        fail(f"Invalid XML in {project_path.relative_to(ROOT)}: {exc}")
        continue

    for ref in tree.findall(".//ProjectReference"):
        include = ref.attrib.get("Include")
        if not include:
            fail(f"ProjectReference without Include in {project_path.relative_to(ROOT)}")
            continue
        resolved = (project_path.parent / include.replace("\\", "/")).resolve()
        if not resolved.exists():
            fail(f"Broken ProjectReference in {project_path.relative_to(ROOT)}: {include} -> {resolved}")

# 3. Protect G3: no codec or FFmpeg dependency before the codec gate.
for project_path in sorted(ROOT.rglob("*.csproj")):
    text = project_path.read_text(encoding="utf-8")
    for forbidden in ("SixLabors.ImageSharp", "SkiaSharp", "FFmpeg", "FFMpegCore"):
        if forbidden.lower() in text.lower():
            fail(f"Forbidden pre-G3 dependency '{forbidden}' in {project_path.relative_to(ROOT)}")

# 4. Required canonical/handoff/evidence files.
required = [
    "docs/canonical/SMARTDRAG_MASTER_CONTEXT.md",
    "docs/handoff/SMARTDRAG_FUTURE_CURSOR_HANDOFF.md",
    "docs/testing/P0_TEST_MATRIX.md",
    "docs/testing/P1_RUNTIME_TEST_MATRIX.md",
    "docs/testing/G2_QUALIFICATION_TEST_MATRIX.md",
    "docs/windows/G2_PREOVERLAY_QUALIFICATION.md",
    "docs/windows/NATIVE_BOUNDARY_SAFETY.md",
    "docs/testing/PROBE_EVIDENCE_RUNBOOK.md",
    "docs/decisions/ADR-021.md",
    "docs/decisions/ADR-022.md",
    "docs/decisions/ADR-023.md",
    "docs/decisions/ADR-024.md",
    "docs/decisions/ADR-025.md",
    "docs/decisions/ADR-026.md",
    "docs/decisions/ADR-027.md",
    "docs/decisions/ADR-028.md",
    "docs/decisions/ADR-029.md",
    "docs/decisions/ADR-030.md",
    "docs/decisions/ADR-031.md",
    "docs/decisions/ADR-032.md",
    "docs/decisions/ADR-033.md",
    "docs/decisions/ADR-034.md",
    "docs/decisions/ADR-035.md",
    "docs/decisions/ADR-036.md",
    "docs/decisions/ADR-037.md",
    "docs/orchestration/PRODUCTION_DRAG_TO_JOB_FLOW.md",
    "docs/testing/P2_ORCHESTRATION_TEST_MATRIX.md",
    "docs/imaging/G3_IMAGE_PROCESSING_SPEC.md",
    "docs/imaging/IMAGE_TEST_CORPUS.md",
    "docs/testing/G3_IMAGE_CODEC_TEST_MATRIX.md",
    "docs/runtime/RUNTIME_EXECUTION_MODEL.md",
    "docs/runtime/OUTPUT_SAFETY_MODEL.md",
    "docs/runtime/COMPLETION_MODEL.md",
    "docs/recovery/CRASH_RECOVERY_MODEL.md",
    "docs/recovery/OUTPUT_RECOVERY_JOURNAL_FORMAT.md",
    "schemas/output-recovery-journal.v1.schema.json",
    "docs/completion/COMPLETION_COMMAND_EXECUTION.md",
    "docs/artifacts/GENERATED_ARTIFACT_IDENTITY.md",
    "docs/composition/PRODUCTION_COMPOSITION_ROOT.md",
    "docs/testing/P3_RECOVERY_COMPLETION_TEST_MATRIX.md",
    "docs/testing/P4_ARTIFACT_IDENTITY_COMPOSITION_TEST_MATRIX.md",
    "docs/testing/STATIC_REVIEW_v0.7.md",
    "docs/testing/STATIC_REVIEW_v0.8.md",
    "PROJECT_STATE.md",
]
for relative in required:
    if not (ROOT / relative).exists():
        fail(f"Missing required repository artifact: {relative}")

# 4b. JSON schemas committed for durable formats must themselves parse.
for schema_relative in ("schemas/output-recovery-journal.v1.schema.json",):
    schema_path = ROOT / schema_relative
    if schema_path.exists():
        try:
            json.loads(schema_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            fail(f"Invalid JSON schema {schema_relative}: {exc}")

# 5. Stable built-in ActionIds are declared centrally.
action_ids_file = ROOT / "src/SmartDrag.Core/Actions/BuiltInActionIds.cs"
if not action_ids_file.exists():
    fail("Missing central BuiltInActionIds.cs")
else:
    action_text = action_ids_file.read_text(encoding="utf-8")
    for action_id in ("image.compress", "image.convert.webp", "image.remove-metadata"):
        if action_text.count(f'"{action_id}"') != 1:
            fail(f"ActionId must be declared exactly once in BuiltInActionIds.cs: {action_id}")

# 6. The handoff package must stay clean. The build script may opt out for its own reruns because
# restore/build legitimately create bin/obj below the source tree.
if not allow_build_artifacts:
    for forbidden_dir in ("bin", "obj", ".vs"):
        found = [p for p in ROOT.rglob(forbidden_dir) if p.is_dir()]
        if found:
            fail(f"Build artifact directory present: {found[0].relative_to(ROOT)}")

# 7. v0.5 qualification invariants.
qualifier_file = ROOT / "src/SmartDrag.Core/Payload/MvpPayloadQualifier.cs"
if not qualifier_file.exists():
    fail("Missing MvpPayloadQualifier.cs")
else:
    qualifier_text = qualifier_file.read_text(encoding="utf-8")
    for required_token in (
        "PayloadQualificationState.Eligible",
        "PayloadEvidenceSource.OleDataObject",
        "PathsMatchPreflight",
    ):
        if required_token not in qualifier_text:
            fail(f"Payload qualifier missing required invariant token: {required_token}")

probe_program = ROOT / "tools/SmartDrag.Windows.Probe/Program.cs"
if probe_program.exists():
    probe_text = probe_program.read_text(encoding="utf-8")
    for mode_token in ("--mode=p0", "--mode=g2"):
        if mode_token not in probe_text:
            fail(f"Windows probe missing explicit mode: {mode_token}")

legacy_classifier = ROOT / "tools/SmartDrag.Windows.Probe/ImageExtensionClassifier.cs"
if legacy_classifier.exists():
    fail("Legacy probe-local ImageExtensionClassifier.cs must not duplicate Core payload policy")


# 8. Native boundary safety invariants.
probe_drop_target = ROOT / "tools/SmartDrag.Windows.Probe/ProbeDropTarget.cs"
if probe_drop_target.exists():
    drop_text = probe_drop_target.read_text(encoding="utf-8")
    for required_token in ("FailOleCallback", "DROPEFFECT_NONE", "exception.HResult"):
        if required_token not in drop_text:
            fail(f"ProbeDropTarget missing native-boundary invariant token: {required_token}")
    if "exception.Message" in drop_text or "ex.Message" in drop_text:
        fail("ProbeDropTarget must not log native-boundary exception messages")

winevent_source = ROOT / "src/SmartDrag.Windows/Drag/WinEventDragSignalSource.cs"
if winevent_source.exists():
    winevent_text = winevent_source.read_text(encoding="utf-8")
    if "DroppedSignals" not in winevent_text or "catch (Exception)" not in winevent_text:
        fail("WinEvent callback must expose dropped-signal evidence and remain no-throw")


# 9. v0.6 orchestration gate invariants.
orchestration_file = ROOT / "src/SmartDrag.Orchestration/DragWorkflowOrchestrator.cs"
if not orchestration_file.exists():
    fail("Missing production DragWorkflowOrchestrator.cs")
else:
    orchestration_text = orchestration_file.read_text(encoding="utf-8")
    for required_token in (
        "PreparedOverlaySession",
        "PathsMatchPreflight",
        "ActionWasNotOffered",
        "ActionNoLongerAvailable",
        "PreserveSource",
    ):
        if required_token not in orchestration_text:
            fail(f"Production orchestration missing commit-gate token: {required_token}")

# Orchestration must stay portable/pure at project boundary.
orch_project = ROOT / "src/SmartDrag.Orchestration/SmartDrag.Orchestration.csproj"
if orch_project.exists():
    orch_text = orch_project.read_text(encoding="utf-8")
    for forbidden in ("SmartDrag.Windows", "SmartDrag.Overlay", "SmartDrag.Imaging", "SmartDrag.Infrastructure"):
        if forbidden in orch_text:
            fail(f"Orchestration project must not depend on implementation project: {forbidden}")

# 10. G3 policy/corpus exists without activating a concrete codec dependency.
image_contract = ROOT / "src/SmartDrag.Imaging/ImageCodecContracts.cs"
semantics = ROOT / "src/SmartDrag.Imaging/MvpImageActionSemantics.cs"
for required_file, tokens in (
    (image_contract, ("IImageInspector", "ImageSafetyLimits", "MvpImageExecutionGuard", "AnimatedInputNotSupported")),
    (semantics, ("NormalizePixelsBeforeRemovingOrientationMetadata", "PreserveWhenRepresentable", "NonVisualMetadataPolicy.Remove")),
):
    if not required_file.exists():
        fail(f"Missing G3 policy source: {required_file.relative_to(ROOT)}")
    else:
        text = required_file.read_text(encoding="utf-8")
        for token in tokens:
            if token not in text:
                fail(f"G3 policy source {required_file.name} missing token: {token}")

corpus_manifest = ROOT / "tests/fixtures/images/CORPUS_MANIFEST.json"
if not corpus_manifest.exists():
    fail("Missing deterministic G3 image corpus manifest")

# 11. v0.6 authorization/idempotency invariants.
prepared = ROOT / "src/SmartDrag.Orchestration/PreparedOverlaySession.cs"
commit = ROOT / "src/SmartDrag.Orchestration/ActionCommitDecision.cs"
dispatcher = ROOT / "src/SmartDrag.Orchestration/CommittedActionDispatcher.cs"
queue = ROOT / "src/SmartDrag.Runtime/SequentialJobQueue.cs"
checks = [
    (prepared, ("internal PreparedOverlaySession", "ToFrozenSet", "internal PayloadQualificationResult")),
    (commit, ("internal ActionCommitDecision", "public bool IsAccepted")),
    (dispatcher, ("_dispatched", "return existing.JobId", "existing.ActionId != request.ActionId")),
    (queue, ("_requestIndex", "Equivalent(existing, request)", "return existing.JobId")),
]
for path, tokens in checks:
    if not path.exists():
        fail(f"Missing v0.6 authorization/idempotency source: {path.relative_to(ROOT)}")
        continue
    text = path.read_text(encoding="utf-8")
    for token in tokens:
        if token not in text:
            fail(f"v0.6 invariant missing in {path.name}: {token}")



# 12. v0.7 durable recovery and completion/session safety invariants.
output_manager = ROOT / "src/SmartDrag.Infrastructure/PhysicalOutputManager.cs"
if not output_manager.exists():
    fail("Missing PhysicalOutputManager.cs")
else:
    text = output_manager.read_text(encoding="utf-8")
    for token in (
        "IOutputRecoveryJournal",
        "_recoveryJournal.UpsertAsync",
        ".smartdrag-{reservationId:N}.partial",
        "BestEffortRemoveJournalEntryAsync",
        "preCommitIdentity",
        "postCommitIdentity == preCommitIdentity",
        "GeneratedArtifact",
    ):
        if token not in text:
            fail(f"v0.7 output recovery invariant missing: {token}")

recovery_service = ROOT / "src/SmartDrag.Infrastructure/Recovery/OutputStartupRecoveryService.cs"
if not recovery_service.exists():
    fail("Missing OutputStartupRecoveryService.cs")
else:
    text = recovery_service.read_text(encoding="utf-8")
    for token in ("IsValidRecoveryPath", "ReservationId", "File.Delete(record.TemporaryPath)"):
        if token not in text:
            fail(f"v0.7 recovery service missing invariant: {token}")
    for forbidden in ("EnumerateFiles", "GetFiles(", "EnumerateFileSystemEntries", "Directory.Enumerate"):
        if forbidden in text:
            fail(f"Recovery service must never scan user directories: {forbidden}")

journal = ROOT / "src/SmartDrag.Infrastructure/Recovery/JsonOutputRecoveryJournal.cs"
if not journal.exists():
    fail("Missing durable JsonOutputRecoveryJournal.cs")
else:
    text = journal.read_text(encoding="utf-8")
    for token in ("MaximumEntries", "MaximumJournalBytes", "Revision = checked(document.Revision + 1)", "PromotePendingUnsafe", "_pendingPath"):
        if token not in text:
            fail(f"Durable recovery journal missing invariant: {token}")

windows_completion = ROOT / "src/SmartDrag.Windows/Completion/WindowsCompletionPlatformService.cs"
if not windows_completion.exists():
    fail("Missing WindowsCompletionPlatformService.cs")
else:
    text = windows_completion.read_text(encoding="utf-8")
    for token in ("clipboardOwnerHwnd != IntPtr.Zero", "OpenClipboard(_clipboardOwnerHwnd)", "GlobalAlloc", "EmptyClipboard"):
        if token not in text:
            fail(f"Windows completion clipboard safety invariant missing: {token}")
    if text.find("var memory = NativeMethods.GlobalAlloc") > text.find("if (!NativeMethods.EmptyClipboard"):
        fail("Clipboard buffer must be allocated before EmptyClipboard is called")

completion_executor = ROOT / "src/SmartDrag.Runtime/CompletionCommandExecutor.cs"
if not completion_executor.exists():
    fail("Missing CompletionCommandExecutor.cs")
else:
    text = completion_executor.read_text(encoding="utf-8")
    for token in (
        "IGeneratedArtifactDeletionService",
        "CanDeleteGeneratedOutput = deletionService?.IsSupported == true",
        "TryGetIdentityBackedSingleArtifact",
        "DeleteIfIdentityMatchesAsync",
        "_deleteAttempts.TryAdd",
        "_deleteAttempts.TryRemove",
        "CommandNotOffered",
        "CompletionProjector.FromTerminalJob",
    ):
        if token not in text:
            fail(f"Completion execution safety invariant missing: {token}")

interaction = ROOT / "src/SmartDrag.Orchestration/DragInteractionCoordinator.cs"
if not interaction.exists():
    fail("Missing DragInteractionCoordinator.cs")
else:
    text = interaction.read_text(encoding="utf-8")
    for token in ("_active = null; // consume capability", "_active.DragSessionId != dragSessionId", "OverlayHideReason.ActionCommitted"):
        if token not in text:
            fail(f"Active drag capability invariant missing: {token}")

mutex_lease = ROOT / "src/SmartDrag.Windows/Lifetime/NamedMutexSingleInstanceLease.cs"
if not mutex_lease.exists():
    fail("Missing NamedMutexSingleInstanceLease.cs")
else:
    text = mutex_lease.read_text(encoding="utf-8")
    for token in ("new Thread", "mutex.WaitOne(0)", "release.Wait()", "mutex.ReleaseMutex()", "_ownerThread.Join()"):
        if token not in text:
            fail(f"Single-instance lifetime-thread invariant missing: {token}")

startup = ROOT / "src/SmartDrag.App/StartupSafetyBootstrap.cs"
if not startup.exists():
    fail("Missing StartupSafetyBootstrap.cs")
else:
    text = startup.read_text(encoding="utf-8")
    ordered = [
        "NamedMutexSingleInstanceLease.TryAcquire",
        "new JsonOutputRecoveryJournal",
        "new OutputStartupRecoveryService",
    ]
    positions = [text.find(token) for token in ordered]
    if any(position < 0 for position in positions) or positions != sorted(positions):
        fail("Startup safety order must be single-instance -> journal -> recovery")
    for token in ("RecoveryBlocked", "RuntimeMayStart"):
        if token not in text:
            fail(f"Startup recovery blocking invariant missing: {token}")


# 13. v0.8 strong artifact identity / guarded codec / composition invariants.
artifact_contract = ROOT / "src/SmartDrag.Core/Artifacts/GeneratedArtifact.cs"
if not artifact_contract.exists():
    fail("Missing GeneratedArtifact identity contracts")
else:
    text = artifact_contract.read_text(encoding="utf-8")
    for token in ("ArtifactIdentity", "IGeneratedArtifactIdentityProvider", "IGeneratedArtifactDeletionService"):
        if token not in text:
            fail(f"Generated artifact contract missing v0.8 token: {token}")

windows_identity = ROOT / "src/SmartDrag.Windows/Artifacts/WindowsGeneratedArtifactIdentityService.cs"
if not windows_identity.exists():
    fail("Missing WindowsGeneratedArtifactIdentityService.cs")
else:
    text = windows_identity.read_text(encoding="utf-8")
    for token in (
        "windows-file-id-v1",
        "FileFlagOpenReparsePoint",
        "GetFileInformationByHandleEx",
        "SetFileInformationByHandle",
        "FileDispositionInfoClass",
        "current.Identity != artifact.Identity",
    ):
        if token not in text:
            fail(f"Windows artifact identity/delete invariant missing: {token}")
    if "DeleteFile(" in text or "File.Delete(" in text:
        fail("Identity-safe generated-artifact deletion must not fall back to path-based DeleteFile/File.Delete")

for source in (
    ROOT / "src/SmartDrag.Core/Actions/ActionResult.cs",
    ROOT / "src/SmartDrag.Core/Jobs/JobSnapshot.cs",
    ROOT / "src/SmartDrag.Runtime/SequentialJobQueue.cs",
):
    if source.exists() and "GeneratedOutputPaths" in source.read_text(encoding="utf-8"):
        fail(f"Legacy bare GeneratedOutputPaths must not remain in runtime source: {source.relative_to(ROOT)}")

guarded = ROOT / "src/SmartDrag.Imaging/GuardedImageProcessor.cs"
if not guarded.exists():
    fail("Missing mandatory GuardedImageProcessor.cs")
else:
    text = guarded.read_text(encoding="utf-8")
    for token in ("IImageInspector", "MvpImageExecutionGuard.Evaluate", "_inner", "ImageResourceLimitExceeded"):
        if token not in text:
            fail(f"Guarded image processor missing invariant: {token}")

composition = ROOT / "src/SmartDrag.App/Composition/ProductionRuntimeGraph.cs"
activation = ROOT / "src/SmartDrag.App/Composition/ProductionActivationPolicy.cs"
if not composition.exists():
    fail("Missing ProductionRuntimeGraph.cs")
else:
    text = composition.read_text(encoding="utf-8")
    for token in (
        "bootstrap.RuntimeMayStart",
        "WindowsGeneratedArtifactIdentityService",
        "new PhysicalOutputManager(bootstrap.Lease.RecoveryJournal, identity)",
        "new GuardedImageProcessor",
        "new CompletionCommandExecutor(queue, completionPlatform, identity)",
        "await _jobQueue.DisposeAsync",
        "_startupSafetyLease.Dispose",
    ):
        if token not in text:
            fail(f"Production composition missing v0.8 invariant: {token}")

if not activation.exists():
    fail("Missing ProductionActivationPolicy.cs")
else:
    text = activation.read_text(encoding="utf-8")
    if "public const bool NativeActivationEnabled = false;" not in text:
        fail("Production native activation kill-switch must remain false before empirical gate acceptance")



# 14. v0.9 presentation / safe application-facing wiring invariants.
presentation_project = ROOT / "src/SmartDrag.Presentation/SmartDrag.Presentation.csproj"
presentation_coordinator = ROOT / "src/SmartDrag.Presentation/MvpPresentationCoordinator.cs"
presentation_policy = ROOT / "src/SmartDrag.Presentation/MvpPresentationPolicy.cs"
production_drag = ROOT / "src/SmartDrag.Orchestration/IProductionDragInteraction.cs"
settings_policy = ROOT / "src/SmartDrag.Core/Settings/MvpSettingsPolicy.cs"
output_factory = ROOT / "src/SmartDrag.Core/Output/MvpOutputPolicyFactory.cs"

for required in (
    presentation_project,
    presentation_coordinator,
    presentation_policy,
    production_drag,
    settings_policy,
    output_factory,
    ROOT / "docs/ux/MVP_PRESENTATION_SPEC.md",
    ROOT / "docs/testing/P5_PRESENTATION_RUNTIME_TEST_MATRIX.md",
    ROOT / "docs/decisions/ADR-038.md",
    ROOT / "docs/decisions/ADR-039.md",
    ROOT / "docs/decisions/ADR-040.md",
    ROOT / "docs/decisions/ADR-041.md",
):
    if not required.exists():
        fail(f"Missing v0.9 presentation artifact: {required.relative_to(ROOT)}")

if presentation_policy.exists():
    text = presentation_policy.read_text(encoding="utf-8")
    for token in ("UsesIndeterminateProgress = true", "SourceDisplayName", "SafeUserError"):
        if token not in text:
            fail(f"Presentation policy missing v0.9 invariant: {token}")
    if "ProgressPercent" in text or "PercentComplete" in text:
        fail("v0.9 must not fabricate numeric processing progress")

if presentation_coordinator.exists():
    text = presentation_coordinator.read_text(encoding="utf-8")
    for token in (
        "TryCancelVisibleOperation",
        "ExecuteVisibleCompletionCommandAsync",
        "_pendingCompletions",
        "_completionCommands.ExecuteAsync",
        "Publish(changed)",
    ):
        if token not in text:
            fail(f"Presentation coordinator missing intent/FIFO invariant: {token}")

if production_drag.exists():
    text = production_drag.read_text(encoding="utf-8")
    if "TryCommitMvpAsync" not in text:
        fail("Production drag interface must expose TryCommitMvpAsync")
    if "OutputPolicy outputPolicy" in text or "OutputPolicy," in text:
        fail("Application-facing production drag interface must not accept OutputPolicy")

if settings_policy.exists():
    text = settings_policy.read_text(encoding="utf-8")
    for token in ("PreserveSource must remain enabled", "double.IsFinite", "MinActivationDelayMs"):
        if token not in text:
            fail(f"MVP settings validation invariant missing: {token}")

if output_factory.exists():
    text = output_factory.read_text(encoding="utf-8")
    if "OutputPolicy.SafeMvpDefault" not in text:
        fail("MVP output policy factory must return the canonical safe policy")

if composition.exists():
    text = composition.read_text(encoding="utf-8")
    for forbidden in (
        "public IJobQueue JobQueue",
        "public DragWorkflowOrchestrator DragWorkflow",
        "public CompletionCoordinator CompletionCoordinator",
        "public ICompletionCommandExecutor CompletionCommands",
    ):
        if forbidden in text:
            fail(f"Production graph leaks runtime authority to UI: {forbidden}")
    for token in ("public IProductionDragInteraction DragInteraction", "public MvpPresentationCoordinator Presentation"):
        if token not in text:
            fail(f"Production graph missing safe v0.9 surface: {token}")

if errors:
    print("STATIC VALIDATION FAILED")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("STATIC VALIDATION PASSED")
print(f"Projects in solution: {len(solution_projects)}")
print(f"Project files discovered: {len(actual_projects)}")
print("Pre-G3 codec dependency gate: clean")
print("G2 payload qualification invariants: present")
print("Native callback safety invariants: present")
print("P2 orchestration commit gate: present")
print("G3 codec-neutral image policy/corpus: present")
print("v0.6 capability/idempotency guards: present")
print("v0.7 recovery/completion/session/startup guards: present")
print("v0.8 artifact identity/guarded codec/composition guards: present")
print("v0.9 presentation/settings/application-surface guards: present")

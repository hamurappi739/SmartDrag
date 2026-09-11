using SmartDrag.Core.Actions;
using SmartDrag.Core.Drag;
using SmartDrag.Core.Output;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;

namespace SmartDrag.Orchestration;

/// <summary>
/// Pure production workflow policy for the boundary between drag qualification, overlay presentation,
/// authoritative OLE payload validation, and ActionRequest creation.
/// No Win32, COM, UI framework, filesystem implementation, or codec may be referenced here.
/// </summary>
public sealed class DragWorkflowOrchestrator
{
    private readonly IActionRegistry _actionRegistry;

    public DragWorkflowOrchestrator(IActionRegistry actionRegistry)
    {
        _actionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
    }

    public OverlayPreparationResult PrepareOverlay(
        Guid dragSessionId,
        PayloadQualificationResult preflight,
        AppCapabilities capabilities,
        UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(preflight);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(settings);

        var settingsValidation = MvpSettingsPolicy.Validate(settings);
        if (!settingsValidation.IsValid)
        {
            return Suppressed($"MVP settings are invalid: {settingsValidation.Summary}");
        }

        if (dragSessionId == Guid.Empty)
        {
            return Suppressed("DragSessionId is empty.");
        }

        if (!preflight.MayShowOverlay || preflight.Payload is null)
        {
            return Suppressed($"Payload is not eligible for overlay presentation: {preflight.State}/{preflight.Reason}.");
        }

        var context = new ActionContext
        {
            Payload = preflight.Payload,
            Capabilities = capabilities,
            Settings = settings
        };

        var available = _actionRegistry.GetAvailable(context);
        var eligibility = OverlayEligibilityEvaluator.Evaluate(preflight, available.Count);
        if (!eligibility.ShouldShow)
        {
            return Suppressed($"Overlay eligibility rejected the session: {eligibility.Reason}.");
        }

        var actions = available
            .Select(action => new OverlayActionItem
            {
                ActionId = action.Id,
                Label = action.DisplayName,
                IconId = action.IconId,
                IsEnabled = true
            })
            .ToArray();

        var payloadLabel = BuildPayloadLabel(preflight.Payload);
        var overlay = new OverlayModel
        {
            DragSessionId = dragSessionId,
            PayloadLabel = payloadLabel,
            Actions = actions
        };

        return new OverlayPreparationResult(
            OverlayPreparationStatus.Prepared,
            "Payload is eligible and at least one local action is available.",
            new PreparedOverlaySession(
                dragSessionId,
                preflight,
                actions.Select(action => action.ActionId),
                overlay));
    }

    public ActionCommitDecision TryCommitDrop(
        PreparedOverlaySession? prepared,
        ActionId selectedActionId,
        PayloadQualificationResult authoritative,
        AppCapabilities capabilities,
        UserSettings settings,
        OutputPolicy outputPolicy)
    {
        ArgumentNullException.ThrowIfNull(authoritative);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(outputPolicy);

        var settingsValidation = MvpSettingsPolicy.Validate(settings);
        if (!settingsValidation.IsValid)
        {
            return Reject(ActionCommitStatus.InvalidSettings, $"MVP settings are invalid: {settingsValidation.Summary}");
        }

        if (prepared is null || prepared.DragSessionId == Guid.Empty)
        {
            return Reject(ActionCommitStatus.OverlaySessionInvalid, "No valid prepared overlay session exists for this drop.");
        }

        if (!authoritative.IsAuthoritative || authoritative.Payload is null)
        {
            return Reject(ActionCommitStatus.PayloadNotAuthoritative, "The OLE payload was not authoritatively qualified as supported.");
        }

        if (!MvpPayloadQualifier.PathsMatchPreflight(prepared.PreflightQualification, authoritative))
        {
            return Reject(ActionCommitStatus.PreflightMismatch, "The authoritative OLE payload does not match the preflight payload that authorized the overlay.");
        }

        if (!prepared.OfferedActionIds.Contains(selectedActionId))
        {
            return Reject(ActionCommitStatus.ActionWasNotOffered, "The selected action was not offered by this overlay session.");
        }

        var authoritativeContext = new ActionContext
        {
            Payload = authoritative.Payload,
            Capabilities = capabilities,
            Settings = settings
        };

        var stillAvailable = _actionRegistry.GetAvailable(authoritativeContext)
            .Any(action => action.Id == selectedActionId);
        if (!stillAvailable)
        {
            return Reject(ActionCommitStatus.ActionNoLongerAvailable, "The action is no longer available for the authoritative payload/capabilities.");
        }

        // Current MVP is explicitly non-destructive. Do not let a caller smuggle an unsafe policy
        // through the OLE boundary even if an implementation accidentally exposes one later.
        if (!outputPolicy.PreserveSource)
        {
            return Reject(ActionCommitStatus.UnsafeOutputPolicy, "MVP commit requires PreserveSource=true.");
        }

        var paths = authoritative.Payload.Files.Select(file => file.FullPath).ToArray();
        if (paths.Length != 1)
        {
            return Reject(ActionCommitStatus.PayloadNotAuthoritative, "Single-file MVP requires exactly one authoritative input path.");
        }

        return new ActionCommitDecision(
            ActionCommitStatus.Accepted,
            "Authoritative payload, preflight identity, offered action, and output safety policy all passed.",
            new ActionRequest
            {
                // One physical drag is allowed to authorize at most one logical request. Reusing the DragSessionId
                // gives dispatch/queue layers a stable idempotency key if native/UI delivery is duplicated.
                RequestId = prepared.DragSessionId,
                ActionId = selectedActionId,
                InputPaths = paths,
                OutputPolicy = outputPolicy
            });
    }

    private static string BuildPayloadLabel(DragPayloadInfo payload)
    {
        if (payload.Files.Count == 1)
        {
            try
            {
                var name = Path.GetFileName(payload.Files[0].FullPath);
                return string.IsNullOrWhiteSpace(name) ? "1 file" : name;
            }
            catch
            {
                return "1 file";
            }
        }

        return $"{payload.Files.Count} files";
    }

    private static OverlayPreparationResult Suppressed(string reason) =>
        new(OverlayPreparationStatus.Suppressed, reason);

    private static ActionCommitDecision Reject(ActionCommitStatus status, string reason) =>
        new(status, reason);
}

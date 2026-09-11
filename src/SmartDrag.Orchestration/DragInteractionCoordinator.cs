using SmartDrag.Core.Actions;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Output;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;

namespace SmartDrag.Orchestration;

/// <summary>
/// Owns the single active production drag interaction. UI/Win32 adapters may feed qualified evidence into this
/// coordinator, but only the underlying DragWorkflowOrchestrator can create authorization capabilities and only
/// CommittedActionDispatcher can enqueue an accepted request.
/// </summary>
public sealed class DragInteractionCoordinator : IProductionDragInteraction
{
    private readonly DragWorkflowOrchestrator _workflow;
    private readonly CommittedActionDispatcher _dispatcher;
    private readonly IOverlayService _overlay;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PreparedOverlaySession? _active;

    public DragInteractionCoordinator(
        DragWorkflowOrchestrator workflow,
        CommittedActionDispatcher dispatcher,
        IOverlayService overlay)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
    }

    public Guid? ActiveDragSessionId => Volatile.Read(ref _active)?.DragSessionId;

    public async Task<OverlayPreparationResult> TryPresentAsync(
        Guid dragSessionId,
        PayloadQualificationResult preflight,
        AppCapabilities capabilities,
        UserSettings settings,
        OverlayPlacement placement,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_active is not null)
            {
                // Only one native drag can own the transient overlay. A newer qualified session supersedes stale
                // state rather than sharing capabilities across drag sessions.
                await SafeHideAsync(OverlayHideReason.Suppressed).ConfigureAwait(false);
                _active = null;
            }

            var preparation = _workflow.PrepareOverlay(dragSessionId, preflight, capabilities, settings);
            if (!preparation.IsPrepared || preparation.Session is null)
            {
                return preparation;
            }

            try
            {
                await _overlay.ShowAsync(preparation.Session.Overlay, placement, cancellationToken).ConfigureAwait(false);
                _active = preparation.Session;
                return preparation;
            }
            catch
            {
                _active = null;
                await SafeHideAsync(OverlayHideReason.Error).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ActionDispatchResult> TryCommitMvpAsync(
        Guid dragSessionId,
        ActionId selectedActionId,
        PayloadQualificationResult authoritative,
        AppCapabilities capabilities,
        UserSettings settings,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_active is null || _active.DragSessionId != dragSessionId)
            {
                return ActionDispatchResult.Rejected(ActionCommitStatus.OverlaySessionInvalid, "The active overlay does not belong to this drag session.");
            }

            var active = _active;
            _active = null; // consume capability before any await/dispatch; one physical drag gets one commit attempt.

            await SafeHideAsync(OverlayHideReason.ActionCommitted).ConfigureAwait(false);

            OutputPolicy outputPolicy;
            try
            {
                outputPolicy = MvpOutputPolicyFactory.Create(settings);
            }
            catch (ArgumentException ex)
            {
                return ActionDispatchResult.Rejected(ActionCommitStatus.InvalidSettings, ex.Message);
            }

            var decision = _workflow.TryCommitDrop(
                active,
                selectedActionId,
                authoritative,
                capabilities,
                settings,
                outputPolicy);

            if (!decision.IsAccepted)
            {
                return ActionDispatchResult.Rejected(decision.Status, decision.Reason);
            }

            try
            {
                var jobId = _dispatcher.Dispatch(decision);
                return ActionDispatchResult.Accepted(jobId);
            }
            catch (Exception ex)
            {
                return ActionDispatchResult.DispatchFailed(
                    $"Accepted request could not be dispatched: {ex.GetType().FullName} (0x{ex.HResult:X8}).");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EndNativeDragAsync(Guid dragSessionId, bool cancelled, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_active is null || _active.DragSessionId != dragSessionId)
            {
                return;
            }

            _active = null;
            await SafeHideAsync(cancelled ? OverlayHideReason.NativeDragCancelled : OverlayHideReason.NativeDragEnded)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SafeHideAsync(OverlayHideReason reason)
    {
        try
        {
            await _overlay.HideAsync(reason, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Overlay cleanup is fail-open relative to the native drag. Production diagnostics may record this,
            // but it must never resurrect/retain an authorization capability.
        }
    }
}

public enum ActionDispatchStatus
{
    Accepted = 0,
    Rejected,
    DispatchFailed
}

public sealed record ActionDispatchResult
{
    public required ActionDispatchStatus Status { get; init; }
    public ActionCommitStatus? CommitStatus { get; init; }
    public JobId? JobId { get; init; }
    public string? Reason { get; init; }

    public static ActionDispatchResult Accepted(JobId jobId) => new()
    {
        Status = ActionDispatchStatus.Accepted,
        JobId = jobId
    };

    public static ActionDispatchResult Rejected(ActionCommitStatus status, string reason) => new()
    {
        Status = ActionDispatchStatus.Rejected,
        CommitStatus = status,
        Reason = reason
    };

    public static ActionDispatchResult DispatchFailed(string reason) => new()
    {
        Status = ActionDispatchStatus.DispatchFailed,
        Reason = reason
    };
}

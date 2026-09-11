using System.Collections.Frozen;
using System.Collections.ObjectModel;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Orchestration;

/// <summary>
/// Unforgeable (outside this assembly) authorization snapshot created only by DragWorkflowOrchestrator.
/// External adapters may retain/pass it, but cannot manufacture a session that grants actions.
/// </summary>
public sealed class PreparedOverlaySession
{
    internal PreparedOverlaySession(
        Guid dragSessionId,
        PayloadQualificationResult preflightQualification,
        IEnumerable<ActionId> offeredActionIds,
        OverlayModel overlay)
    {
        DragSessionId = dragSessionId;
        PreflightQualification = preflightQualification ?? throw new ArgumentNullException(nameof(preflightQualification));
        var offered = offeredActionIds?.ToArray() ?? throw new ArgumentNullException(nameof(offeredActionIds));
        if (offered.Length == 0 || offered.Length != offered.Distinct().Count())
        {
            throw new ArgumentException("A prepared overlay must contain unique offered actions.", nameof(offeredActionIds));
        }

        if (overlay is null)
        {
            throw new ArgumentNullException(nameof(overlay));
        }

        if (overlay.DragSessionId != dragSessionId)
        {
            throw new ArgumentException("Overlay and authorization session ids must match.", nameof(overlay));
        }

        var overlayActions = overlay.Actions?.ToArray() ?? throw new ArgumentException("Overlay actions are required.", nameof(overlay));
        if (overlayActions.Length != offered.Length
            || overlayActions.Any(action => action is null || !action.IsEnabled)
            || !overlayActions.Select(action => action.ActionId).ToHashSet().SetEquals(offered))
        {
            throw new ArgumentException("Overlay actions must exactly match the enabled offered action set.", nameof(overlay));
        }

        OfferedActionIds = offered.ToFrozenSet();
        Overlay = overlay with { Actions = new ReadOnlyCollection<OverlayActionItem>(overlayActions) };
    }

    public Guid DragSessionId { get; }
    public IReadOnlySet<ActionId> OfferedActionIds { get; }
    public OverlayModel Overlay { get; }

    // Deliberately hidden from UI/interop callers. This is authorization evidence, not presentation state.
    internal PayloadQualificationResult PreflightQualification { get; }
}

public enum OverlayPreparationStatus
{
    Prepared = 0,
    Suppressed
}

public sealed class OverlayPreparationResult
{
    internal OverlayPreparationResult(OverlayPreparationStatus status, string reason, PreparedOverlaySession? session = null)
    {
        Status = status;
        Reason = reason;
        Session = session;
    }

    public OverlayPreparationStatus Status { get; }
    public string Reason { get; }
    public PreparedOverlaySession? Session { get; }
    public bool IsPrepared => Status == OverlayPreparationStatus.Prepared && Session is not null;
}

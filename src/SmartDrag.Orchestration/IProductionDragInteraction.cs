using SmartDrag.Core.Actions;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;

namespace SmartDrag.Orchestration;

/// <summary>
/// Narrow application-facing drag surface. It intentionally has no OutputPolicy parameter: production MVP callers
/// cannot smuggle destructive output semantics across the native/UI boundary.
/// </summary>
public interface IProductionDragInteraction
{
    Guid? ActiveDragSessionId { get; }

    Task<OverlayPreparationResult> TryPresentAsync(
        Guid dragSessionId,
        PayloadQualificationResult preflight,
        AppCapabilities capabilities,
        UserSettings settings,
        OverlayPlacement placement,
        CancellationToken cancellationToken);

    Task<ActionDispatchResult> TryCommitMvpAsync(
        Guid dragSessionId,
        ActionId selectedActionId,
        PayloadQualificationResult authoritative,
        AppCapabilities capabilities,
        UserSettings settings,
        CancellationToken cancellationToken);

    Task EndNativeDragAsync(Guid dragSessionId, bool cancelled, CancellationToken cancellationToken);
}

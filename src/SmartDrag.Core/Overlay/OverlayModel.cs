using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Overlay;

public sealed record OverlayModel
{
    public required Guid DragSessionId { get; init; }
    public required string PayloadLabel { get; init; }
    public required IReadOnlyList<OverlayActionItem> Actions { get; init; }
}

public sealed record OverlayActionItem
{
    public required ActionId ActionId { get; init; }
    public required string Label { get; init; }
    public required string IconId { get; init; }
    public bool IsEnabled { get; init; }
}

public readonly record struct OverlayPlacement(double X, double Y, double Width, double Height);

public enum OverlayHideReason
{
    NativeDragEnded = 0,
    NativeDragCancelled,
    ActionCommitted,
    Suppressed,
    Error
}

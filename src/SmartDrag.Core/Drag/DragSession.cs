using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Drag;

public sealed record DragSession
{
    public required Guid Id { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required PointD StartCursorPosition { get; init; }
    public required PointD CurrentCursorPosition { get; init; }
    public required DragSessionState State { get; init; }
    public DragPayloadInfo? Payload { get; init; }
    public bool OverlayWasShown { get; init; }
    public ActionId? SelectedActionId { get; init; }

    public static DragSession Create(DateTimeOffset startedAt, PointD cursorPosition) => new()
    {
        Id = Guid.NewGuid(),
        StartedAt = startedAt,
        StartCursorPosition = cursorPosition,
        CurrentCursorPosition = cursorPosition,
        State = DragSessionState.Candidate
    };
}

namespace SmartDrag.Core.Drag;

public sealed class InvalidDragTransitionException : InvalidOperationException
{
    public InvalidDragTransitionException(DragSessionState state, DragSessionEvent @event)
        : base($"Drag transition '{@event}' is invalid from state '{state}'.")
    {
        State = state;
        Event = @event;
    }

    public DragSessionState State { get; }
    public DragSessionEvent Event { get; }
}

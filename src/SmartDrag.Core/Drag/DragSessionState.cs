namespace SmartDrag.Core.Drag;

public enum DragSessionState
{
    Idle = 0,
    Candidate,
    Inspecting,
    Qualified,
    OverlayVisible,
    ActionCommitted,
    Executing,
    Cancelling,
    Completed,
    Failed,
    Cancelled,
    Suppressed,
    Dismissed
}

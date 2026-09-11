namespace SmartDrag.Core.Drag;

public static class DragSessionReducer
{
    public static DragSessionState Reduce(DragSessionState state, DragSessionEvent @event) =>
        (state, @event) switch
        {
            (DragSessionState.Idle, DragSessionEvent.ProbableDragDetected) => DragSessionState.Candidate,

            (DragSessionState.Candidate, DragSessionEvent.QualificationThresholdReached) => DragSessionState.Inspecting,
            (DragSessionState.Candidate, DragSessionEvent.NativeDragEnded) => DragSessionState.Cancelled,
            (DragSessionState.Candidate, DragSessionEvent.NativeDragCancelled) => DragSessionState.Cancelled,

            (DragSessionState.Inspecting, DragSessionEvent.PayloadSupported) => DragSessionState.Qualified,
            (DragSessionState.Inspecting, DragSessionEvent.PayloadRejected) => DragSessionState.Suppressed,
            (DragSessionState.Inspecting, DragSessionEvent.NativeDragEnded) => DragSessionState.Cancelled,
            (DragSessionState.Inspecting, DragSessionEvent.NativeDragCancelled) => DragSessionState.Cancelled,

            (DragSessionState.Qualified, DragSessionEvent.OverlayShown) => DragSessionState.OverlayVisible,
            (DragSessionState.Qualified, DragSessionEvent.NativeDragEnded) => DragSessionState.Dismissed,
            (DragSessionState.Qualified, DragSessionEvent.NativeDragCancelled) => DragSessionState.Dismissed,

            (DragSessionState.OverlayVisible, DragSessionEvent.ActionSelected) => DragSessionState.ActionCommitted,
            (DragSessionState.OverlayVisible, DragSessionEvent.NativeDragEnded) => DragSessionState.Dismissed,
            (DragSessionState.OverlayVisible, DragSessionEvent.NativeDragCancelled) => DragSessionState.Dismissed,

            (DragSessionState.ActionCommitted, DragSessionEvent.ExecutionStarted) => DragSessionState.Executing,
            (DragSessionState.ActionCommitted, DragSessionEvent.NativeDragCancelled) => DragSessionState.Cancelled,

            (DragSessionState.Executing, DragSessionEvent.ExecutionSucceeded) => DragSessionState.Completed,
            (DragSessionState.Executing, DragSessionEvent.ExecutionFailed) => DragSessionState.Failed,
            (DragSessionState.Executing, DragSessionEvent.ExecutionCancelled) => DragSessionState.Cancelled,
            (DragSessionState.Executing, DragSessionEvent.CancellationRequested) => DragSessionState.Cancelling,
            (DragSessionState.Cancelling, DragSessionEvent.ExecutionCancelled) => DragSessionState.Cancelled,
            (DragSessionState.Cancelling, DragSessionEvent.ExecutionFailed) => DragSessionState.Failed,

            _ => throw new InvalidDragTransitionException(state, @event)
        };
}

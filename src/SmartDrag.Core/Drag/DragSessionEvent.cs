namespace SmartDrag.Core.Drag;

public enum DragSessionEvent
{
    ProbableDragDetected = 0,
    QualificationThresholdReached,
    PayloadSupported,
    PayloadRejected,
    OverlayShown,
    NativeDragEnded,
    NativeDragCancelled,
    ActionSelected,
    ExecutionStarted,
    ExecutionSucceeded,
    ExecutionFailed,
    CancellationRequested,
    ExecutionCancelled
}

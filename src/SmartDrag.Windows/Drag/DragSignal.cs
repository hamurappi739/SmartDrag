namespace SmartDrag.Windows.Drag;

public enum DragSignalKind
{
    Started = 0,
    Cancelled,
    Completed
}

public sealed record DragSignal(
    DragSignalKind Kind,
    nint SourceHwnd,
    int ObjectId,
    int ChildId,
    uint SourceThreadId,
    uint EventTimeMs,
    DateTimeOffset ObservedAt);

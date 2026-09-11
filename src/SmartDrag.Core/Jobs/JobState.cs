namespace SmartDrag.Core.Jobs;

public enum JobState
{
    Queued = 0,
    Running,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}

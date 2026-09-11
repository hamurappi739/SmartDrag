using SmartDrag.Core.Completion;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Presentation;

public enum OperationPresentationState
{
    Queued = 0,
    Running,
    Cancelling
}

public enum PresentationTone
{
    Neutral = 0,
    Success,
    Warning,
    Error
}

public sealed record OperationPresentationModel
{
    public required JobId JobId { get; init; }
    public required ActionId ActionId { get; init; }
    public required string ActionLabel { get; init; }
    public required string SourceDisplayName { get; init; }
    public required OperationPresentationState State { get; init; }
    public required string StatusText { get; init; }
    public bool CanCancel { get; init; }
    public bool UsesIndeterminateProgress { get; init; } = true;
    public int QueuedBehindCount { get; init; }
}

public sealed record CompletionPresentationModel
{
    public required JobId JobId { get; init; }
    public required PresentationTone Tone { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required IReadOnlyList<CompletionCommand> Commands { get; init; }
    public bool IsCommandInFlight { get; init; }
    public string? CommandError { get; init; }
    public bool OutputWasDeleted { get; init; }
}

public sealed record SmartDragPresentationSnapshot
{
    public OperationPresentationModel? Operation { get; init; }
    public CompletionPresentationModel? Completion { get; init; }
    public int NonTerminalJobCount { get; init; }
    public int PendingCompletionCount { get; init; }

    public bool IsQuiet => Operation is null && Completion is null;

    public static SmartDragPresentationSnapshot Quiet { get; } = new();
}

public sealed class PresentationSnapshotChangedEventArgs(SmartDragPresentationSnapshot snapshot) : EventArgs
{
    public SmartDragPresentationSnapshot Snapshot { get; } = snapshot;
}

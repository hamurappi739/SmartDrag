using SmartDrag.Core.Actions;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Jobs;

public sealed record JobSnapshot
{
    public required JobId Id { get; init; }
    public required Guid RequestId { get; init; }
    public required ActionId ActionId { get; init; }
    public required JobState State { get; init; }
    public required DateTimeOffset EnqueuedAt { get; init; }
    public IReadOnlyList<string> InputPaths { get; init; } = Array.Empty<string>();
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public IReadOnlyList<string> OutputPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GeneratedArtifact> GeneratedArtifacts { get; init; } = Array.Empty<GeneratedArtifact>();
    public ActionMetrics? Metrics { get; init; }
    public AppError? Error { get; init; }

    public bool IsTerminal => State is JobState.Completed or JobState.Failed or JobState.Cancelled;
}

public sealed class JobChangedEventArgs(JobSnapshot snapshot) : EventArgs
{
    public JobSnapshot Snapshot { get; } = snapshot;
}

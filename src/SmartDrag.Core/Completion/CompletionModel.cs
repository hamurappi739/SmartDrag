using SmartDrag.Core.Errors;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Completion;

public enum CompletionStatus
{
    Completed = 0,
    Failed,
    Cancelled
}

public enum CompletionCommand
{
    OpenContainingFolder = 0,
    CopyResultPath,
    StartResultDrag,
    DeleteGeneratedOutput,
    Dismiss
}

public sealed record CompletionModel
{
    public required JobId JobId { get; init; }
    public required CompletionStatus Status { get; init; }
    public required ActionId ActionId { get; init; }
    public string? SourcePath { get; init; }
    public string? OutputPath { get; init; }
    public long? SourceSizeBytes { get; init; }
    public long? OutputSizeBytes { get; init; }
    public AppError? Error { get; init; }
    public required IReadOnlyList<CompletionCommand> Commands { get; init; }

    public long? BytesSaved => SourceSizeBytes is { } source && OutputSizeBytes is { } output
        ? source - output
        : null;
}

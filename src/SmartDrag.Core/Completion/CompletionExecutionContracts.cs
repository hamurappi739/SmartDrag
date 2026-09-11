using SmartDrag.Core.Errors;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Completion;

/// <summary>
/// Runtime capability snapshot. Commands must only be offered when the production composition can execute
/// them safely. Dismiss is always available and is intentionally not represented as a capability flag.
/// </summary>
public sealed record CompletionCapabilities
{
    public bool CanOpenContainingFolder { get; init; }
    public bool CanCopyResultPath { get; init; }
    public bool CanStartResultDrag { get; init; }
    public bool CanDeleteGeneratedOutput { get; init; }

    public static CompletionCapabilities None { get; } = new();
}

public enum CompletionCommandExecutionStatus
{
    Executed = 0,
    Unsupported,
    JobNotFound,
    JobNotTerminal,
    CommandNotOffered,
    InvalidOutput,
    Failed
}

public sealed record CompletionCommandExecutionResult
{
    public required CompletionCommandExecutionStatus Status { get; init; }
    public AppError? Error { get; init; }
    public bool Success => Status == CompletionCommandExecutionStatus.Executed;

    public static CompletionCommandExecutionResult Executed() => new()
    {
        Status = CompletionCommandExecutionStatus.Executed
    };
}

public interface ICompletionCommandExecutor
{
    CompletionCapabilities Capabilities { get; }

    Task<CompletionCommandExecutionResult> ExecuteAsync(
        JobId jobId,
        CompletionCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// OS/UI boundary for non-destructive completion actions. Generated-output deletion is deliberately NOT
/// part of this interface: it requires a stronger artifact-identity design and remains disabled in v0.7.
/// </summary>
public interface ICompletionPlatformService
{
    CompletionCapabilities Capabilities { get; }
    Task<AppError?> OpenContainingFolderAsync(string path, CancellationToken cancellationToken);
    Task<AppError?> CopyTextAsync(string text, CancellationToken cancellationToken);
    Task<AppError?> StartResultDragAsync(string path, CancellationToken cancellationToken);
}

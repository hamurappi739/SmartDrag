using System.Collections.Concurrent;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Runtime;

/// <summary>
/// Executes only commands that CompletionProjector would offer for the current terminal job and current
/// runtime capabilities. Generated-output deletion is enabled only when an identity-safe deletion service is
/// composed and the terminal job carries exactly one strong identity for exactly one output.
/// </summary>
public sealed class CompletionCommandExecutor : ICompletionCommandExecutor
{
    private readonly IJobQueue _jobQueue;
    private readonly ICompletionPlatformService _platform;
    private readonly IGeneratedArtifactDeletionService? _deletionService;
    private readonly ConcurrentDictionary<JobId, byte> _deleteAttempts = new();

    public CompletionCommandExecutor(
        IJobQueue jobQueue,
        ICompletionPlatformService platform,
        IGeneratedArtifactDeletionService? deletionService = null)
    {
        _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _deletionService = deletionService;

        Capabilities = platform.Capabilities with
        {
            CanDeleteGeneratedOutput = deletionService?.IsSupported == true
        };
    }

    public CompletionCapabilities Capabilities { get; }

    public async Task<CompletionCommandExecutionResult> ExecuteAsync(
        JobId jobId,
        CompletionCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(jobId, command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result(
                CompletionCommandExecutionStatus.Failed,
                ErrorCode.CompletionCommandFailed,
                $"Completion command adapter failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "The completion action could not be completed safely.");
        }
    }

    private async Task<CompletionCommandExecutionResult> ExecuteCoreAsync(
        JobId jobId,
        CompletionCommand command,
        CancellationToken cancellationToken)
    {
        if (!_jobQueue.TryGetSnapshot(jobId, out var snapshot))
        {
            return Result(CompletionCommandExecutionStatus.JobNotFound);
        }

        if (!snapshot.IsTerminal)
        {
            return Result(CompletionCommandExecutionStatus.JobNotTerminal);
        }

        var model = CompletionProjector.FromTerminalJob(snapshot, Capabilities);
        if (!model.Commands.Contains(command))
        {
            return Result(
                CompletionCommandExecutionStatus.CommandNotOffered,
                ErrorCode.CompletionCommandUnavailable,
                $"Completion command '{command}' was not offered for job '{jobId}'.",
                "That completion action is not available.");
        }

        if (command == CompletionCommand.Dismiss)
        {
            return CompletionCommandExecutionResult.Executed();
        }

        if (string.IsNullOrWhiteSpace(model.OutputPath))
        {
            return Result(
                CompletionCommandExecutionStatus.InvalidOutput,
                ErrorCode.InvalidInput,
                "Completion command requires exactly one output path.",
                "The generated file is unavailable.");
        }

        if (command == CompletionCommand.DeleteGeneratedOutput)
        {
            if (_deletionService is null || !CompletionProjector.TryGetIdentityBackedSingleArtifact(snapshot, out var artifact))
            {
                return Result(
                    CompletionCommandExecutionStatus.CommandNotOffered,
                    ErrorCode.CompletionCommandUnavailable,
                    "Generated-output deletion requires a composed identity-safe deletion service and one identity-backed artifact.",
                    "Deleting this generated file is not available.");
            }

            if (!_deleteAttempts.TryAdd(jobId, 0))
            {
                return Result(
                    CompletionCommandExecutionStatus.CommandNotOffered,
                    ErrorCode.CompletionCommandUnavailable,
                    $"Generated-output deletion was already attempted for job '{jobId}'.",
                    "That generated-file delete action has already been used.");
            }

            GeneratedArtifactDeletionResult deletion;
            try
            {
                deletion = await _deletionService
                    .DeleteIfIdentityMatchesAsync(artifact, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _deleteAttempts.TryRemove(jobId, out _);
                throw;
            }
            catch
            {
                _deleteAttempts.TryRemove(jobId, out _);
                throw;
            }

            // A generic native/access failure may be transient, so permit a later retry. Identity mismatch, missing
            // object, unsupported identity, or success are terminal for this completion's destructive capability.
            if (deletion.Status == GeneratedArtifactDeletionStatus.Failed)
            {
                _deleteAttempts.TryRemove(jobId, out _);
            }

            return deletion.Success
                ? CompletionCommandExecutionResult.Executed()
                : new CompletionCommandExecutionResult
                {
                    Status = CompletionCommandExecutionStatus.Failed,
                    Error = deletion.Error ?? new AppError(
                        ErrorCode.GeneratedArtifactDeleteFailed,
                        $"Generated-artifact deletion failed with status '{deletion.Status}'.",
                        "SmartDrag could not safely delete the generated file.")
                };
        }

        AppError? error;
        switch (command)
        {
            case CompletionCommand.OpenContainingFolder:
                error = await _platform.OpenContainingFolderAsync(model.OutputPath, cancellationToken).ConfigureAwait(false);
                break;
            case CompletionCommand.CopyResultPath:
                error = await _platform.CopyTextAsync(model.OutputPath, cancellationToken).ConfigureAwait(false);
                break;
            case CompletionCommand.StartResultDrag:
                error = await _platform.StartResultDragAsync(model.OutputPath, cancellationToken).ConfigureAwait(false);
                break;
            default:
                return Result(CompletionCommandExecutionStatus.Unsupported);
        }

        return error is null
            ? CompletionCommandExecutionResult.Executed()
            : new CompletionCommandExecutionResult
            {
                Status = CompletionCommandExecutionStatus.Failed,
                Error = error
            };
    }

    private static CompletionCommandExecutionResult Result(
        CompletionCommandExecutionStatus status,
        ErrorCode? code = null,
        string? technical = null,
        string? user = null) => new()
    {
        Status = status,
        Error = code is null ? null : new AppError(code.Value, technical ?? status.ToString(), user ?? "The completion action failed.")
    };
}

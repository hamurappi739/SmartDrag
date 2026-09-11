using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;

namespace SmartDrag.Runtime;

public static class CompletionProjector
{
    private static readonly IReadOnlyList<CompletionCommand> TerminalFailureCommands =
        Array.AsReadOnly(new[] { CompletionCommand.Dismiss });

    public static CompletionModel FromTerminalJob(JobSnapshot snapshot, CompletionCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(capabilities);
        if (!snapshot.IsTerminal)
        {
            throw new InvalidOperationException("Completion UI may only be projected from a terminal job snapshot.");
        }

        var status = snapshot.State switch
        {
            JobState.Completed => CompletionStatus.Completed,
            JobState.Cancelled => CompletionStatus.Cancelled,
            JobState.Failed => CompletionStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported terminal job state '{snapshot.State}'.")
        };

        return new CompletionModel
        {
            JobId = snapshot.Id,
            Status = status,
            ActionId = snapshot.ActionId,
            SourcePath = snapshot.InputPaths.Count == 1 ? snapshot.InputPaths[0] : null,
            OutputPath = snapshot.OutputPaths.Count == 1 ? snapshot.OutputPaths[0] : null,
            SourceSizeBytes = snapshot.Metrics?.SourceSizeBytes,
            OutputSizeBytes = snapshot.Metrics?.OutputSizeBytes,
            Error = snapshot.Error,
            Commands = status == CompletionStatus.Completed
                ? BuildSuccessCommands(snapshot, capabilities)
                : TerminalFailureCommands
        };
    }

    private static IReadOnlyList<CompletionCommand> BuildSuccessCommands(
        JobSnapshot snapshot,
        CompletionCapabilities capabilities)
    {
        var commands = new List<CompletionCommand>(5);
        var hasSingleOutput = snapshot.OutputPaths.Count == 1 && !string.IsNullOrWhiteSpace(snapshot.OutputPaths[0]);

        if (hasSingleOutput && capabilities.CanOpenContainingFolder)
        {
            commands.Add(CompletionCommand.OpenContainingFolder);
        }

        if (hasSingleOutput && capabilities.CanCopyResultPath)
        {
            commands.Add(CompletionCommand.CopyResultPath);
        }

        if (hasSingleOutput && capabilities.CanStartResultDrag)
        {
            commands.Add(CompletionCommand.StartResultDrag);
        }

        if (hasSingleOutput && capabilities.CanDeleteGeneratedOutput && TryGetIdentityBackedSingleArtifact(snapshot, out _))
        {
            commands.Add(CompletionCommand.DeleteGeneratedOutput);
        }

        commands.Add(CompletionCommand.Dismiss);
        return Array.AsReadOnly(commands.ToArray());
    }

    internal static bool TryGetIdentityBackedSingleArtifact(
        JobSnapshot snapshot,
        out SmartDrag.Core.Artifacts.GeneratedArtifact artifact)
    {
        artifact = null!;
        if (snapshot.OutputPaths.Count != 1 || snapshot.GeneratedArtifacts.Count != 1)
        {
            return false;
        }

        var generated = snapshot.GeneratedArtifacts[0];
        if (!generated.HasStrongIdentity)
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(generated.Path, snapshot.OutputPaths[0], comparison))
        {
            return false;
        }

        artifact = generated;
        return true;
    }
}

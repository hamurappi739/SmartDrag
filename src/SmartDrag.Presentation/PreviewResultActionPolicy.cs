using SmartDrag.Core.Completion;

namespace SmartDrag.Presentation;

public sealed record PreviewResultActionState
{
    public bool ShowActionRow { get; init; }
    public bool ShowCopyPath { get; init; }
    public bool ShowOpenFolder { get; init; }
    public bool ShowDeleteOutput { get; init; }
    public bool ShowDismiss { get; init; }
}

/// <summary>
/// Keeps result-action visibility fail-closed at the presentation boundary. Runtime commands remain the authority;
/// this policy only prevents stale or unsafe controls from being rendered when the generated output is unavailable.
/// </summary>
public static class PreviewResultActionPolicy
{
    public static PreviewResultActionState Resolve(
        IReadOnlyList<CompletionCommand> commands,
        bool outputAvailable,
        bool outputWasDeleted)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var commandSet = commands.ToHashSet();
        var canUseOutput = outputAvailable && !outputWasDeleted;
        var showCopyPath = canUseOutput && commandSet.Contains(CompletionCommand.CopyResultPath);
        var showOpenFolder = canUseOutput && commandSet.Contains(CompletionCommand.OpenContainingFolder);
        var showDeleteOutput = canUseOutput && commandSet.Contains(CompletionCommand.DeleteGeneratedOutput);
        var showDismiss = commandSet.Contains(CompletionCommand.Dismiss);
        return new PreviewResultActionState
        {
            ShowActionRow = showCopyPath || showOpenFolder || showDeleteOutput || showDismiss,
            ShowCopyPath = showCopyPath,
            ShowOpenFolder = showOpenFolder,
            ShowDeleteOutput = showDeleteOutput,
            ShowDismiss = showDismiss
        };
    }
}

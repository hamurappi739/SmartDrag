using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;

namespace SmartDrag.Presentation;

/// <summary>
/// Toolkit-neutral MVP projection rules. The MVP intentionally does not fabricate a numeric percentage when the
/// codec cannot report trustworthy progress; running work is represented as indeterminate progress instead.
/// </summary>
public static class MvpPresentationPolicy
{
    public static OperationPresentationModel? ProjectOperation(
        IReadOnlyList<JobSnapshot> snapshots,
        Func<SmartDrag.Core.Primitives.ActionId, string> actionLabelResolver)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(actionLabelResolver);

        var nonTerminal = snapshots
            .Where(snapshot => !snapshot.IsTerminal)
            .OrderBy(snapshot => snapshot.EnqueuedAt)
            .ThenBy(snapshot => snapshot.Id.Value)
            .ToArray();

        if (nonTerminal.Length == 0)
        {
            return null;
        }

        var visible = nonTerminal[0];
        var state = visible.State switch
        {
            JobState.Queued => OperationPresentationState.Queued,
            JobState.Running => OperationPresentationState.Running,
            JobState.Cancelling => OperationPresentationState.Cancelling,
            _ => throw new InvalidOperationException($"Unexpected non-terminal state '{visible.State}'.")
        };

        return new OperationPresentationModel
        {
            JobId = visible.Id,
            ActionId = visible.ActionId,
            ActionLabel = SafeLabel(actionLabelResolver, visible.ActionId),
            SourceDisplayName = SafeFileName(visible.InputPaths.Count == 1 ? visible.InputPaths[0] : null),
            State = state,
            StatusText = state switch
            {
                OperationPresentationState.Queued => "Waiting…",
                OperationPresentationState.Running => "Working…",
                OperationPresentationState.Cancelling => "Cancelling…",
                _ => "Working…"
            },
            CanCancel = state is OperationPresentationState.Queued or OperationPresentationState.Running,
            UsesIndeterminateProgress = true,
            QueuedBehindCount = Math.Max(0, nonTerminal.Length - 1)
        };
    }

    public static CompletionPresentationModel ProjectCompletion(CompletionModel completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        var title = completion.Status switch
        {
            CompletionStatus.Completed => SuccessTitle(completion),
            CompletionStatus.Cancelled => "Cancelled",
            CompletionStatus.Failed => "Couldn’t finish",
            _ => "Finished"
        };

        var detail = completion.Status switch
        {
            CompletionStatus.Completed => SuccessDetail(completion),
            CompletionStatus.Cancelled => "The original file was not changed.",
            CompletionStatus.Failed => SafeUserError(completion),
            _ => string.Empty
        };

        return new CompletionPresentationModel
        {
            JobId = completion.JobId,
            Tone = completion.Status switch
            {
                CompletionStatus.Completed => PresentationTone.Success,
                CompletionStatus.Cancelled => PresentationTone.Neutral,
                CompletionStatus.Failed => PresentationTone.Error,
                _ => PresentationTone.Neutral
            },
            Title = title,
            Detail = detail,
            Commands = Array.AsReadOnly(completion.Commands.ToArray())
        };
    }

    private static string SuccessTitle(CompletionModel completion) => completion.ActionId.Value switch
    {
        "image.compress" => "Compressed",
        "image.convert.webp" => "Converted to WebP",
        "image.remove-metadata" => "Metadata removed",
        _ => "Done"
    };

    private static string SuccessDetail(CompletionModel completion)
    {
        if (completion.SourceSizeBytes is { } source && completion.OutputSizeBytes is { } output && source > 0)
        {
            var saved = source - output;
            if (saved > 0)
            {
                var percent = Math.Clamp((double)saved / source * 100d, 0d, 100d);
                return $"Saved {FormatBytes(saved)} ({percent:0.#}%).";
            }
        }

        return "A new file was created. The original was kept.";
    }

    private static string SafeUserError(CompletionModel completion) =>
        string.IsNullOrWhiteSpace(completion.Error?.UserMessage)
            ? "The operation failed. The original file was not changed."
            : completion.Error.UserMessage;

    private static string SafeLabel(Func<SmartDrag.Core.Primitives.ActionId, string> resolver, SmartDrag.Core.Primitives.ActionId actionId)
    {
        try
        {
            var value = resolver(actionId);
            return string.IsNullOrWhiteSpace(value) ? "SmartDrag action" : value;
        }
        catch
        {
            return "SmartDrag action";
        }
    }

    private static string SafeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "file";
        }

        // Do not use platform-specific Path.GetFileName here: presentation tests/tooling may run on a non-Windows
        // host while the payload still contains Windows paths. Treat both separators explicitly so a full private
        // path can never leak merely because the current process uses different path semantics.
        var lastSeparator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        var candidate = lastSeparator >= 0 && lastSeparator + 1 < path.Length
            ? path[(lastSeparator + 1)..]
            : lastSeparator == path.Length - 1
                ? string.Empty
                : path;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "file";
        }

        var sanitized = new string(candidate
            .Take(128)
            .Select(character => char.IsControl(character) ? '�' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var kb = bytes / 1024d;
        if (kb < 1024) return $"{kb:0.#} KB";
        var mb = kb / 1024d;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024d:0.#} GB";
    }
}

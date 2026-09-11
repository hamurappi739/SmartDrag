using SmartDrag.Core.Completion;

namespace SmartDrag.Presentation;

/// <summary>
/// Converts a completion-command failure into a short localized sentence. Runtime errors remain useful for logs and
/// diagnostics, but no adapter-provided text is allowed to cross into the normal Preview result surface.
/// </summary>
public static class PreviewCommandErrorPolicy
{
    public static string? Localize(CompletionCommand? command, string? commandError, bool russian)
    {
        if (string.IsNullOrWhiteSpace(commandError))
        {
            return null;
        }

        return command switch
        {
            CompletionCommand.OpenContainingFolder => russian
                ? "Не удалось открыть папку результата."
                : "Could not open the output folder.",
            CompletionCommand.CopyResultPath => russian
                ? "Не удалось скопировать путь результата."
                : "Could not copy the output path.",
            CompletionCommand.DeleteGeneratedOutput => russian
                ? "Не удалось удалить созданный файл."
                : "Could not delete the generated file.",
            CompletionCommand.StartResultDrag => russian
                ? "Не удалось начать перетаскивание результата."
                : "Could not start dragging the result.",
            _ => russian
                ? "Действие с результатом не выполнено."
                : "The result action could not be completed."
        };
    }
}

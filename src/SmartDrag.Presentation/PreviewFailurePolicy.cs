namespace SmartDrag.Presentation;

/// <summary>
/// User-facing fallback copy for preview failures that originate below the presentation boundary. The original
/// reason remains available to diagnostics, but generic copy prevents paths, enum values, and exception details from
/// leaking through an accidental UI assignment.
/// </summary>
public static class PreviewFailurePolicy
{
    public static string ImageRejected(bool russian) => russian
        ? "Изображение не прошло безопасную проверку. Исходный файл не изменён."
        : "The image did not pass the safety check. The original file was not changed.";

    public static string ActionPanelUnavailable(bool russian) => russian
        ? "Панель действий недоступна. Действие не запущено."
        : "The action panel is unavailable. No action was started.";

    public static string ActionNotStarted(bool russian) => russian
        ? "Действие не запущено. Исходный файл не изменён."
        : "The action was not started. The original file was not changed.";
}

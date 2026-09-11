namespace SmartDrag.Presentation;

/// <summary>
/// Maps persisted history values to a small, known display vocabulary. Local history is user data, so unknown or
/// tampered values must not become arbitrary UI copy merely because they were present in a JSON file.
/// </summary>
public static class PreviewHistoryDisplayPolicy
{
    public static string LocalizeState(string? state, bool russian) => state switch
    {
        "Queued" => russian ? "В очереди" : "Queued",
        "Running" => russian ? "Выполняется" : "Running",
        "Cancelling" => russian ? "Отмена" : "Cancelling",
        "Completed" => russian ? "Готово" : "Completed",
        "Failed" => russian ? "Ошибка" : "Failed",
        "Cancelled" => russian ? "Отменено" : "Cancelled",
        _ => russian ? "Операция" : "Operation"
    };

    public static string LocalizeAction(string? actionId, bool russian) => actionId switch
    {
        "image.compress" => russian ? "Сжать" : "Compress",
        "image.convert.webp" => russian ? "Конвертировать в WebP" : "Convert to WebP",
        "image.remove-metadata" => russian ? "Удалить метаданные" : "Remove Metadata",
        _ => russian ? "Действие SmartDrag" : "SmartDrag action"
    };

    public static string SafeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "file";
        }

        var lastSeparator = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        var name = lastSeparator >= 0 && lastSeparator + 1 < value.Length ? value[(lastSeparator + 1)..] : value;
        var sanitized = new string(name.Take(128).Select(character => char.IsControl(character) ? '�' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
}

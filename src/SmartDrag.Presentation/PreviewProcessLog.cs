using System.Text;

namespace SmartDrag.Presentation;

/// <summary>
/// Bounded, best-effort process log for the safe Preview host. It is intentionally independent from WPF so the
/// process boundary can record lifecycle and exception events without creating a second failure.
/// </summary>
public static class PreviewProcessLog
{
    public const long MaximumLogBytes = 256 * 1024;
    public const int MaximumDetailCharacters = 8 * 1024;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartDrag",
        "preview-errors.log");

    public static bool TryAppend(string? path, string eventName, string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(eventName))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            Directory.CreateDirectory(directory);
            RotateIfNeeded(fullPath);
            var safeEvent = eventName.Trim();
            var safeDetail = string.IsNullOrEmpty(detail)
                ? string.Empty
                : detail.Length <= MaximumDetailCharacters
                    ? detail
                    : detail[..MaximumDetailCharacters] + "…";
            var line = $"[{DateTimeOffset.UtcNow:O}] {safeEvent}\n{safeDetail}\n\n";
            File.AppendAllText(fullPath, line, Encoding.UTF8);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= MaximumLogBytes)
        {
            return;
        }

        try
        {
            File.Move(path, path + ".1", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Rotation is best effort; retaining the current log is safer than failing the process boundary.
        }
    }
}

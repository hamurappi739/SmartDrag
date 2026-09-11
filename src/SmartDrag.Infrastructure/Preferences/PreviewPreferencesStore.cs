using System.Text.Json;
using System.Text;

namespace SmartDrag.Infrastructure.Preferences;

public sealed record PreviewPreferences
{
    public string Language { get; init; } = "ru";
    public bool DarkTheme { get; init; }
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    public double? WindowWidth { get; init; }
    public double? WindowHeight { get; init; }
}

/// <summary>
/// Small, UI-only preference store for the safe preview host. It is deliberately separate from runtime settings:
/// language/theme preferences cannot change payload qualification, output policy, or native activation.
/// </summary>
public sealed class PreviewPreferencesStore
{
    public const int MaximumFileBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string _path;

    public PreviewPreferencesStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A preferences path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
    }

    public static PreviewPreferencesStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartDrag",
        "preview-preferences.json"));

    public PreviewPreferences Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new PreviewPreferences();
            }

            var info = new FileInfo(_path);
            if (!info.Exists || info.Length > MaximumFileBytes)
            {
                return new PreviewPreferences();
            }

            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            var preferences = JsonSerializer.Deserialize<PreviewPreferences>(stream, JsonOptions);
            return Normalize(preferences);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PreviewPreferences();
        }
    }

    public bool TrySave(PreviewPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        string? pending = null;
        try
        {
            var normalized = Normalize(preferences);
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > MaximumFileBytes)
            {
                return false;
            }

            pending = $"{_path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(pending, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
        finally
        {
            if (pending is not null)
            {
                try
                {
                    File.Delete(pending);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A failed cleanup must not turn a best-effort preference write into a crash.
                }
            }
        }
    }

    private static PreviewPreferences Normalize(PreviewPreferences? preferences)
    {
        var language = string.Equals(preferences?.Language, "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "ru";
        return new PreviewPreferences
        {
            Language = language,
            DarkTheme = preferences?.DarkTheme == true,
            WindowLeft = NormalizeCoordinate(preferences?.WindowLeft),
            WindowTop = NormalizeCoordinate(preferences?.WindowTop),
            WindowWidth = NormalizeDimension(preferences?.WindowWidth),
            WindowHeight = NormalizeDimension(preferences?.WindowHeight)
        };
    }

    private static double? NormalizeCoordinate(double? value) =>
        value is { } coordinate && double.IsFinite(coordinate) && Math.Abs(coordinate) <= 100_000
            ? coordinate
            : null;

    private static double? NormalizeDimension(double? value) =>
        value is { } dimension && double.IsFinite(dimension) && dimension is >= 320 and <= 10_000
            ? dimension
            : null;
}

using System.Text.Json;
using System.Text;

namespace SmartDrag.Infrastructure.Preferences;

public sealed record PreviewHistoryEntry
{
    public string Id { get; init; } = string.Empty;
    public string State { get; init; } = "Unknown";
    public string ActionId { get; init; } = "smartdrag.action";
    public string SourceName { get; init; } = "file";
    public string? OutputName { get; init; }
    public bool OutputDeleted { get; init; }
    public bool OutputAvailable { get; init; } = true;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PreviewHistoryState
{
    public IReadOnlyList<PreviewHistoryEntry> Entries { get; init; } = Array.Empty<PreviewHistoryEntry>();
    public DateTimeOffset? ClearedAt { get; init; }
}

/// <summary>
/// Persists the small preview history without retaining private absolute paths. Only sanitized display names,
/// action identifiers, state, and timestamp are written. The file is bounded and replaced atomically.
/// </summary>
public sealed class PreviewHistoryStore
{
    public const int MaximumEntries = 6;
    public const int MaximumFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string _path;

    public PreviewHistoryStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A history path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
    }

    public static PreviewHistoryStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartDrag",
        "preview-history.json"));

    public IReadOnlyList<PreviewHistoryEntry> Load() => LoadState().Entries;

    public PreviewHistoryState LoadState()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new PreviewHistoryState();
            }

            var info = new FileInfo(_path);
            if (!info.Exists || info.Length > MaximumFileBytes)
            {
                return new PreviewHistoryState();
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyEntries = JsonSerializer.Deserialize<List<PreviewHistoryEntry>>(json, JsonOptions);
                return new PreviewHistoryState
                {
                    Entries = Normalize(legacyEntries).ToArray()
                };
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new PreviewHistoryState();
            }

            var state = JsonSerializer.Deserialize<HistoryDocument>(json, JsonOptions);
            return new PreviewHistoryState
            {
                Entries = Normalize(state?.Entries).ToArray(),
                ClearedAt = state?.ClearedAt
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PreviewHistoryState();
        }
    }

    public bool TrySave(IEnumerable<PreviewHistoryEntry> entries)
        => TrySave(entries, LoadState().ClearedAt);

    public bool TrySave(IEnumerable<PreviewHistoryEntry> entries, DateTimeOffset? clearedAt)
    {
        ArgumentNullException.ThrowIfNull(entries);
        string? pending = null;
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            Directory.CreateDirectory(directory);
            var normalized = Normalize(entries).ToArray();
            var document = new HistoryDocument
            {
                Entries = normalized,
                ClearedAt = clearedAt
            };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions));
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

    private sealed record HistoryDocument
    {
        public IReadOnlyList<PreviewHistoryEntry>? Entries { get; init; }
        public DateTimeOffset? ClearedAt { get; init; }
    }

    private static IEnumerable<PreviewHistoryEntry> Normalize(IEnumerable<PreviewHistoryEntry>? entries)
    {
        if (entries is null)
        {
            return Array.Empty<PreviewHistoryEntry>();
        }

        return entries
            .Where(entry => entry is not null)
            .Select(entry => NormalizeEntry(entry!))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.OccurredAt).First())
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(MaximumEntries);
    }

    private static PreviewHistoryEntry NormalizeEntry(PreviewHistoryEntry entry) => entry with
    {
        Id = Limit(entry.Id, 80),
        State = Limit(entry.State, 32),
        ActionId = Limit(entry.ActionId, 80),
        SourceName = SafeName(entry.SourceName),
        OutputName = string.IsNullOrWhiteSpace(entry.OutputName) ? null : SafeName(entry.OutputName),
        OccurredAt = entry.OccurredAt == default ? DateTimeOffset.UtcNow : entry.OccurredAt
    };

    private static string SafeName(string? value)
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

    private static string Limit(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Take(maximum).ToArray());
}

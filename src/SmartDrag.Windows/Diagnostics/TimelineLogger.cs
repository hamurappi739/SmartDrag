using System.IO;
using System.Text.Json;

namespace SmartDrag.Windows.Diagnostics;

/// <summary>
/// Best-effort probe logging. After construction, logging failures are never allowed to break
/// Explorer/OLE drag behavior; failed writes are counted and silently dropped.
/// </summary>
public sealed class TimelineLogger : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private long _droppedEntries;

    public TimelineLogger(string path)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Path = path;
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public string Path { get; }
    public long DroppedEntries => Interlocked.Read(ref _droppedEntries);

    public void Write(string eventName, object? data = null)
    {
        try
        {
            var entry = new
            {
                tsUtc = DateTimeOffset.UtcNow,
                eventName,
                data
            };

            var json = JsonSerializer.Serialize(entry);
            lock (_gate)
            {
                _writer.WriteLine(json);
            }
        }
        catch (Exception)
        {
            // Diagnostics are strictly best-effort. Serialization or I/O failures must never
            // participate in drag/drop behavior or cross a native callback boundary.
            Interlocked.Increment(ref _droppedEntries);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}

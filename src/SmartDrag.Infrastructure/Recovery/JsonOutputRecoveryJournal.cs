using System.Text.Json;
using SmartDrag.Core.Recovery;

namespace SmartDrag.Infrastructure.Recovery;

/// <summary>
/// Local-only durable output-reservation journal. It records only SmartDrag-owned partial paths.
/// Writes use one sibling .pending file plus a monotonically increasing revision; startup/read chooses the
/// newest valid copy and promotes it. This avoids both unbounded write-temp accumulation and silently losing a
/// fully flushed newest journal if the process dies immediately before rename.
/// </summary>
public sealed class JsonOutputRecoveryJournal : IOutputRecoveryJournal, IDisposable
{
    public const int CurrentVersion = 1;
    public const int MaximumEntries = 4096;
    public const long MaximumJournalBytes = 2 * 1024 * 1024;
    public const int MaximumPathCharacters = 32_768;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _journalPath;
    private readonly string _pendingPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public JsonOutputRecoveryJournal(string journalPath)
    {
        if (string.IsNullOrWhiteSpace(journalPath))
        {
            throw new ArgumentException("A recovery journal path is required.", nameof(journalPath));
        }

        _journalPath = Path.GetFullPath(journalPath);
        _pendingPath = _journalPath + ".pending";
    }

    public static string GetDefaultJournalPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            throw new InvalidOperationException("LocalApplicationData is unavailable.");
        }

        return Path.Combine(local, "SmartDrag", "recovery", "output-reservations.v1.json");
    }

    public async Task<IReadOnlyList<OutputRecoveryRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var document = await ReadDocumentUnsafeAsync(cancellationToken).ConfigureAwait(false);
            return document.Entries.OrderBy(record => record.CreatedAt).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(OutputRecoveryRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.ReservationId == Guid.Empty)
        {
            throw new ArgumentException("ReservationId must not be empty.", nameof(record));
        }

        if (string.IsNullOrWhiteSpace(record.TemporaryPath)
            || record.TemporaryPath.Length > MaximumPathCharacters
            || !Path.IsPathFullyQualified(record.TemporaryPath))
        {
            throw new ArgumentException("TemporaryPath must be fully qualified.", nameof(record));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var document = await ReadDocumentUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var entries = document.Entries
                .Where(existing => existing.ReservationId != record.ReservationId)
                .Append(record)
                .OrderBy(existing => existing.CreatedAt)
                .ToList();

            if (entries.Count > MaximumEntries)
            {
                throw new InvalidDataException($"Recovery journal entry limit ({MaximumEntries}) exceeded.");
            }

            await WriteDocumentUnsafeAsync(new JournalDocument
            {
                Version = CurrentVersion,
                Revision = checked(document.Revision + 1),
                Entries = entries
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        if (reservationId == Guid.Empty)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var document = await ReadDocumentUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var entries = document.Entries
                .Where(existing => existing.ReservationId != reservationId)
                .ToList();

            if (entries.Count == document.Entries.Count)
            {
                return;
            }

            await WriteDocumentUnsafeAsync(new JournalDocument
            {
                Version = CurrentVersion,
                Revision = checked(document.Revision + 1),
                Entries = entries
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private async Task<JournalDocument> ReadDocumentUnsafeAsync(CancellationToken cancellationToken)
    {
        var main = await ReadCandidateAsync(_journalPath, cancellationToken).ConfigureAwait(false);
        var pending = await ReadCandidateAsync(_pendingPath, cancellationToken).ConfigureAwait(false);

        if (main.Document is not null && pending.Document is not null &&
            main.Document.Revision == pending.Document.Revision)
        {
            if (!EquivalentEntries(main.Document.Entries, pending.Document.Entries))
            {
                throw new InvalidDataException(
                    "Main and pending recovery journals have the same revision but divergent entries.");
            }

            BestEffortDelete(_pendingPath);
            return main.Document;
        }

        if (pending.Document is not null &&
            (main.Document is null || pending.Document.Revision > main.Document.Revision))
        {
            PromotePendingUnsafe();
            return pending.Document;
        }

        if (main.Document is not null)
        {
            BestEffortDelete(_pendingPath);
            return main.Document;
        }

        if (main.Error is not null)
        {
            throw new InvalidDataException("The main recovery journal is invalid and no valid pending journal is available.", main.Error);
        }

        if (pending.Error is not null)
        {
            throw new InvalidDataException("The pending recovery journal is invalid and no valid main journal is available.", pending.Error);
        }

        return new JournalDocument { Version = CurrentVersion, Revision = 0 };
    }

    private async Task<JournalCandidate> ReadCandidateAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new JournalCandidate(null, null);
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaximumJournalBytes)
            {
                throw new InvalidDataException($"Recovery journal candidate exceeds {MaximumJournalBytes} bytes.");
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var document = await JsonSerializer.DeserializeAsync<JournalDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (document is null || document.Version != CurrentVersion || document.Revision < 0)
            {
                throw new InvalidDataException("Recovery journal candidate is empty, invalid, or has an unsupported version.");
            }

            ValidateDocument(document);

            return new JournalCandidate(document, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new JournalCandidate(null, ex);
        }
    }

    private async Task WriteDocumentUnsafeAsync(JournalDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_journalPath)
            ?? throw new InvalidOperationException("Recovery journal path has no parent directory.");
        Directory.CreateDirectory(directory);

        try
        {
            await using (var stream = new FileStream(
                _pendingPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(_pendingPath).Length > MaximumJournalBytes)
            {
                throw new InvalidDataException($"Recovery journal exceeds {MaximumJournalBytes} bytes.");
            }

            PromotePendingUnsafe();
        }
        catch
        {
            // A valid .pending is deliberately retained: a later read can recover/promote it. An invalid partial
            // pending file will be ignored when a valid main journal exists, or will fail closed when no main exists.
            throw;
        }
    }

    private void PromotePendingUnsafe()
    {
        if (!File.Exists(_pendingPath))
        {
            throw new FileNotFoundException("Pending recovery journal disappeared before promotion.", _pendingPath);
        }

        File.Move(_pendingPath, _journalPath, overwrite: true);
    }

    private static bool EquivalentEntries(
        IReadOnlyList<OutputRecoveryRecord> left,
        IReadOnlyList<OutputRecoveryRecord> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void BestEffortDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // App-owned pending-journal cleanup is best effort and never expands to directory scanning.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record JournalCandidate(JournalDocument? Document, Exception? Error);

    private static void ValidateDocument(JournalDocument? document)
    {
        if (document is null || document.Entries is null)
        {
            throw new InvalidDataException("Recovery journal candidate has no entries collection.");
        }

        if (document.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException($"Recovery journal entry limit ({MaximumEntries}) exceeded.");
        }

        var ids = new HashSet<Guid>();
        foreach (var entry in document.Entries)
        {
            if (entry is null || entry.ReservationId == Guid.Empty || !ids.Add(entry.ReservationId))
            {
                throw new InvalidDataException("Recovery journal contains an empty or duplicate reservation id.");
            }

            if (string.IsNullOrWhiteSpace(entry.TemporaryPath)
                || entry.TemporaryPath.Length > MaximumPathCharacters
                || !Path.IsPathFullyQualified(entry.TemporaryPath))
            {
                throw new InvalidDataException("Recovery journal contains an invalid temporary path.");
            }

            if (entry.CreatedAt == default)
            {
                throw new InvalidDataException("Recovery journal contains an entry without a creation timestamp.");
            }
        }
    }

    private sealed record JournalDocument
    {
        public int Version { get; init; } = CurrentVersion;
        public long Revision { get; init; }
        public List<OutputRecoveryRecord> Entries { get; init; } = new();
    }
}

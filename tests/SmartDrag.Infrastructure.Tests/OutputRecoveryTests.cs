using SmartDrag.Core.Recovery;
using SmartDrag.Infrastructure.Recovery;
using Xunit;

namespace SmartDrag.Infrastructure.Tests;

public sealed class OutputRecoveryTests
{
    [Fact]
    public async Task JsonJournal_RoundTripsAndRemovesRecords()
    {
        using var directory = new TemporaryDirectory();
        using var journal = new JsonOutputRecoveryJournal(Path.Combine(directory.Path, "recovery.json"));
        var id = Guid.NewGuid();
        var path = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png");

        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = path,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var read = await journal.ReadAllAsync(CancellationToken.None);
        Assert.Single(read);
        Assert.Equal(id, read[0].ReservationId);

        await journal.RemoveAsync(id, CancellationToken.None);
        Assert.Empty(await journal.ReadAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task JsonJournal_PromotesValidPendingCopyWhenMainIsMissing()
    {
        using var directory = new TemporaryDirectory();
        var journalPath = Path.Combine(directory.Path, "recovery.json");
        using var journal = new JsonOutputRecoveryJournal(journalPath);
        var id = Guid.NewGuid();
        var partial = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png");
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = partial,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        File.Move(journalPath, journalPath + ".pending", overwrite: true);
        Assert.False(File.Exists(journalPath));

        var records = await journal.ReadAllAsync(CancellationToken.None);

        Assert.Single(records);
        Assert.Equal(id, records[0].ReservationId);
        Assert.True(File.Exists(journalPath));
        Assert.False(File.Exists(journalPath + ".pending"));
    }

    [Fact]
    public async Task JsonJournal_DivergentCopiesAtSameRevisionFailClosed()
    {
        using var directory = new TemporaryDirectory();
        var journalPath = Path.Combine(directory.Path, "recovery.json");
        using var journal = new JsonOutputRecoveryJournal(journalPath);
        var id = Guid.NewGuid();
        var originalPath = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png");
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = originalPath,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var json = await File.ReadAllTextAsync(journalPath);
        await File.WriteAllTextAsync(
            journalPath + ".pending",
            json.Replace(".photo.smartdrag-", ".different.smartdrag-", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => journal.ReadAllAsync(CancellationToken.None));
        Assert.True(File.Exists(journalPath));
        Assert.True(File.Exists(journalPath + ".pending"));
    }

    [Fact]
    public async Task JsonJournal_RecoversValidPendingWhenMainIsCorrupt()
    {
        using var directory = new TemporaryDirectory();
        var journalPath = Path.Combine(directory.Path, "recovery.json");
        using var journal = new JsonOutputRecoveryJournal(journalPath);
        var id = Guid.NewGuid();
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png"),
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        File.Move(journalPath, journalPath + ".pending", overwrite: true);
        await File.WriteAllTextAsync(journalPath, "corrupt-main");

        var records = await journal.ReadAllAsync(CancellationToken.None);

        Assert.Single(records);
        Assert.Equal(id, records[0].ReservationId);
        Assert.False(File.Exists(journalPath + ".pending"));
    }

    [Fact]
    public async Task JsonJournal_IgnoresInvalidPendingWhenMainIsValid()
    {
        using var directory = new TemporaryDirectory();
        var journalPath = Path.Combine(directory.Path, "recovery.json");
        using var journal = new JsonOutputRecoveryJournal(journalPath);
        var id = Guid.NewGuid();
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png"),
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);
        await File.WriteAllTextAsync(journalPath + ".pending", "not-json");

        var records = await journal.ReadAllAsync(CancellationToken.None);

        Assert.Single(records);
        Assert.Equal(id, records[0].ReservationId);
        Assert.False(File.Exists(journalPath + ".pending"));
    }

    [Fact]
    public async Task JsonJournal_RejectsDuplicateReservationIdsInsteadOfRecoveringAmbiguously()
    {
        using var directory = new TemporaryDirectory();
        var journalPath = Path.Combine(directory.Path, "recovery.json");
        using var journal = new JsonOutputRecoveryJournal(journalPath);
        var id = Guid.NewGuid();
        var first = Path.Combine(directory.Path, $".first.smartdrag-{id:N}.partial.png");
        var second = Path.Combine(directory.Path, $".second.smartdrag-{id:N}.partial.png");
        var json = $$"""
        {
          "version": 1,
          "revision": 4,
          "entries": [
            { "reservationId": "{{id}}", "temporaryPath": "{{first.Replace("\\", "\\\\")}}", "createdAt": "2026-09-04T00:00:00Z" },
            { "reservationId": "{{id}}", "temporaryPath": "{{second.Replace("\\", "\\\\")}}", "createdAt": "2026-09-04T00:01:00Z" }
          ]
        }
        """;
        await File.WriteAllTextAsync(journalPath, json);

        await Assert.ThrowsAsync<InvalidDataException>(() => journal.ReadAllAsync(CancellationToken.None));
        Assert.True(File.Exists(journalPath));
    }

    [Fact]
    public async Task JsonJournal_RejectsEntryWithoutTimestamp()
    {
        using var directory = new TemporaryDirectory();
        var journalPath = Path.Combine(directory.Path, "recovery.json");
        using var journal = new JsonOutputRecoveryJournal(journalPath);
        var id = Guid.NewGuid();
        var partial = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png");
        var json = $$"""
        {
          "version": 1,
          "revision": 1,
          "entries": [
            { "reservationId": "{{id}}", "temporaryPath": "{{partial.Replace("\\", "\\\\")}}", "createdAt": "0001-01-01T00:00:00Z" }
          ]
        }
        """;
        await File.WriteAllTextAsync(journalPath, json);

        await Assert.ThrowsAsync<InvalidDataException>(() => journal.ReadAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task JsonJournal_RejectsOverlongTemporaryPathBeforeWriting()
    {
        using var directory = new TemporaryDirectory();
        using var journal = new JsonOutputRecoveryJournal(Path.Combine(directory.Path, "recovery.json"));
        var overlongPath = "C:\\" + new string('x', JsonOutputRecoveryJournal.MaximumPathCharacters);

        await Assert.ThrowsAsync<ArgumentException>(() => journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = Guid.NewGuid(),
            TemporaryPath = overlongPath,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Recovery_DeletesOnlyJournaledReservationBoundPartial()
    {
        using var directory = new TemporaryDirectory();
        var journal = new InMemoryOutputRecoveryJournal();
        var id = Guid.NewGuid();
        var partial = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png");
        var unrelated = directory.CreateFile(".photo.smartdrag-random.partial.png", "do-not-delete");
        await File.WriteAllTextAsync(partial, "partial");
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = partial,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var report = await new OutputStartupRecoveryService(journal).RecoverAsync(CancellationToken.None);

        Assert.Equal(1, report.DeletedPartials);
        Assert.True(report.IsClean);
        Assert.False(File.Exists(partial));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public async Task Recovery_InvalidJournalPathNeverAuthorizesDeletion()
    {
        using var directory = new TemporaryDirectory();
        var journal = new InMemoryOutputRecoveryJournal();
        var id = Guid.NewGuid();
        var victim = directory.CreateFile("important.png", "keep");
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = victim,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var report = await new OutputStartupRecoveryService(journal).RecoverAsync(CancellationToken.None);

        Assert.Equal(1, report.InvalidEntries);
        Assert.True(File.Exists(victim));
        Assert.Empty(await journal.ReadAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Recovery_MissingPartialOnlyClearsJournalRecord()
    {
        using var directory = new TemporaryDirectory();
        var journal = new InMemoryOutputRecoveryJournal();
        var id = Guid.NewGuid();
        var missing = Path.Combine(directory.Path, $".photo.smartdrag-{id:N}.partial.png");
        await journal.UpsertAsync(new OutputRecoveryRecord
        {
            ReservationId = id,
            TemporaryPath = missing,
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var report = await new OutputStartupRecoveryService(journal).RecoverAsync(CancellationToken.None);

        Assert.Equal(1, report.MissingPartials);
        Assert.True(report.IsClean);
        Assert.Empty(await journal.ReadAllAsync(CancellationToken.None));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmartDrag.Recovery.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateFile(string name, string contents)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}

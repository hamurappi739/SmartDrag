using SmartDrag.Infrastructure.Preferences;
using Xunit;

namespace SmartDrag.Infrastructure.Tests;

public sealed class PreviewHistoryStoreTests
{
    [Fact]
    public void MissingFile_ReturnsEmptyHistory()
    {
        var store = new PreviewHistoryStore(TestPath());

        Assert.Empty(store.Load());
    }

    [Fact]
    public void SaveAndLoad_BoundsEntriesAndStripsAbsolutePaths()
    {
        var path = TestPath();
        var store = new PreviewHistoryStore(path);
        var entries = Enumerable.Range(0, 8).Select(index => new PreviewHistoryEntry
        {
            Id = index.ToString(),
            State = "Completed",
            ActionId = "image.compress",
            SourceName = $@"C:\Private\Folder\image-{index}.png",
            OutputName = $@"C:\Private\Folder\output-{index}.jpg",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-index)
        });

        Assert.True(store.TrySave(entries));
        var loaded = store.Load();

        Assert.Equal(PreviewHistoryStore.MaximumEntries, loaded.Count);
        Assert.DoesNotContain(loaded, entry => entry.SourceName.Contains("Private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(File.ReadAllText(path), "Private", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("image-0.png", loaded[0].SourceName);
    }

    [Fact]
    public void CorruptJson_FailsClosedToEmptyHistory()
    {
        var path = TestPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not-json");

        Assert.Empty(new PreviewHistoryStore(path).Load());
    }

    [Fact]
    public void OversizedFile_FailsClosedToEmptyHistory()
    {
        var path = TestPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string('x', PreviewHistoryStore.MaximumFileBytes + 1));

        Assert.Empty(new PreviewHistoryStore(path).Load());
    }

    [Fact]
    public void DuplicateIds_AreCollapsedToNewestEntry()
    {
        var store = new PreviewHistoryStore(TestPath());
        var now = DateTimeOffset.UtcNow;
        Assert.True(store.TrySave(new[]
        {
            new PreviewHistoryEntry { Id = "same", SourceName = "old.png", OccurredAt = now.AddMinutes(-1) },
            new PreviewHistoryEntry { Id = "same", SourceName = "new.png", OccurredAt = now }
        }));

        var loaded = store.Load();

        var entry = Assert.Single(loaded);
        Assert.Equal("new.png", entry.SourceName);
    }

    [Fact]
    public void DeletedOutputFlag_RoundTrips()
    {
        var store = new PreviewHistoryStore(TestPath());
        Assert.True(store.TrySave(new[]
        {
            new PreviewHistoryEntry
            {
                Id = "deleted",
                State = "Completed",
                SourceName = "source.png",
                OutputName = "output.jpg",
                OutputDeleted = true,
                OutputAvailable = false,
                OccurredAt = DateTimeOffset.UtcNow
            }
        }));

        var entry = Assert.Single(store.Load());

        Assert.True(entry.OutputDeleted);
        Assert.False(entry.OutputAvailable);
        Assert.Equal("output.jpg", entry.OutputName);
    }

    [Fact]
    public void ClearedAt_RoundTripsWithEntries()
    {
        var store = new PreviewHistoryStore(TestPath());
        var clearedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        Assert.True(store.TrySave(new[]
        {
            new PreviewHistoryEntry { Id = "kept", SourceName = "new.png", OccurredAt = clearedAt.AddMinutes(1) }
        }, clearedAt));

        var state = store.LoadState();

        Assert.Equal(clearedAt, state.ClearedAt);
        Assert.Equal("kept", Assert.Single(state.Entries).Id);
    }

    [Fact]
    public void LegacyArrayFormat_LoadsWithoutClearedAt()
    {
        var path = TestPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[{\"id\":\"legacy\",\"sourceName\":\"old.png\"}]");

        var state = new PreviewHistoryStore(path).LoadState();

        Assert.Null(state.ClearedAt);
        Assert.Equal("legacy", Assert.Single(state.Entries).Id);
    }

    [Fact]
    public void LegacySaveOverload_PreservesExistingClearBoundary()
    {
        var store = new PreviewHistoryStore(TestPath());
        var clearedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        Assert.True(store.TrySave(Array.Empty<PreviewHistoryEntry>(), clearedAt));

        Assert.True(store.TrySave(new[]
        {
            new PreviewHistoryEntry { Id = "new", SourceName = "new.png", OccurredAt = DateTimeOffset.UtcNow }
        }));

        var state = store.LoadState();

        Assert.Equal(clearedAt, state.ClearedAt);
        Assert.Equal("new", Assert.Single(state.Entries).Id);
    }

    [Fact]
    public void NullEntries_AreIgnoredDuringNormalization()
    {
        var store = new PreviewHistoryStore(TestPath());

        Assert.True(store.TrySave(new PreviewHistoryEntry[]
        {
            null!,
            new PreviewHistoryEntry { Id = "valid", SourceName = "valid.png" }
        }));

        var loaded = store.Load();

        var entry = Assert.Single(loaded);
        Assert.Equal("valid", entry.Id);
    }

    private static string TestPath() => Path.Combine(
        Path.GetTempPath(),
        "smartdrag-history",
        Guid.NewGuid().ToString("N"),
        "history.json");
}

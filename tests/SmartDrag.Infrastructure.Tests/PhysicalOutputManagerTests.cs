using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Output;
using SmartDrag.Infrastructure;
using SmartDrag.Infrastructure.Recovery;
using Xunit;

namespace SmartDrag.Infrastructure.Tests;

public sealed class PhysicalOutputManagerTests
{

    [Fact]
    public void Output_name_planner_allows_extension_only_conversion()
    {
        var candidate = OutputNamePlanner.BuildCandidate("photo.png", string.Empty, ".webp");

        Assert.EndsWith("photo.webp", candidate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reservation_is_same_directory_and_non_destructive()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-compressed",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Reservation);
        Assert.Equal(Path.Combine(directory.Path, "photo-compressed.png"), result.Reservation.PreferredFinalPath);
        Assert.Equal(directory.Path, Path.GetDirectoryName(result.Reservation.TemporaryPath));
        Assert.NotEqual(source, result.Reservation.PreferredFinalPath);
    }


    [Fact]
    public async Task Empty_suffix_is_allowed_when_extension_changes()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = string.Empty,
            NewExtension = ".webp",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(directory.Path, "photo.webp"), result.Reservation?.PreferredFinalPath);
    }

    [Fact]
    public async Task Reservation_rejects_destructive_policy()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-unsafe",
            Policy = OutputPolicy.SafeMvpDefault with { PreserveSource = false }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidOutputPolicy, result.Error?.Code);
    }

    [Fact]
    public async Task Commit_never_overwrites_collision_created_after_reservation()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var reservationResult = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = string.Empty + "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);

        var reservation = Assert.IsType<OutputReservation>(reservationResult.Reservation);
        await File.WriteAllTextAsync(reservation.TemporaryPath, "generated");
        await File.WriteAllTextAsync(reservation.PreferredFinalPath, "other-process");

        var commit = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.True(commit.Success);
        Assert.NotNull(commit.FinalPath);
        Assert.NotEqual(reservation.PreferredFinalPath, commit.FinalPath);
        Assert.Equal("other-process", await File.ReadAllTextAsync(reservation.PreferredFinalPath));
        Assert.Equal("generated", await File.ReadAllTextAsync(commit.FinalPath));
        Assert.Equal("source", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task Abandon_removes_partial_output()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());
        var reservationResult = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);
        var reservation = Assert.IsType<OutputReservation>(reservationResult.Reservation);
        await File.WriteAllTextAsync(reservation.TemporaryPath, "partial");

        var cleanup = await manager.AbandonAsync(reservation);

        Assert.True(cleanup.Success);
        Assert.False(File.Exists(reservation.TemporaryPath));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task Configured_directory_requires_explicit_path()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault with
            {
                LocationMode = OutputLocationMode.ConfiguredDirectory,
                ConfiguredDirectoryPath = null
            }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidOutputPolicy, result.Error?.Code);
    }

    [Fact]
    public async Task Reservation_IsDurablyTrackedBeforeItIsReturned()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var journal = new InMemoryOutputRecoveryJournal();
        var manager = new PhysicalOutputManager(journal);

        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);

        var reservation = Assert.IsType<OutputReservation>(result.Reservation);
        var records = await journal.ReadAllAsync(CancellationToken.None);
        var record = Assert.Single(records);
        Assert.Equal(reservation.Id, record.ReservationId);
        Assert.Equal(reservation.TemporaryPath, record.TemporaryPath);
        Assert.Contains($".smartdrag-{reservation.Id:N}.partial", Path.GetFileName(reservation.TemporaryPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuccessfulCommit_ClearsRecoveryRecord()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var journal = new InMemoryOutputRecoveryJournal();
        var manager = new PhysicalOutputManager(journal);
        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);
        var reservation = Assert.IsType<OutputReservation>(result.Reservation);
        await File.WriteAllTextAsync(reservation.TemporaryPath, "generated");

        var commit = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.True(commit.Success);
        Assert.Empty(await journal.ReadAllAsync(CancellationToken.None));
        Assert.False(File.Exists(reservation.TemporaryPath));
        Assert.Equal("source", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task SuccessfulCommit_CapturesStrongArtifactIdentityWhenProviderSucceeds()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var journal = new InMemoryOutputRecoveryJournal();
        var identity = new FakeIdentityProvider();
        var manager = new PhysicalOutputManager(journal, identity);
        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);
        var reservation = Assert.IsType<OutputReservation>(result.Reservation);
        await File.WriteAllTextAsync(reservation.TemporaryPath, "generated");

        var commit = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.True(commit.Success);
        var artifact = Assert.IsType<GeneratedArtifact>(commit.Artifact);
        Assert.Equal(commit.FinalPath, artifact.Path);
        Assert.NotNull(artifact.Identity);
        Assert.Equal(artifact.Path, identity.LastPath);
    }

    [Fact]
    public async Task IdentityMismatchAcrossCommit_DoesNotGrantDestructiveArtifactIdentity()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var identity = new FakeIdentityProvider { ChangeIdentityBetweenCalls = true };
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal(), identity);
        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);
        var reservation = Assert.IsType<OutputReservation>(result.Reservation);
        await File.WriteAllTextAsync(reservation.TemporaryPath, "generated");

        var commit = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.True(commit.Success);
        Assert.NotNull(commit.Artifact);
        Assert.Null(commit.Artifact.Identity);
        Assert.Equal(2, identity.Calls);
        Assert.True(File.Exists(commit.FinalPath));
    }

    [Fact]
    public async Task IdentityCaptureFailure_DoesNotRollBackAlreadyCommittedOutput()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(
            new InMemoryOutputRecoveryJournal(),
            new FakeIdentityProvider { Fail = true });
        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);
        var reservation = Assert.IsType<OutputReservation>(result.Reservation);
        await File.WriteAllTextAsync(reservation.TemporaryPath, "generated");

        var commit = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.True(commit.Success);
        Assert.NotNull(commit.Artifact);
        Assert.Null(commit.Artifact.Identity);
        Assert.True(File.Exists(commit.FinalPath));
        Assert.Equal("source", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task JournalFailure_PreventsReservationAndProcessingAuthority()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var manager = new PhysicalOutputManager(new ThrowingJournal());

        var result = await manager.ReserveAsync(new OutputRequest
        {
            SourcePath = source,
            Suffix = "-clean",
            Policy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Reservation);
        Assert.Equal(ErrorCode.RecoveryJournalUnavailable, result.Error?.Code);
        Assert.Equal("source", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task Commit_rejects_temporary_path_not_bound_to_reservation()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var foreignTemporary = directory.CreateFile("foreign.partial", "must remain");
        var finalPath = Path.Combine(directory.Path, "photo-clean.png");
        var reservation = new OutputReservation
        {
            Id = Guid.NewGuid(),
            SourcePath = source,
            TemporaryPath = foreignTemporary,
            CollisionBasePath = finalPath,
            PreferredFinalPath = finalPath,
            Policy = OutputPolicy.SafeMvpDefault
        };
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidOutputPolicy, result.Error?.Code);
        Assert.Equal("must remain", await File.ReadAllTextAsync(foreignTemporary));
        Assert.False(File.Exists(finalPath));
    }

    [Fact]
    public async Task Abandon_rejects_foreign_temporary_path_without_deleting_it()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var foreignTemporary = directory.CreateFile("foreign.partial", "must remain");
        var finalPath = Path.Combine(directory.Path, "photo-clean.png");
        var reservation = new OutputReservation
        {
            Id = Guid.NewGuid(),
            SourcePath = source,
            TemporaryPath = foreignTemporary,
            CollisionBasePath = finalPath,
            PreferredFinalPath = finalPath,
            Policy = OutputPolicy.SafeMvpDefault
        };
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.AbandonAsync(reservation);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidOutputPolicy, result.Error?.Code);
        Assert.True(File.Exists(foreignTemporary));
    }

    [Fact]
    public async Task Commit_rejects_reservation_that_targets_source_path()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CreateFile("photo.png", "source");
        var reservation = new OutputReservation
        {
            Id = Guid.NewGuid(),
            SourcePath = source,
            TemporaryPath = Path.Combine(directory.Path, $".photo.smartdrag-{Guid.NewGuid():N}.partial.png"),
            CollisionBasePath = source,
            PreferredFinalPath = source,
            Policy = OutputPolicy.SafeMvpDefault
        };
        await File.WriteAllTextAsync(reservation.TemporaryPath, "must remain");
        var manager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal());

        var result = await manager.CommitAsync(reservation, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidOutputPolicy, result.Error?.Code);
        Assert.Equal("source", await File.ReadAllTextAsync(source));
        Assert.Equal("must remain", await File.ReadAllTextAsync(reservation.TemporaryPath));
    }

    private sealed class FakeIdentityProvider : IGeneratedArtifactIdentityProvider
    {
        public bool Fail { get; init; }
        public bool ChangeIdentityBetweenCalls { get; init; }
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }

        public Task<ArtifactIdentityCaptureResult> CaptureAsync(string path, CancellationToken cancellationToken)
        {
            LastPath = path;
            Calls++;
            return Task.FromResult(Fail
                ? ArtifactIdentityCaptureResult.Failed(new SmartDrag.Core.Errors.AppError(
                    SmartDrag.Core.Errors.ErrorCode.ArtifactIdentityUnavailable,
                    "test identity failure",
                    "identity unavailable"))
                : ArtifactIdentityCaptureResult.Captured(new ArtifactIdentity
                {
                    Scheme = "test-id-v1",
                    VolumeId = "VOL",
                    ObjectId = ChangeIdentityBetweenCalls ? $"OBJ-{Calls}" : "OBJ"
                }));
        }
    }

    private sealed class ThrowingJournal : SmartDrag.Core.Recovery.IOutputRecoveryJournal
    {
        public Task<IReadOnlyList<SmartDrag.Core.Recovery.OutputRecoveryRecord>> ReadAllAsync(CancellationToken cancellationToken) =>
            throw new IOException("test journal unavailable");

        public Task UpsertAsync(SmartDrag.Core.Recovery.OutputRecoveryRecord record, CancellationToken cancellationToken) =>
            throw new IOException("test journal unavailable");

        public Task RemoveAsync(Guid reservationId, CancellationToken cancellationToken) =>
            throw new IOException("test journal unavailable");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmartDrag.Tests", Guid.NewGuid().ToString("N"));
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
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test cleanup is best effort. The assertion targets product cleanup, not fixture cleanup.
            }
        }
    }
}

using SmartDrag.Core.Actions;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;
using SmartDrag.Runtime;
using Xunit;

namespace SmartDrag.Runtime.Tests;

public sealed class CompletionCommandExecutorTests
{
    [Fact]
    public async Task CopyPath_ExecutesOnlyWhenOffered()
    {
        var snapshot = CompletedJob();
        await using var queue = new SnapshotQueue(snapshot);
        var platform = new FakePlatform
        {
            CapabilitiesValue = new CompletionCapabilities { CanCopyResultPath = true }
        };
        var sut = new CompletionCommandExecutor(queue, platform);

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.CopyResultPath, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(snapshot.OutputPaths[0], platform.CopiedText);
    }

    [Fact]
    public async Task DeleteGeneratedOutput_IsNotOfferedWithoutIdentitySafeDeletionService()
    {
        var snapshot = CompletedJob();
        await using var queue = new SnapshotQueue(snapshot);
        var sut = new CompletionCommandExecutor(queue, new FakePlatform());

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);

        Assert.Equal(CompletionCommandExecutionStatus.CommandNotOffered, result.Status);
        Assert.False(sut.Capabilities.CanDeleteGeneratedOutput);
    }

    [Fact]
    public async Task DeleteGeneratedOutput_UsesIdentitySafeDeletionService()
    {
        var snapshot = CompletedJob();
        await using var queue = new SnapshotQueue(snapshot);
        var deletion = new FakeDeletionService();
        var sut = new CompletionCommandExecutor(queue, new FakePlatform(), deletion);

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(sut.Capabilities.CanDeleteGeneratedOutput);
        Assert.Equal(snapshot.GeneratedArtifacts[0], deletion.DeletedArtifact);
    }

    [Fact]
    public async Task SuccessfulDelete_IsConsumedAndCannotExecuteTwice()
    {
        var snapshot = CompletedJob();
        await using var queue = new SnapshotQueue(snapshot);
        var deletion = new FakeDeletionService();
        var sut = new CompletionCommandExecutor(queue, new FakePlatform(), deletion);

        var first = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);
        var second = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal(CompletionCommandExecutionStatus.CommandNotOffered, second.Status);
        Assert.Equal(1, deletion.Calls);
    }

    [Fact]
    public async Task DeleteGeneratedOutput_IsNotOfferedWhenStrongIdentityIsMissing()
    {
        var snapshot = CompletedJob() with
        {
            GeneratedArtifacts = new[] { new GeneratedArtifact { Path = "result.png" } }
        };
        await using var queue = new SnapshotQueue(snapshot);
        var sut = new CompletionCommandExecutor(queue, new FakePlatform(), new FakeDeletionService());

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);

        Assert.Equal(CompletionCommandExecutionStatus.CommandNotOffered, result.Status);
    }

    [Fact]
    public async Task DeletionIdentityMismatch_IsReportedAsFailure()
    {
        var snapshot = CompletedJob();
        await using var queue = new SnapshotQueue(snapshot);
        var deletion = new FakeDeletionService
        {
            Result = new GeneratedArtifactDeletionResult
            {
                Status = GeneratedArtifactDeletionStatus.IdentityMismatch,
                Error = new AppError(
                    ErrorCode.ArtifactIdentityMismatch,
                    "identity changed",
                    "The file changed and was not deleted.")
            }
        };
        var sut = new CompletionCommandExecutor(queue, new FakePlatform(), deletion);

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);

        Assert.Equal(CompletionCommandExecutionStatus.Failed, result.Status);
        Assert.Equal(ErrorCode.ArtifactIdentityMismatch, result.Error?.Code);
    }

    [Fact]
    public async Task AdapterException_IsContainedAsSafeFailure()
    {
        var snapshot = CompletedJob();
        await using var queue = new SnapshotQueue(snapshot);
        var deletion = new FakeDeletionService { ExceptionToThrow = new InvalidOperationException("private adapter detail") };
        var sut = new CompletionCommandExecutor(queue, new FakePlatform(), deletion);

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.DeleteGeneratedOutput, CancellationToken.None);

        Assert.Equal(CompletionCommandExecutionStatus.Failed, result.Status);
        Assert.Equal(ErrorCode.CompletionCommandFailed, result.Error?.Code);
        Assert.Equal("The completion action could not be completed safely.", result.Error?.UserMessage);
        Assert.DoesNotContain("private", result.Error?.UserMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonTerminalJob_RejectsCompletionCommand()
    {
        var snapshot = CompletedJob() with { State = JobState.Running, OutputPaths = Array.Empty<string>() };
        await using var queue = new SnapshotQueue(snapshot);
        var sut = new CompletionCommandExecutor(queue, new FakePlatform());

        var result = await sut.ExecuteAsync(snapshot.Id, CompletionCommand.Dismiss, CancellationToken.None);

        Assert.Equal(CompletionCommandExecutionStatus.JobNotTerminal, result.Status);
    }

    private static JobSnapshot CompletedJob() => new()
    {
        Id = JobId.New(),
        RequestId = Guid.NewGuid(),
        ActionId = BuiltInActionIds.CompressImage,
        State = JobState.Completed,
        EnqueuedAt = DateTimeOffset.UtcNow,
        InputPaths = new[] { "source.png" },
        OutputPaths = new[] { "result.png" },
        GeneratedArtifacts = new[] { StrongArtifact("result.png") }
    };

    private static GeneratedArtifact StrongArtifact(string path) => new()
    {
        Path = path,
        Identity = new ArtifactIdentity
        {
            Scheme = "test-id-v1",
            VolumeId = "VOL",
            ObjectId = "OBJ"
        }
    };

    private sealed class FakePlatform : ICompletionPlatformService
    {
        public CompletionCapabilities CapabilitiesValue { get; init; } = new() { CanOpenContainingFolder = true };
        public CompletionCapabilities Capabilities => CapabilitiesValue;
        public string? CopiedText { get; private set; }

        public Task<AppError?> OpenContainingFolderAsync(string path, CancellationToken cancellationToken) => Task.FromResult<AppError?>(null);

        public Task<AppError?> CopyTextAsync(string text, CancellationToken cancellationToken)
        {
            CopiedText = text;
            return Task.FromResult<AppError?>(null);
        }

        public Task<AppError?> StartResultDragAsync(string path, CancellationToken cancellationToken) => Task.FromResult<AppError?>(new AppError(
            ErrorCode.CompletionCommandUnavailable,
            "Unavailable in test.",
            "Unavailable."));
    }

    private sealed class FakeDeletionService : IGeneratedArtifactDeletionService
    {
        public bool IsSupported { get; init; } = true;
        public GeneratedArtifact? DeletedArtifact { get; private set; }
        public int Calls { get; private set; }
        public Exception? ExceptionToThrow { get; init; }
        public GeneratedArtifactDeletionResult Result { get; init; } = GeneratedArtifactDeletionResult.Deleted();

        public Task<GeneratedArtifactDeletionResult> DeleteIfIdentityMatchesAsync(
            GeneratedArtifact artifact,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            DeletedArtifact = artifact;
            return Task.FromResult(Result);
        }
    }

    private sealed class SnapshotQueue(params JobSnapshot[] snapshots) : IJobQueue
    {
        private readonly Dictionary<JobId, JobSnapshot> _snapshots = snapshots.ToDictionary(x => x.Id);
        public event EventHandler<JobChangedEventArgs>? JobChanged
        {
            add { }
            remove { }
        }
        public JobId Enqueue(ActionRequest request) => throw new NotSupportedException();
        public bool TryCancel(JobId jobId) => false;
        public bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot) => _snapshots.TryGetValue(jobId, out snapshot!);
        public IReadOnlyList<JobSnapshot> GetSnapshots() => _snapshots.Values.ToArray();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;
using SmartDrag.Presentation;
using SmartDrag.Runtime;
using Xunit;

namespace SmartDrag.Presentation.Tests;

public sealed class MvpPresentationCoordinatorTests
{
    [Fact]
    public async Task VisibleRunningOperation_CanBeCancelledThroughJobQueueOnly()
    {
        var job = Job(JobState.Running);
        await using var queue = new FakeQueue(job);
        using var completion = new CompletionCoordinator(queue, CompletionCapabilities.None);
        using var sut = new MvpPresentationCoordinator(queue, completion, new FakeCommands(), Registry());

        Assert.True(sut.TryCancelVisibleOperation(job.Id));
        Assert.Equal(job.Id, queue.CancelledJobId);
    }

    [Fact]
    public async Task Completion_IsQueuedAndDismissPromotesNext()
    {
        var first = Job(JobState.Completed);
        var second = Job(JobState.Completed);
        await using var queue = new FakeQueue();
        using var completion = new CompletionCoordinator(queue, CompletionCapabilities.None);
        using var sut = new MvpPresentationCoordinator(queue, completion, new FakeCommands(), Registry());

        queue.Publish(first);
        queue.Publish(second);

        Assert.Equal(first.Id, sut.Current.Completion?.JobId);
        Assert.Equal(1, sut.Current.PendingCompletionCount);

        var result = await sut.ExecuteVisibleCompletionCommandAsync(first.Id, CompletionCommand.Dismiss, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(second.Id, sut.Current.Completion?.JobId);
        Assert.Equal(0, sut.Current.PendingCompletionCount);
    }

    [Fact]
    public async Task DuplicateCompletionEvent_DoesNotDuplicateToast()
    {
        var terminal = Job(JobState.Completed);
        await using var queue = new FakeQueue();
        using var completion = new CompletionCoordinator(queue, CompletionCapabilities.None);
        using var sut = new MvpPresentationCoordinator(queue, completion, new FakeCommands(), Registry());

        queue.Publish(terminal);
        queue.Publish(terminal);

        Assert.Equal(terminal.Id, sut.Current.Completion?.JobId);
        Assert.Equal(0, sut.Current.PendingCompletionCount);
    }

    [Fact]
    public async Task CompletionCommandFailure_IsProjectedAsUserSafeInlineError()
    {
        var terminal = Job(JobState.Completed, outputPath: "result.png");
        await using var queue = new FakeQueue();
        var commands = new FakeCommands
        {
            CapabilitiesValue = new CompletionCapabilities { CanOpenContainingFolder = true },
            Result = new CompletionCommandExecutionResult
            {
                Status = CompletionCommandExecutionStatus.Failed,
                Error = new AppError(ErrorCode.CompletionCommandFailed, "secret technical detail", "Could not open the folder.")
            }
        };
        using var completion = new CompletionCoordinator(queue, commands.Capabilities);
        using var sut = new MvpPresentationCoordinator(queue, completion, commands, Registry());
        queue.Publish(terminal);

        var result = await sut.ExecuteVisibleCompletionCommandAsync(
            terminal.Id,
            CompletionCommand.OpenContainingFolder,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Could not open the folder.", sut.Current.Completion?.CommandError);
        Assert.DoesNotContain("secret", sut.Current.Completion?.CommandError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletionCommandException_IsContainedAndClearsInFlightState()
    {
        var terminal = Job(JobState.Completed, outputPath: "result.png");
        await using var queue = new FakeQueue();
        var commands = new FakeCommands
        {
            CapabilitiesValue = new CompletionCapabilities { CanOpenContainingFolder = true },
            ExceptionToThrow = new InvalidOperationException("private adapter detail")
        };
        using var completion = new CompletionCoordinator(queue, commands.Capabilities);
        using var sut = new MvpPresentationCoordinator(queue, completion, commands, Registry());
        queue.Publish(terminal);

        var result = await sut.ExecuteVisibleCompletionCommandAsync(
            terminal.Id,
            CompletionCommand.OpenContainingFolder,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("The completion action could not be completed safely.", sut.Current.Completion?.CommandError);
        Assert.DoesNotContain("private", sut.Current.Completion?.CommandError ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var retry = await sut.ExecuteVisibleCompletionCommandAsync(
            terminal.Id,
            CompletionCommand.OpenContainingFolder,
            CancellationToken.None);

        Assert.False(retry.Success);
        Assert.Equal(2, commands.ExecutionCount);
    }

    private static IActionRegistry Registry() => new ActionRegistry(BuiltInActionDefinitions.Mvp);

    private static JobSnapshot Job(JobState state, string? outputPath = null) => new()
    {
        Id = JobId.New(),
        RequestId = Guid.NewGuid(),
        ActionId = BuiltInActionIds.CompressImage,
        State = state,
        EnqueuedAt = DateTimeOffset.UtcNow,
        InputPaths = new[] { "source.png" },
        OutputPaths = outputPath is null ? Array.Empty<string>() : new[] { outputPath }
    };

    private sealed class FakeCommands : ICompletionCommandExecutor
    {
        public CompletionCapabilities CapabilitiesValue { get; init; } = CompletionCapabilities.None;
        public CompletionCapabilities Capabilities => CapabilitiesValue;
        public CompletionCommandExecutionResult Result { get; init; } = CompletionCommandExecutionResult.Executed();
        public Exception? ExceptionToThrow { get; init; }
        public int ExecutionCount { get; private set; }
        public Task<CompletionCommandExecutionResult> ExecuteAsync(JobId jobId, CompletionCommand command, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeQueue(params JobSnapshot[] initial) : IJobQueue
    {
        private readonly Dictionary<JobId, JobSnapshot> _snapshots = initial.ToDictionary(x => x.Id);
        public event EventHandler<JobChangedEventArgs>? JobChanged;
        public JobId? CancelledJobId { get; private set; }
        public JobId Enqueue(ActionRequest request) => throw new NotSupportedException();
        public bool TryCancel(JobId jobId) { CancelledJobId = jobId; return _snapshots.ContainsKey(jobId); }
        public bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot) => _snapshots.TryGetValue(jobId, out snapshot!);
        public IReadOnlyList<JobSnapshot> GetSnapshots() => _snapshots.Values.OrderBy(x => x.EnqueuedAt).ToArray();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Publish(JobSnapshot snapshot)
        {
            _snapshots[snapshot.Id] = snapshot;
            JobChanged?.Invoke(this, new JobChangedEventArgs(snapshot));
        }
    }
}

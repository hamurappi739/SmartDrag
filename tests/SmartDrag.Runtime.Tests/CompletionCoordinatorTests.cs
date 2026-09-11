using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;
using SmartDrag.Runtime;
using Xunit;

namespace SmartDrag.Runtime.Tests;

public sealed class CompletionCoordinatorTests
{
    [Fact]
    public void RepeatedTerminalNotification_PublishesCompletionOnce()
    {
        var queue = new MutableQueue();
        using var sut = new CompletionCoordinator(queue, CompletionCapabilities.None);
        var count = 0;
        sut.CompletionReady += (_, _) => count++;
        var terminal = new JobSnapshot
        {
            Id = JobId.New(),
            RequestId = Guid.NewGuid(),
            ActionId = BuiltInActionIds.CompressImage,
            State = JobState.Completed,
            EnqueuedAt = DateTimeOffset.UtcNow,
            OutputPaths = new[] { "result.png" }
        };

        queue.Publish(terminal);
        queue.Publish(terminal);

        Assert.Equal(1, count);
        Assert.True(sut.TryGet(terminal.Id, out var model));
        Assert.Equal(CompletionStatus.Completed, model.Status);
    }

    private sealed class MutableQueue : IJobQueue
    {
        private readonly Dictionary<JobId, JobSnapshot> _snapshots = new();
        public event EventHandler<JobChangedEventArgs>? JobChanged;
        public JobId Enqueue(ActionRequest request) => throw new NotSupportedException();
        public bool TryCancel(JobId jobId) => false;
        public bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot) => _snapshots.TryGetValue(jobId, out snapshot!);
        public IReadOnlyList<JobSnapshot> GetSnapshots() => _snapshots.Values.ToArray();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(JobSnapshot snapshot)
        {
            _snapshots[snapshot.Id] = snapshot;
            JobChanged?.Invoke(this, new JobChangedEventArgs(snapshot));
        }
    }
}

using SmartDrag.Core.Actions;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Output;
using SmartDrag.Core.Primitives;
using SmartDrag.Runtime;
using Xunit;

namespace SmartDrag.Runtime.Tests;

public sealed class SequentialJobQueueTests
{
    [Fact]
    public async Task Successful_job_reaches_completed()
    {
        var executor = new RecordingExecutor(async (_, token) =>
        {
            await Task.Delay(10, token);
            return ActionResult.Succeeded("result.webp");
        });

        await using var queue = new SequentialJobQueue(executor);
        var id = queue.Enqueue(Request("image.convert.webp"));

        var snapshot = await WaitForStateAsync(queue, id, JobState.Completed);

        Assert.Single(snapshot.OutputPaths);
        Assert.Equal("result.webp", snapshot.OutputPaths[0]);
        Assert.NotNull(snapshot.StartedAt);
        Assert.NotNull(snapshot.FinishedAt);
    }

    [Fact]
    public async Task Enqueue_snapshots_request_and_publishes_read_only_result_lists()
    {
        var received = new TaskCompletionSource<ActionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var outputs = new List<string> { "result.webp" };
        var executor = new RecordingExecutor((request, _) =>
        {
            received.TrySetResult(request);
            return Task.FromResult(new ActionResult
            {
                Success = true,
                OutputPaths = outputs
            });
        });

        await using var queue = new SequentialJobQueue(executor);
        var inputs = new List<string> { "input.png" };
        var id = queue.Enqueue(Request("image.compress") with { InputPaths = inputs });
        inputs[0] = "tampered.png";

        var captured = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var snapshot = await WaitForStateAsync(queue, id, JobState.Completed);
        outputs[0] = "tampered-result.webp";

        Assert.Equal("input.png", captured.InputPaths[0]);
        Assert.True(Assert.IsAssignableFrom<IList<string>>(captured.InputPaths).IsReadOnly);
        Assert.Equal("result.webp", snapshot.OutputPaths[0]);
        Assert.True(Assert.IsAssignableFrom<IList<string>>(snapshot.OutputPaths).IsReadOnly);
    }


    [Fact]
    public async Task Completed_job_preserves_generated_output_ownership_and_metrics()
    {
        var executor = new RecordingExecutor((_, _) => Task.FromResult(
            ActionResult.SucceededGeneratedWithMetrics(
                new ActionMetrics { SourceSizeBytes = 2_000, OutputSizeBytes = 500 },
                StrongArtifact("result.webp"))));

        await using var queue = new SequentialJobQueue(executor);
        var id = queue.Enqueue(Request("image.convert.webp"));

        var snapshot = await WaitForStateAsync(queue, id, JobState.Completed);

        Assert.Equal(new[] { "result.webp" }, snapshot.GeneratedArtifacts.Select(artifact => artifact.Path));
        Assert.Equal(2_000, snapshot.Metrics?.SourceSizeBytes);
        Assert.Equal(500, snapshot.Metrics?.OutputSizeBytes);
    }

    [Fact]
    public async Task Queue_executes_jobs_sequentially()
    {
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionOrder = new List<string>();
        var gate = new object();

        var executor = new RecordingExecutor(async (request, token) =>
        {
            lock (gate)
            {
                executionOrder.Add(request.ActionId.Value);
            }

            if (request.ActionId.Value == "first")
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.WaitAsync(token);
            }

            return ActionResult.Succeeded();
        });

        await using var queue = new SequentialJobQueue(executor);
        var first = queue.Enqueue(Request("first"));
        var second = queue.Enqueue(Request("second"));

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(queue.TryGetSnapshot(second, out var secondBeforeRelease));
        Assert.Equal(JobState.Queued, secondBeforeRelease.State);

        releaseFirst.TrySetResult();
        await WaitForStateAsync(queue, first, JobState.Completed);
        await WaitForStateAsync(queue, second, JobState.Completed);

        Assert.Equal(new[] { "first", "second" }, executionOrder);
        Assert.Equal(1, executor.MaxConcurrency);
    }


    [Fact]
    public async Task Duplicate_request_id_returns_same_job_and_executes_once()
    {
        var executed = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new RecordingExecutor(async (_, token) =>
        {
            Interlocked.Increment(ref executed);
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return ActionResult.Succeeded();
        });

        await using var queue = new SequentialJobQueue(executor);
        var requestId = Guid.NewGuid();
        var request = Request("image.compress") with { RequestId = requestId };

        var first = queue.Enqueue(request);
        var second = queue.Enqueue(request);

        Assert.Equal(first, second);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, Volatile.Read(ref executed));
        release.TrySetResult();
        await WaitForStateAsync(queue, first, JobState.Completed);
    }


    [Fact]
    public async Task Duplicate_request_id_with_different_action_is_rejected()
    {
        var executor = new RecordingExecutor((_, _) => Task.FromResult(ActionResult.Succeeded()));
        await using var queue = new SequentialJobQueue(executor);
        var requestId = Guid.NewGuid();

        queue.Enqueue(Request("image.compress") with { RequestId = requestId });

        Assert.Throws<InvalidOperationException>(() =>
            queue.Enqueue(Request("image.convert.webp") with { RequestId = requestId }));
    }

    [Fact]
    public async Task Running_job_can_be_cancelled()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new RecordingExecutor(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return ActionResult.Succeeded();
        });

        await using var queue = new SequentialJobQueue(executor);
        var id = queue.Enqueue(Request("image.compress"));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(queue.TryCancel(id));
        var snapshot = await WaitForStateAsync(queue, id, JobState.Cancelled);

        Assert.Equal(ErrorCode.OperationCancelled, snapshot.Error?.Code);
    }

    [Fact]
    public async Task Executor_failure_reaches_failed()
    {
        var executor = new RecordingExecutor((_, _) => Task.FromResult(ActionResult.Failed(new AppError(
            ErrorCode.EncodeFailed,
            "encoder failed",
            "Could not create the output file."))));

        await using var queue = new SequentialJobQueue(executor);
        var id = queue.Enqueue(Request("image.compress"));

        var snapshot = await WaitForStateAsync(queue, id, JobState.Failed);
        Assert.Equal(ErrorCode.EncodeFailed, snapshot.Error?.Code);
    }

    private static ActionRequest Request(string actionId) => new()
    {
        RequestId = Guid.NewGuid(),
        ActionId = new ActionId(actionId),
        InputPaths = new[] { "input.png" },
        OutputPolicy = OutputPolicy.SafeMvpDefault
    };

    private static async Task<JobSnapshot> WaitForStateAsync(
        IJobQueue queue,
        JobId id,
        JobState target,
        int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (queue.TryGetSnapshot(id, out var snapshot) && snapshot.State == target)
            {
                return snapshot;
            }

            await Task.Delay(10);
        }

        queue.TryGetSnapshot(id, out var last);
        throw new TimeoutException($"Job did not reach {target}; last state was {last?.State}.");
    }

    private sealed class RecordingExecutor : IActionExecutor
    {
        private readonly Func<ActionRequest, CancellationToken, Task<ActionResult>> _execute;
        private int _concurrency;
        private int _maxConcurrency;

        public RecordingExecutor(Func<ActionRequest, CancellationToken, Task<ActionResult>> execute)
        {
            _execute = execute;
        }

        public int MaxConcurrency => Volatile.Read(ref _maxConcurrency);

        public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
        {
            var concurrency = Interlocked.Increment(ref _concurrency);
            UpdateMax(concurrency);
            try
            {
                return await _execute(request, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }

        private void UpdateMax(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrency);
                if (value <= current || Interlocked.CompareExchange(ref _maxConcurrency, value, current) == current)
                {
                    return;
                }
            }
        }
    }
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

}

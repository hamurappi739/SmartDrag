using System.Collections.Concurrent;
using System.Threading.Channels;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Runtime;

/// <summary>
/// Executes at most one SmartDrag action at a time. The single-consumer policy is intentional for MVP:
/// image codecs can be memory-heavy, and predictable cancellation/resource usage is more valuable than throughput.
/// </summary>
public sealed class SequentialJobQueue : IJobQueue
{
    private readonly IActionExecutor _executor;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<JobControl> _channel;
    private readonly ConcurrentDictionary<JobId, JobControl> _jobs = new();
    private readonly object _enqueueGate = new();
    private readonly Dictionary<Guid, RequestIndexEntry> _requestIndex = new();
    private readonly Task _worker;
    private int _disposeState;

    public SequentialJobQueue(IActionExecutor executor, TimeProvider? timeProvider = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _channel = Channel.CreateUnbounded<JobControl>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessQueueAsync);
    }

    public event EventHandler<JobChangedEventArgs>? JobChanged;

    public JobId Enqueue(ActionRequest inputRequest)
    {
        ArgumentNullException.ThrowIfNull(inputRequest);
        var request = SnapshotRequest(inputRequest);

        JobSnapshot snapshot;
        JobId jobId;
        lock (_enqueueGate)
        {
            ThrowIfDisposed();

            if (_requestIndex.TryGetValue(request.RequestId, out var existing))
            {
                if (!Equivalent(existing, request))
                {
                    throw new InvalidOperationException(
                        $"RequestId '{request.RequestId}' is already bound to a different action/input/output policy.");
                }

                return existing.JobId;
            }

            jobId = JobId.New();
            snapshot = new JobSnapshot
            {
                Id = jobId,
                RequestId = request.RequestId,
                ActionId = request.ActionId,
                State = JobState.Queued,
                EnqueuedAt = _timeProvider.GetUtcNow(),
                InputPaths = request.InputPaths
            };

            var control = new JobControl(request, snapshot);
            if (!_jobs.TryAdd(jobId, control))
            {
                control.Dispose();
                throw new InvalidOperationException("A duplicate JobId was generated.");
            }

            _requestIndex.Add(request.RequestId, new RequestIndexEntry(jobId, request.ActionId, request.InputPaths, request.OutputPolicy));
            if (!_channel.Writer.TryWrite(control))
            {
                _requestIndex.Remove(request.RequestId);
                _jobs.TryRemove(jobId, out _);
                control.Dispose();
                throw new ObjectDisposedException(nameof(SequentialJobQueue));
            }
        }

        Publish(snapshot);
        return jobId;
    }

    public bool TryCancel(JobId jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var control))
        {
            return false;
        }

        JobSnapshot? changed = null;
        lock (control.Gate)
        {
            switch (control.Snapshot.State)
            {
                case JobState.Queued:
                    control.Cancellation.Cancel();
                    control.Snapshot = control.Snapshot with
                    {
                        State = JobState.Cancelled,
                        FinishedAt = _timeProvider.GetUtcNow(),
                        Error = CancelledError(control.Request.ActionId)
                    };
                    changed = control.Snapshot;
                    break;

                case JobState.Running:
                    control.Snapshot = control.Snapshot with { State = JobState.Cancelling };
                    control.Cancellation.Cancel();
                    changed = control.Snapshot;
                    break;

                case JobState.Cancelling:
                    return true;

                case JobState.Completed:
                case JobState.Failed:
                case JobState.Cancelled:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (changed is not null)
        {
            Publish(changed);
        }

        return true;
    }

    public bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot)
    {
        if (!_jobs.TryGetValue(jobId, out var control))
        {
            snapshot = null!;
            return false;
        }

        lock (control.Gate)
        {
            snapshot = control.Snapshot;
            return true;
        }
    }

    public IReadOnlyList<JobSnapshot> GetSnapshots()
    {
        return _jobs.Values
            .Select(control =>
            {
                lock (control.Gate)
                {
                    return control.Snapshot;
                }
            })
            .OrderBy(snapshot => snapshot.EnqueuedAt)
            .ThenBy(snapshot => snapshot.Id.Value)
            .ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        lock (_enqueueGate)
        {
            _channel.Writer.TryComplete();
        }

        foreach (var control in _jobs.Values)
        {
            lock (control.Gate)
            {
                if (!control.Snapshot.IsTerminal)
                {
                    control.Cancellation.Cancel();
                }
            }
        }

        try
        {
            await _worker.ConfigureAwait(false);
        }
        finally
        {
            foreach (var control in _jobs.Values)
            {
                control.Dispose();
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var control in _channel.Reader.ReadAllAsync())
        {
            JobSnapshot runningSnapshot;
            lock (control.Gate)
            {
                if (control.Snapshot.State == JobState.Cancelled)
                {
                    continue;
                }

                if (control.Cancellation.IsCancellationRequested)
                {
                    control.Snapshot = control.Snapshot with
                    {
                        State = JobState.Cancelled,
                        FinishedAt = _timeProvider.GetUtcNow(),
                        Error = CancelledError(control.Request.ActionId)
                    };
                    runningSnapshot = control.Snapshot;
                }
                else
                {
                    control.Snapshot = control.Snapshot with
                    {
                        State = JobState.Running,
                        StartedAt = _timeProvider.GetUtcNow()
                    };
                    runningSnapshot = control.Snapshot;
                }
            }

            Publish(runningSnapshot);

            if (runningSnapshot.State == JobState.Cancelled)
            {
                continue;
            }

            ActionResult result;
            try
            {
                result = await _executor.ExecuteAsync(control.Request, control.Cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (control.Cancellation.IsCancellationRequested)
            {
                result = ActionResult.Failed(CancelledError(control.Request.ActionId));
            }
            catch (Exception ex)
            {
                result = ActionResult.Failed(new AppError(
                    ErrorCode.Unknown,
                    $"Unhandled job executor exception for '{control.Request.ActionId}': {ex}",
                    "The operation failed."));
            }

            JobSnapshot finalSnapshot;
            lock (control.Gate)
            {
                var cancelled = control.Cancellation.IsCancellationRequested ||
                                result.Error?.Code == ErrorCode.OperationCancelled;

                finalSnapshot = control.Snapshot = control.Snapshot with
                {
                    State = result.Success
                        ? JobState.Completed
                        : cancelled
                            ? JobState.Cancelled
                            : JobState.Failed,
                    FinishedAt = _timeProvider.GetUtcNow(),
                    OutputPaths = ReadOnlyCopy(result.OutputPaths),
                    GeneratedArtifacts = ReadOnlyCopy(result.GeneratedArtifacts),
                    Metrics = result.Metrics,
                    Error = result.Error
                };
            }

            Publish(finalSnapshot);
        }
    }


    private static bool Equivalent(RequestIndexEntry existing, ActionRequest request)
    {
        if (existing.ActionId != request.ActionId || existing.OutputPolicy != request.OutputPolicy ||
            existing.InputPaths.Count != request.InputPaths.Count)
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var i = 0; i < existing.InputPaths.Count; i++)
        {
            if (!string.Equals(existing.InputPaths[i], request.InputPaths[i], comparison))
            {
                return false;
            }
        }

        return true;
    }

    private static ActionRequest SnapshotRequest(ActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.InputPaths);
        return request with
        {
            InputPaths = ReadOnlyCopy(request.InputPaths)
        };
    }

    private static IReadOnlyList<T> ReadOnlyCopy<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly(values?.ToArray() ?? Array.Empty<T>());

    private static AppError CancelledError(ActionId actionId) => new(
        ErrorCode.OperationCancelled,
        $"Action '{actionId}' was cancelled.",
        "The operation was cancelled.");

    private void Publish(JobSnapshot snapshot)
    {
        var handlers = JobChanged;
        if (handlers is null)
        {
            return;
        }

        var args = new JobChangedEventArgs(snapshot);
        foreach (EventHandler<JobChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch
            {
                // Observers (typically UI) must never be able to terminate the worker.
                // Production diagnostics will record observer failures once logging is composed.
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(SequentialJobQueue));
        }
    }

    private sealed record RequestIndexEntry(
        JobId JobId,
        ActionId ActionId,
        IReadOnlyList<string> InputPaths,
        SmartDrag.Core.Output.OutputPolicy OutputPolicy);

    private sealed class JobControl : IDisposable
    {
        public JobControl(ActionRequest request, JobSnapshot snapshot)
        {
            Request = request;
            Snapshot = snapshot;
        }

        public object Gate { get; } = new();
        public ActionRequest Request { get; }
        public CancellationTokenSource Cancellation { get; } = new();
        public JobSnapshot Snapshot { get; set; }

        public void Dispose() => Cancellation.Dispose();
    }
}

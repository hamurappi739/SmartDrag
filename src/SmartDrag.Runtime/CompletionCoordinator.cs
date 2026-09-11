using System.Collections.Concurrent;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Runtime;

/// <summary>
/// Converts terminal job transitions into at-most-once completion models. It never performs UI work inside
/// the JobQueue callback; listeners receive an immutable projection and are isolated from queue execution.
/// </summary>
public sealed class CompletionCoordinator : IDisposable
{
    private readonly IJobQueue _jobQueue;
    private readonly CompletionCapabilities _capabilities;
    private readonly ConcurrentDictionary<JobId, CompletionModel> _terminal = new();
    private int _disposed;

    public CompletionCoordinator(IJobQueue jobQueue, CompletionCapabilities capabilities)
    {
        _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _jobQueue.JobChanged += OnJobChanged;

        // Handle composition after a job has already completed.
        foreach (var snapshot in _jobQueue.GetSnapshots().Where(snapshot => snapshot.IsTerminal))
        {
            TryPublish(snapshot);
        }
    }

    public event EventHandler<CompletionReadyEventArgs>? CompletionReady;

    public bool TryGet(JobId jobId, out CompletionModel completion) =>
        _terminal.TryGetValue(jobId, out completion!);

    public IReadOnlyList<CompletionModel> GetCompletions() =>
        _terminal.Values.OrderBy(model => model.JobId.Value).ToArray();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _jobQueue.JobChanged -= OnJobChanged;
    }

    private void OnJobChanged(object? sender, JobChangedEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0 || !args.Snapshot.IsTerminal)
        {
            return;
        }

        TryPublish(args.Snapshot);
    }

    private void TryPublish(JobSnapshot snapshot)
    {
        var model = CompletionProjector.FromTerminalJob(snapshot, _capabilities);
        if (!_terminal.TryAdd(snapshot.Id, model))
        {
            return;
        }

        var handlers = CompletionReady;
        if (handlers is null)
        {
            return;
        }

        var args = new CompletionReadyEventArgs(model);
        foreach (EventHandler<CompletionReadyEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch
            {
                // Completion observers are UI/integration boundaries and must not destabilize Runtime.
            }
        }
    }
}

public sealed class CompletionReadyEventArgs(CompletionModel completion) : EventArgs
{
    public CompletionModel Completion { get; } = completion;
}

using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;
using SmartDrag.Runtime;

namespace SmartDrag.Presentation;

/// <summary>
/// Owns toolkit-neutral status/completion presentation state. It observes runtime events, serializes user intents,
/// and exposes immutable snapshots. It never receives raw file-processing services and cannot bypass the runtime
/// completion-command executor or JobQueue cancellation contract.
/// </summary>
public sealed class MvpPresentationCoordinator : IDisposable
{
    private readonly IJobQueue _jobQueue;
    private readonly CompletionCoordinator _completionCoordinator;
    private readonly ICompletionCommandExecutor _completionCommands;
    private readonly IActionRegistry _actionRegistry;
    private readonly object _gate = new();
    private readonly Queue<CompletionModel> _pendingCompletions = new();
    private readonly HashSet<JobId> _knownCompletions = new();
    private CompletionPresentationModel? _visibleCompletion;
    private SmartDragPresentationSnapshot _current = SmartDragPresentationSnapshot.Quiet;
    private int _disposed;

    public MvpPresentationCoordinator(
        IJobQueue jobQueue,
        CompletionCoordinator completionCoordinator,
        ICompletionCommandExecutor completionCommands,
        IActionRegistry actionRegistry)
    {
        _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
        _completionCoordinator = completionCoordinator ?? throw new ArgumentNullException(nameof(completionCoordinator));
        _completionCommands = completionCommands ?? throw new ArgumentNullException(nameof(completionCommands));
        _actionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));

        _jobQueue.JobChanged += OnJobChanged;
        _completionCoordinator.CompletionReady += OnCompletionReady;

        var existingCompletions = _completionCoordinator.GetCompletions().ToDictionary(model => model.JobId);
        foreach (var terminal in _jobQueue.GetSnapshots()
                     .Where(snapshot => snapshot.IsTerminal && existingCompletions.ContainsKey(snapshot.Id))
                     .OrderBy(snapshot => snapshot.FinishedAt ?? snapshot.EnqueuedAt)
                     .ThenBy(snapshot => snapshot.EnqueuedAt)
                     .ThenBy(snapshot => snapshot.Id.Value))
        {
            if (_knownCompletions.Add(terminal.Id))
            {
                _pendingCompletions.Enqueue(existingCompletions[terminal.Id]);
            }
        }

        PromoteCompletionIfNeeded_NoLock();
        _current = BuildSnapshot_NoLock();
    }

    public event EventHandler<PresentationSnapshotChangedEventArgs>? SnapshotChanged;

    public SmartDragPresentationSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool TryCancelVisibleOperation(JobId jobId)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_current.Operation is null || _current.Operation.JobId != jobId || !_current.Operation.CanCancel)
            {
                return false;
            }
        }

        // JobQueue is the authority for whether the state is still cancellable at this exact instant.
        return _jobQueue.TryCancel(jobId);
    }

    public async Task<CompletionCommandExecutionResult> ExecuteVisibleCompletionCommandAsync(
        JobId jobId,
        CompletionCommand command,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (command == CompletionCommand.Dismiss)
        {
            SmartDragPresentationSnapshot changed;
            lock (_gate)
            {
                if (_visibleCompletion is null || _visibleCompletion.JobId != jobId ||
                    _visibleCompletion.IsCommandInFlight ||
                    !_visibleCompletion.Commands.Contains(CompletionCommand.Dismiss))
                {
                    return Result(CompletionCommandExecutionStatus.CommandNotOffered);
                }

                _visibleCompletion = null;
                PromoteCompletionIfNeeded_NoLock();
                changed = StoreSnapshot_NoLock();
            }

            Publish(changed);
            return CompletionCommandExecutionResult.Executed();
        }

        SmartDragPresentationSnapshot busySnapshot;
        lock (_gate)
        {
            if (_visibleCompletion is null || _visibleCompletion.JobId != jobId ||
                !_visibleCompletion.Commands.Contains(command) || _visibleCompletion.IsCommandInFlight)
            {
                return Result(CompletionCommandExecutionStatus.CommandNotOffered);
            }

            _visibleCompletion = _visibleCompletion with { IsCommandInFlight = true, CommandError = null };
            busySnapshot = StoreSnapshot_NoLock();
        }
        Publish(busySnapshot);

        CompletionCommandExecutionResult execution;
        try
        {
            execution = await _completionCommands.ExecuteAsync(jobId, command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SmartDragPresentationSnapshot? changed = null;
            lock (_gate)
            {
                if (_visibleCompletion?.JobId == jobId)
                {
                    _visibleCompletion = _visibleCompletion with { IsCommandInFlight = false };
                    changed = StoreSnapshot_NoLock();
                }
            }
            if (changed is not null) Publish(changed);
            throw;
        }
        catch (Exception ex)
        {
            // Completion adapters are process boundaries. Convert an unexpected adapter failure into a safe
            // terminal result and clear the in-flight flag below so the user can dismiss or retry the command.
            execution = new CompletionCommandExecutionResult
            {
                Status = CompletionCommandExecutionStatus.Failed,
                Error = new AppError(
                    ErrorCode.CompletionCommandFailed,
                    $"{ex.GetType().FullName} (0x{ex.HResult:X8})",
                    "The completion action could not be completed safely.")
            };
        }

        SmartDragPresentationSnapshot? finalSnapshot = null;
        lock (_gate)
        {
            if (_visibleCompletion?.JobId == jobId)
            {
                if (execution.Success && command == CompletionCommand.DeleteGeneratedOutput)
                {
                    _visibleCompletion = _visibleCompletion with
                    {
                        IsCommandInFlight = false,
                        OutputWasDeleted = true,
                        Title = "Generated file deleted",
                        Detail = "The original file was not changed.",
                        Commands = new[] { CompletionCommand.Dismiss },
                        CommandError = null
                    };
                }
                else
                {
                    _visibleCompletion = _visibleCompletion with
                    {
                        IsCommandInFlight = false,
                        CommandError = execution.Success ? null : execution.Error?.UserMessage ?? "The action could not be completed."
                    };
                }

                finalSnapshot = StoreSnapshot_NoLock();
            }
        }
        if (finalSnapshot is not null) Publish(finalSnapshot);

        return execution;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _jobQueue.JobChanged -= OnJobChanged;
        _completionCoordinator.CompletionReady -= OnCompletionReady;
    }

    private void OnJobChanged(object? sender, JobChangedEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        SmartDragPresentationSnapshot changed;
        lock (_gate)
        {
            changed = StoreSnapshot_NoLock();
        }
        Publish(changed);
    }

    private void OnCompletionReady(object? sender, CompletionReadyEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        SmartDragPresentationSnapshot? changed = null;
        lock (_gate)
        {
            if (!_knownCompletions.Add(args.Completion.JobId))
            {
                return;
            }

            _pendingCompletions.Enqueue(args.Completion);
            PromoteCompletionIfNeeded_NoLock();
            changed = StoreSnapshot_NoLock();
        }
        Publish(changed);
    }

    private SmartDragPresentationSnapshot StoreSnapshot_NoLock() => _current = BuildSnapshot_NoLock();

    private SmartDragPresentationSnapshot BuildSnapshot_NoLock()
    {
        var jobs = _jobQueue.GetSnapshots();
        return new SmartDragPresentationSnapshot
        {
            Operation = MvpPresentationPolicy.ProjectOperation(jobs, ResolveActionLabel),
            Completion = _visibleCompletion,
            NonTerminalJobCount = jobs.Count(snapshot => !snapshot.IsTerminal),
            PendingCompletionCount = _pendingCompletions.Count
        };
    }

    private void PromoteCompletionIfNeeded_NoLock()
    {
        if (_visibleCompletion is null && _pendingCompletions.TryDequeue(out var next))
        {
            _visibleCompletion = MvpPresentationPolicy.ProjectCompletion(next);
        }
    }

    private string ResolveActionLabel(ActionId actionId) =>
        _actionRegistry.TryGet(actionId, out var definition) ? definition.DisplayName : "SmartDrag action";

    private void Publish(SmartDragPresentationSnapshot snapshot)
    {
        var handlers = SnapshotChanged;
        if (handlers is null)
        {
            return;
        }

        var args = new PresentationSnapshotChangedEventArgs(snapshot);
        foreach (EventHandler<PresentationSnapshotChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch
            {
                // UI observers are process-internal boundaries and may not destabilize runtime state.
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(MvpPresentationCoordinator));
        }
    }

    private static CompletionCommandExecutionResult Result(CompletionCommandExecutionStatus status) => new()
    {
        Status = status
    };
}

namespace SmartDrag.Windows.Lifetime;

/// <summary>
/// Process-level single-instance lease backed by a Windows named mutex.
/// Mutex ownership is thread-affine, so a dedicated lifetime thread acquires and releases the mutex on the
/// same thread. Async application startup/shutdown may therefore dispose the lease from any managed thread.
/// </summary>
public sealed class NamedMutexSingleInstanceLease : IDisposable
{
    public const string DefaultMutexName = "Local\\SmartDrag.Desktop.Instance.v1";

    private readonly Thread? _ownerThread;
    private readonly ManualResetEventSlim? _releaseSignal;
    private int _disposeState;

    private NamedMutexSingleInstanceLease(bool acquired, Thread? ownerThread, ManualResetEventSlim? releaseSignal)
    {
        IsAcquired = acquired;
        _ownerThread = ownerThread;
        _releaseSignal = releaseSignal;
    }

    public bool IsAcquired { get; }

    public static NamedMutexSingleInstanceLease TryAcquire(string? mutexName = null)
    {
        var name = mutexName ?? DefaultMutexName;
        using var ready = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var acquired = false;
        Exception? ownerFailure = null;

        var thread = new Thread(() =>
        {
            try
            {
                using var mutex = new Mutex(initiallyOwned: false, name);
                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    // Previous process/thread died while owning the mutex. Windows grants ownership to this
                    // waiter; this is precisely the state in which startup recovery should run.
                    acquired = true;
                }

                ready.Set();
                if (!acquired)
                {
                    return;
                }

                release.Wait();
                mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                ownerFailure = ex;
                ready.Set();
            }
        })
        {
            IsBackground = true,
            Name = "SmartDrag.SingleInstanceLease"
        };

        thread.Start();
        ready.Wait();

        if (ownerFailure is not null)
        {
            release.Set();
            thread.Join();
            release.Dispose();
            throw new InvalidOperationException("SmartDrag could not establish the single-instance mutex lease.", ownerFailure);
        }

        if (!acquired)
        {
            thread.Join();
            release.Dispose();
            return new NamedMutexSingleInstanceLease(false, null, null);
        }

        return new NamedMutexSingleInstanceLease(true, thread, release);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        if (!IsAcquired || _ownerThread is null || _releaseSignal is null)
        {
            return;
        }

        _releaseSignal.Set();
        _ownerThread.Join();
        _releaseSignal.Dispose();
    }
}

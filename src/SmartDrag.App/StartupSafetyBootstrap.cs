using SmartDrag.Core.Recovery;
using SmartDrag.Infrastructure.Recovery;
using SmartDrag.Windows.Lifetime;

namespace SmartDrag.App;

/// <summary>
/// Safety-first production startup boundary. The intended startup order is:
/// 1) acquire the single-instance mutex; 2) open the durable recovery journal; 3) reconcile stale partials;
/// only then may drag hooks/overlay/runtime processing be composed.
/// </summary>
public static class StartupSafetyBootstrap
{
    public static async Task<StartupSafetyBootstrapResult> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var instanceLease = NamedMutexSingleInstanceLease.TryAcquire();
        if (!instanceLease.IsAcquired)
        {
            instanceLease.Dispose();
            return StartupSafetyBootstrapResult.AlreadyRunning();
        }

        JsonOutputRecoveryJournal? journal = null;
        try
        {
            journal = new JsonOutputRecoveryJournal(JsonOutputRecoveryJournal.GetDefaultJournalPath());
            var recovery = await new OutputStartupRecoveryService(journal)
                .RecoverAsync(cancellationToken)
                .ConfigureAwait(false);

            var lease = new StartupSafetyLease(instanceLease, journal, recovery);
            return recovery.IsClean
                ? StartupSafetyBootstrapResult.Acquired(lease)
                : StartupSafetyBootstrapResult.RecoveryBlocked(lease);
        }
        catch
        {
            journal?.Dispose();
            instanceLease.Dispose();
            throw;
        }
    }
}

public enum StartupSafetyBootstrapStatus
{
    Acquired = 0,
    AlreadyRunning,
    RecoveryBlocked
}

public sealed record StartupSafetyBootstrapResult
{
    public required StartupSafetyBootstrapStatus Status { get; init; }
    public StartupSafetyLease? Lease { get; init; }
    public bool RuntimeMayStart => Status == StartupSafetyBootstrapStatus.Acquired && Lease is not null;

    public static StartupSafetyBootstrapResult Acquired(StartupSafetyLease lease) => new()
    {
        Status = StartupSafetyBootstrapStatus.Acquired,
        Lease = lease
    };

    public static StartupSafetyBootstrapResult AlreadyRunning() => new()
    {
        Status = StartupSafetyBootstrapStatus.AlreadyRunning
    };

    public static StartupSafetyBootstrapResult RecoveryBlocked(StartupSafetyLease lease) => new()
    {
        Status = StartupSafetyBootstrapStatus.RecoveryBlocked,
        Lease = lease
    };
}

/// <summary>
/// Must remain alive for the entire SmartDrag process lifetime. It owns both the process mutex and durable
/// recovery journal used by PhysicalOutputManager.
/// </summary>
public sealed class StartupSafetyLease : IDisposable
{
    private readonly NamedMutexSingleInstanceLease _instanceLease;
    private bool _disposed;

    internal StartupSafetyLease(
        NamedMutexSingleInstanceLease instanceLease,
        JsonOutputRecoveryJournal recoveryJournal,
        StartupRecoveryReport recoveryReport)
    {
        _instanceLease = instanceLease;
        RecoveryJournal = recoveryJournal;
        RecoveryReport = recoveryReport;
    }

    public JsonOutputRecoveryJournal RecoveryJournal { get; }
    public StartupRecoveryReport RecoveryReport { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RecoveryJournal.Dispose();
        _instanceLease.Dispose();
    }
}

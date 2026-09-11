using SmartDrag.Core.Recovery;

namespace SmartDrag.Infrastructure.Recovery;

/// <summary>
/// Deterministic journal for tests and non-production composition. Production must use the durable JSON journal.
/// </summary>
public sealed class InMemoryOutputRecoveryJournal : IOutputRecoveryJournal
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, OutputRecoveryRecord> _records = new();

    public Task<IReadOnlyList<OutputRecoveryRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<OutputRecoveryRecord>>(
                _records.Values.OrderBy(record => record.CreatedAt).ToArray());
        }
    }

    public Task UpsertAsync(OutputRecoveryRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _records[record.ReservationId] = record;
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _records.Remove(reservationId);
        }

        return Task.CompletedTask;
    }
}

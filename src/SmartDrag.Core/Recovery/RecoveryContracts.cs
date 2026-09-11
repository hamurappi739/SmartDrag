using SmartDrag.Core.Errors;

namespace SmartDrag.Core.Recovery;

/// <summary>
/// Durable evidence that SmartDrag reserved a specific temporary output path.
/// The journal deliberately stores only the reservation id, temporary path and timestamp;
/// source/final paths are unnecessary for crash cleanup and are therefore not persisted here.
/// </summary>
public sealed record OutputRecoveryRecord
{
    public required Guid ReservationId { get; init; }
    public required string TemporaryPath { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public interface IOutputRecoveryJournal
{
    Task<IReadOnlyList<OutputRecoveryRecord>> ReadAllAsync(CancellationToken cancellationToken);
    Task UpsertAsync(OutputRecoveryRecord record, CancellationToken cancellationToken);
    Task RemoveAsync(Guid reservationId, CancellationToken cancellationToken);
}

public interface IStartupRecoveryService
{
    Task<StartupRecoveryReport> RecoverAsync(CancellationToken cancellationToken);
}

public sealed record StartupRecoveryReport
{
    public int ExaminedEntries { get; init; }
    public int DeletedPartials { get; init; }
    public int MissingPartials { get; init; }
    public int InvalidEntries { get; init; }
    public int FailedEntries { get; init; }
    public int RemainingEntries { get; init; }
    public IReadOnlyList<AppError> Errors { get; init; } = Array.Empty<AppError>();

    public bool IsClean => FailedEntries == 0 && RemainingEntries == 0;
}

namespace SmartDrag.Core.Output;

public interface IOutputManager
{
    Task<OutputReservationResult> ReserveAsync(OutputRequest request, CancellationToken cancellationToken);
    Task<OutputCommitResult> CommitAsync(OutputReservation reservation, CancellationToken cancellationToken);
    Task<OutputCleanupResult> AbandonAsync(OutputReservation reservation);
}

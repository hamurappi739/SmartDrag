using SmartDrag.Core.Errors;
using SmartDrag.Core.Recovery;

namespace SmartDrag.Infrastructure.Recovery;

/// <summary>
/// Crash recovery is journal-driven only. It NEVER scans user directories for wildcard partials.
/// A journal record may authorize deletion only when its path still follows SmartDrag's reservation-bound
/// temporary filename convention.
/// </summary>
public sealed class OutputStartupRecoveryService(IOutputRecoveryJournal journal) : IStartupRecoveryService
{
    private readonly IOutputRecoveryJournal _journal = journal ?? throw new ArgumentNullException(nameof(journal));

    public async Task<StartupRecoveryReport> RecoverAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OutputRecoveryRecord> records;
        try
        {
            records = await _journal.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new StartupRecoveryReport
            {
                FailedEntries = 1,
                RemainingEntries = 1,
                Errors = new[]
                {
                    new AppError(
                        ErrorCode.RecoveryJournalUnavailable,
                        $"Recovery journal could not be read: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                        "SmartDrag could not check temporary-file recovery state.")
                }
            };
        }

        var deleted = 0;
        var missing = 0;
        var invalid = 0;
        var failed = 0;
        var errors = new List<AppError>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsValidRecoveryPath(record))
            {
                invalid++;
                errors.Add(new AppError(
                    ErrorCode.RecoveryEntryInvalid,
                    $"Recovery entry '{record.ReservationId}' failed temporary-path validation.",
                    "SmartDrag ignored an invalid recovery record."));

                await BestEffortRemoveJournalRecordAsync(record.ReservationId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (!File.Exists(record.TemporaryPath))
                {
                    missing++;
                    await _journal.RemoveAsync(record.ReservationId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                File.Delete(record.TemporaryPath);
                deleted++;
                await _journal.RemoveAsync(record.ReservationId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add(new AppError(
                    ErrorCode.RecoveryCleanupFailed,
                    $"Recovery cleanup/journal update failed for reservation '{record.ReservationId}': {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                    "SmartDrag could not complete temporary-file recovery."));
            }
        }

        IReadOnlyList<OutputRecoveryRecord> remaining;
        try
        {
            remaining = await _journal.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            remaining = Array.Empty<OutputRecoveryRecord>();
            failed++;
            errors.Add(new AppError(
                ErrorCode.RecoveryJournalUnavailable,
                $"Recovery journal could not be re-read after cleanup: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not verify that temporary-file recovery completed."));
        }

        return new StartupRecoveryReport
        {
            ExaminedEntries = records.Count,
            DeletedPartials = deleted,
            MissingPartials = missing,
            InvalidEntries = invalid,
            FailedEntries = failed,
            RemainingEntries = remaining.Count,
            Errors = errors
        };
    }

    public static bool IsValidRecoveryPath(OutputRecoveryRecord record)
    {
        if (record.ReservationId == Guid.Empty || string.IsNullOrWhiteSpace(record.TemporaryPath))
        {
            return false;
        }

        try
        {
            if (!Path.IsPathFullyQualified(record.TemporaryPath))
            {
                return false;
            }

            var fileName = Path.GetFileName(record.TemporaryPath);
            var marker = $".smartdrag-{record.ReservationId:N}.partial";
            return fileName.StartsWith(".", StringComparison.Ordinal) &&
                   fileName.Contains(marker, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task BestEffortRemoveJournalRecordAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        try
        {
            await _journal.RemoveAsync(reservationId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Invalid journal entries never authorize filesystem deletion. If journal mutation also fails,
            // the same invalid entry can be ignored again on the next startup.
        }
    }
}

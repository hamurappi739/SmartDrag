using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Output;
using SmartDrag.Core.Recovery;

namespace SmartDrag.Infrastructure;

/// <summary>
/// Implements SmartDrag's non-destructive output policy. Processing writes to a same-directory temporary
/// artifact first; only a validated temporary file may be moved into the final user-visible name.
/// Every reservation is journaled before it is returned so process crashes can recover known partials.
/// Existing files are never overwritten.
/// </summary>
public sealed class PhysicalOutputManager : IOutputManager
{
    private readonly IOutputRecoveryJournal _recoveryJournal;
    private readonly IGeneratedArtifactIdentityProvider? _identityProvider;
    private readonly TimeProvider _timeProvider;

    public PhysicalOutputManager(
        IOutputRecoveryJournal recoveryJournal,
        IGeneratedArtifactIdentityProvider? identityProvider = null,
        TimeProvider? timeProvider = null)
    {
        _recoveryJournal = recoveryJournal ?? throw new ArgumentNullException(nameof(recoveryJournal));
        _identityProvider = identityProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<OutputReservationResult> ReserveAsync(OutputRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.Policy.PreserveSource)
        {
            return OutputReservationResult.Failed(new AppError(
                ErrorCode.InvalidOutputPolicy,
                "MVP output policy requested PreserveSource=false.",
                "SmartDrag will not overwrite the original file."));
        }

        string sourcePath;
        string destinationDirectory;
        string preferredFinalPath;
        try
        {
            sourcePath = Path.GetFullPath(request.SourcePath);
            if (!File.Exists(sourcePath))
            {
                return OutputReservationResult.Failed(new AppError(
                    ErrorCode.SourceNotFound,
                    "The source file does not exist at reservation time.",
                    "The original file could not be found."));
            }

            destinationDirectory = ResolveDestinationDirectory(sourcePath, request.Policy);
            Directory.CreateDirectory(destinationDirectory);

            var outputName = BuildOutputFileName(sourcePath, request.Suffix, request.NewExtension);
            preferredFinalPath = Path.Combine(destinationDirectory, outputName);

            if (PathEquals(sourcePath, preferredFinalPath))
            {
                return OutputReservationResult.Failed(new AppError(
                    ErrorCode.InvalidOutputPolicy,
                    "The requested output path resolves to the source path.",
                    "SmartDrag will not overwrite the original file."));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return OutputReservationResult.Failed(new AppError(
                ErrorCode.InvalidOutputPolicy,
                $"Output reservation path validation failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not create a safe output path."));
        }

        var reservationId = Guid.NewGuid();
        string temporaryPath;
        try
        {
            temporaryPath = AllocateTemporaryPath(destinationDirectory, preferredFinalPath, reservationId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OutputReservationResult.Failed(new AppError(
                ErrorCode.OutputAccessDenied,
                $"Temporary output path allocation failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not reserve a temporary output file."));
        }

        var reservation = new OutputReservation
        {
            Id = reservationId,
            SourcePath = sourcePath,
            TemporaryPath = temporaryPath,
            CollisionBasePath = preferredFinalPath,
            PreferredFinalPath = preferredFinalPath,
            Policy = request.Policy
        };

        try
        {
            await _recoveryJournal.UpsertAsync(new OutputRecoveryRecord
            {
                ReservationId = reservation.Id,
                TemporaryPath = reservation.TemporaryPath,
                CreatedAt = _timeProvider.GetUtcNow()
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OutputReservationResult.Failed(new AppError(
                ErrorCode.RecoveryJournalUnavailable,
                $"Output reservation was not issued because recovery journaling failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not safely reserve an output file."));
        }

        return OutputReservationResult.Succeeded(reservation);
    }

    public async Task<OutputCommitResult> CommitAsync(OutputReservation reservation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryValidateReservation(reservation, out var validationError))
        {
            return OutputCommitResult.Failed(validationError);
        }

        if (!reservation.Policy.PreserveSource)
        {
            return OutputCommitResult.Failed(new AppError(
                ErrorCode.InvalidOutputPolicy,
                "Commit rejected PreserveSource=false.",
                "SmartDrag will not overwrite the original file."));
        }

        if (!File.Exists(reservation.TemporaryPath))
        {
            return OutputCommitResult.Failed(new AppError(
                ErrorCode.TemporaryOutputMissing,
                "The reserved temporary output does not exist at commit time.",
                "The generated file could not be finalized."));
        }

        try
        {
            var finalPath = ResolveCollision(reservation);
            cancellationToken.ThrowIfCancellationRequested();

            // Capture the identity of the exact temporary object before the rename. After the path move we will
            // re-read identity from the final path and authorize destructive completion only if both identities match.
            // A mismatch means another actor interfered with either path; the output may still be valid, but SmartDrag
            // will not claim strong ownership for later deletion.
            ArtifactIdentity? preCommitIdentity = null;
            if (_identityProvider is not null)
            {
                preCommitIdentity = await TryCaptureIdentityAsync(reservation.TemporaryPath).ConfigureAwait(false);
            }

            // overwrite:false is the final race barrier. Another process may create a candidate between
            // ResolveCollision and File.Move; in that case we retry a new unique name rather than overwrite.
            for (var attempt = 0; attempt < 128; attempt++)
            {
                try
                {
                    File.Move(reservation.TemporaryPath, finalPath, overwrite: false);
                    await BestEffortRemoveJournalEntryAsync(reservation.Id).ConfigureAwait(false);

                    ArtifactIdentity? identity = null;
                    if (_identityProvider is not null && preCommitIdentity is not null)
                    {
                        var postCommitIdentity = await TryCaptureIdentityAsync(finalPath).ConfigureAwait(false);
                        if (postCommitIdentity == preCommitIdentity)
                        {
                            identity = preCommitIdentity;
                        }
                    }

                    return OutputCommitResult.Succeeded(new GeneratedArtifact
                    {
                        Path = finalPath,
                        Identity = identity
                    });
                }
                catch (IOException) when (File.Exists(finalPath) && reservation.Policy.CollisionPolicy == CollisionPolicy.GenerateUniqueName)
                {
                    finalPath = NextUniquePath(reservation.CollisionBasePath, attempt + 2);
                }
            }

            return OutputCommitResult.Failed(new AppError(
                ErrorCode.OutputCollision,
                "Unable to allocate a unique output name after repeated collision retries.",
                "SmartDrag could not find an available output filename."));
        }
        catch (IOException ex)
        {
            var code = IsDiskFull(ex) ? ErrorCode.DiskFull : ErrorCode.OutputCollision;
            var userMessage = code == ErrorCode.DiskFull
                ? "There is not enough free disk space."
                : "A file with the output name already exists.";
            return OutputCommitResult.Failed(new AppError(code, ex.ToString(), userMessage));
        }
        catch (UnauthorizedAccessException ex)
        {
            return OutputCommitResult.Failed(new AppError(
                ErrorCode.OutputAccessDenied,
                ex.ToString(),
                "SmartDrag does not have permission to write the generated file."));
        }
    }

    public async Task<OutputCleanupResult> AbandonAsync(OutputReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        if (!TryValidateReservation(reservation, out var validationError))
        {
            return OutputCleanupResult.Failed(validationError);
        }

        try
        {
            if (File.Exists(reservation.TemporaryPath))
            {
                File.Delete(reservation.TemporaryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the journal record: it is the only authority startup recovery has to retry this cleanup.
            return OutputCleanupResult.Failed(new AppError(
                ErrorCode.CleanupFailed,
                $"Temporary-file cleanup failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not remove a temporary file."));
        }

        try
        {
            await _recoveryJournal.RemoveAsync(reservation.Id, CancellationToken.None).ConfigureAwait(false);
            return OutputCleanupResult.Succeeded();
        }
        catch (Exception ex)
        {
            // The partial is already gone. A stale journal record is safe: startup recovery will see a missing
            // path and remove the record without touching any other file.
            return OutputCleanupResult.Failed(new AppError(
                ErrorCode.RecoveryJournalUnavailable,
                $"Temporary output was removed but its recovery record could not be cleared: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag removed the temporary file but could not update recovery state."));
        }
    }

    private async Task<ArtifactIdentity?> TryCaptureIdentityAsync(string path)
    {
        if (_identityProvider is null)
        {
            return null;
        }

        try
        {
            var capture = await _identityProvider
                .CaptureAsync(path, CancellationToken.None)
                .ConfigureAwait(false);
            return capture.Success ? capture.Identity : null;
        }
        catch
        {
            // Strong identity is an optional destructive-completion capability. Failing to capture it must not
            // roll back or misreport a successful non-destructive output.
            return null;
        }
    }

    private async Task BestEffortRemoveJournalEntryAsync(Guid reservationId)
    {
        try
        {
            await _recoveryJournal.RemoveAsync(reservationId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Final output is already committed and the partial path no longer exists. Leaving a stale journal
            // entry is safe; startup recovery will remove the missing record on the next run.
        }
    }

    private static string ResolveDestinationDirectory(string sourcePath, OutputPolicy policy)
    {
        return policy.LocationMode switch
        {
            OutputLocationMode.SameDirectory => Path.GetDirectoryName(sourcePath)
                ?? throw new InvalidOperationException("Source file has no parent directory."),
            OutputLocationMode.ConfiguredDirectory when !string.IsNullOrWhiteSpace(policy.ConfiguredDirectoryPath)
                => Path.GetFullPath(policy.ConfiguredDirectoryPath),
            OutputLocationMode.ConfiguredDirectory
                => throw new ArgumentException("ConfiguredDirectoryPath is required for ConfiguredDirectory mode."),
            _ => throw new ArgumentOutOfRangeException(nameof(policy.LocationMode))
        };
    }

    private static string BuildOutputFileName(string sourcePath, string suffix, string? newExtension)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = newExtension ?? Path.GetExtension(sourcePath);
        if (!string.IsNullOrEmpty(extension) && extension[0] != '.')
        {
            extension = "." + extension;
        }

        return $"{baseName}{suffix}{extension}";
    }

    private static string AllocateTemporaryPath(string directory, string finalPath, Guid reservationId)
    {
        var targetName = Path.GetFileNameWithoutExtension(finalPath);
        var extension = Path.GetExtension(finalPath);
        var candidate = Path.Combine(
            directory,
            $".{targetName}.smartdrag-{reservationId:N}.partial{extension}");

        if (File.Exists(candidate))
        {
            throw new IOException("The reservation-bound temporary path already exists.");
        }

        return candidate;
    }

    private static string ResolveCollision(OutputReservation reservation)
    {
        if (!File.Exists(reservation.PreferredFinalPath))
        {
            return reservation.PreferredFinalPath;
        }

        if (reservation.Policy.CollisionPolicy == CollisionPolicy.Fail)
        {
            throw new IOException("The preferred output path already exists.");
        }

        for (var number = 2; number < 130; number++)
        {
            var candidate = NextUniquePath(reservation.CollisionBasePath, number);
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Unable to allocate a unique output path.");
    }

    private static string NextUniquePath(string basePath, int number)
    {
        var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        return Path.Combine(directory, $"{stem} ({number}){extension}");
    }

    private static bool IsDiskFull(IOException exception)
    {
        var nativeCode = exception.HResult & 0xFFFF;
        return nativeCode is 112 or 39;
    }

    private static bool PathEquals(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }

    private static bool TryValidateReservation(OutputReservation reservation, out AppError error)
    {
        try
        {
            if (reservation.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(reservation.SourcePath)
                || string.IsNullOrWhiteSpace(reservation.TemporaryPath)
                || string.IsNullOrWhiteSpace(reservation.CollisionBasePath)
                || string.IsNullOrWhiteSpace(reservation.PreferredFinalPath)
                || reservation.Policy is null)
            {
                error = InvalidReservationError("Reservation is missing one or more required fields.");
                return false;
            }

            var sourcePath = Path.GetFullPath(reservation.SourcePath);
            var temporaryPath = Path.GetFullPath(reservation.TemporaryPath);
            var collisionBasePath = Path.GetFullPath(reservation.CollisionBasePath);
            var preferredFinalPath = Path.GetFullPath(reservation.PreferredFinalPath);
            var temporaryDirectory = Path.GetDirectoryName(temporaryPath);
            var finalDirectory = Path.GetDirectoryName(preferredFinalPath);

            if (temporaryDirectory is null
                || finalDirectory is null
                || !PathEquals(temporaryDirectory, finalDirectory)
                || !PathEquals(collisionBasePath, preferredFinalPath)
                || PathEquals(sourcePath, temporaryPath)
                || PathEquals(sourcePath, preferredFinalPath))
            {
                error = InvalidReservationError("Reservation paths do not describe one source-preserving output transaction.");
                return false;
            }

            var temporaryName = Path.GetFileName(temporaryPath);
            var expectedMarker = $".smartdrag-{reservation.Id:N}.partial";
            if (!temporaryName.Contains(expectedMarker, StringComparison.OrdinalIgnoreCase))
            {
                error = InvalidReservationError("Temporary output is not bound to this reservation id.");
                return false;
            }

            if (reservation.Policy.LocationMode == OutputLocationMode.ConfiguredDirectory
                && string.IsNullOrWhiteSpace(reservation.Policy.ConfiguredDirectoryPath))
            {
                error = InvalidReservationError("Configured output reservations must include an explicit directory.");
                return false;
            }

            if (!reservation.Policy.PreserveSource)
            {
                error = new AppError(
                    ErrorCode.InvalidOutputPolicy,
                    "Commit rejected PreserveSource=false.",
                    "SmartDrag will not overwrite the original file.");
                return false;
            }

            error = null!;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = InvalidReservationError($"Reservation path validation failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).");
            return false;
        }
    }

    private static AppError InvalidReservationError(string technical) => new(
        ErrorCode.InvalidOutputPolicy,
        technical,
        "SmartDrag rejected an unsafe output transaction.");
}

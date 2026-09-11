using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;

namespace SmartDrag.Core.Output;

public sealed record OutputReservation
{
    public required Guid Id { get; init; }
    public required string SourcePath { get; init; }
    public required string TemporaryPath { get; init; }
    public required string CollisionBasePath { get; init; }
    public required string PreferredFinalPath { get; init; }
    public required OutputPolicy Policy { get; init; }
}

public sealed record OutputReservationResult
{
    public required bool Success { get; init; }
    public OutputReservation? Reservation { get; init; }
    public AppError? Error { get; init; }

    public static OutputReservationResult Succeeded(OutputReservation reservation) => new()
    {
        Success = true,
        Reservation = reservation
    };

    public static OutputReservationResult Failed(AppError error) => new()
    {
        Success = false,
        Error = error
    };
}

public sealed record OutputCommitResult
{
    public required bool Success { get; init; }
    public GeneratedArtifact? Artifact { get; init; }
    public string? FinalPath => Artifact?.Path;
    public AppError? Error { get; init; }

    public static OutputCommitResult Succeeded(GeneratedArtifact artifact) => new()
    {
        Success = true,
        Artifact = artifact
    };

    public static OutputCommitResult Failed(AppError error) => new()
    {
        Success = false,
        Error = error
    };
}

public sealed record OutputCleanupResult
{
    public required bool Success { get; init; }
    public AppError? Error { get; init; }

    public static OutputCleanupResult Succeeded() => new() { Success = true };

    public static OutputCleanupResult Failed(AppError error) => new()
    {
        Success = false,
        Error = error
    };
}

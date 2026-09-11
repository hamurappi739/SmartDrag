using SmartDrag.Core.Errors;

namespace SmartDrag.Core.Artifacts;

/// <summary>
/// A file that SmartDrag successfully committed. Path ownership is historical only; destructive commands
/// must use a strong platform identity and perform verification on the same handle used for deletion.
/// </summary>
public sealed record GeneratedArtifact
{
    public required string Path { get; init; }
    public ArtifactIdentity? Identity { get; init; }

    public bool HasStrongIdentity => Identity is not null;
}

/// <summary>
/// Opaque, platform-defined identity. For Windows v1 this is volume serial number + 128-bit file id.
/// The scheme is versioned so identity semantics can evolve without silently reinterpreting old data.
/// </summary>
public sealed record ArtifactIdentity
{
    public required string Scheme { get; init; }
    public required string VolumeId { get; init; }
    public required string ObjectId { get; init; }
}

public sealed record ArtifactIdentityCaptureResult
{
    public required bool Success { get; init; }
    public ArtifactIdentity? Identity { get; init; }
    public AppError? Error { get; init; }

    public static ArtifactIdentityCaptureResult Captured(ArtifactIdentity identity) => new()
    {
        Success = true,
        Identity = identity
    };

    public static ArtifactIdentityCaptureResult Failed(AppError error) => new()
    {
        Success = false,
        Error = error
    };
}

public interface IGeneratedArtifactIdentityProvider
{
    Task<ArtifactIdentityCaptureResult> CaptureAsync(string path, CancellationToken cancellationToken);
}

public enum GeneratedArtifactDeletionStatus
{
    Deleted = 0,
    IdentityUnavailable,
    IdentityMismatch,
    FileMissing,
    UnsupportedIdentityScheme,
    Failed
}

public sealed record GeneratedArtifactDeletionResult
{
    public required GeneratedArtifactDeletionStatus Status { get; init; }
    public AppError? Error { get; init; }
    public bool Success => Status == GeneratedArtifactDeletionStatus.Deleted;

    public static GeneratedArtifactDeletionResult Deleted() => new()
    {
        Status = GeneratedArtifactDeletionStatus.Deleted
    };
}

/// <summary>
/// Strong destructive boundary. Implementations must verify identity and delete using the same opened object,
/// not verify by path and then call a separate path-based delete API.
/// </summary>
public interface IGeneratedArtifactDeletionService
{
    bool IsSupported { get; }
    Task<GeneratedArtifactDeletionResult> DeleteIfIdentityMatchesAsync(
        GeneratedArtifact artifact,
        CancellationToken cancellationToken);
}

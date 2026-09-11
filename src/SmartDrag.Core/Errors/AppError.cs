namespace SmartDrag.Core.Errors;

public sealed record AppError(ErrorCode Code, string TechnicalMessage, string UserMessage);

public enum ErrorCode
{
    Unknown = 0,
    SourceNotFound,
    SourceAccessDenied,
    UnsupportedFormat,
    DecodeFailed,
    EncodeFailed,
    DiskFull,
    OutputAccessDenied,
    OutputCollision,
    OperationCancelled,
    InvalidOutputPolicy,
    TemporaryOutputMissing,
    CleanupFailed,
    InvalidInput,
    RecoveryJournalUnavailable,
    RecoveryEntryInvalid,
    RecoveryCleanupFailed,
    CompletionCommandUnavailable,
    CompletionCommandFailed,
    ArtifactIdentityUnavailable,
    ArtifactIdentityMismatch,
    GeneratedArtifactDeleteFailed,
    ImageResourceLimitExceeded
}

namespace SmartDrag.Core.Payload;

/// <summary>
/// Unknown: insufficient evidence to decide safely.
/// Eligible: conservative pre-overlay evidence is good enough to show SmartDrag,
/// but the payload must still be authoritatively revalidated from OLE IDataObject on Drop.
/// Supported: authoritative drop payload is valid for the current MVP.
/// Rejected: evidence proves the payload should not be offered by the current MVP.
/// </summary>
public enum PayloadQualificationState
{
    Unknown = 0,
    Eligible,
    Supported,
    Rejected
}

public enum PayloadRejectionReason
{
    None = 0,
    EvidenceUnavailable,
    NotFiles,
    NoFiles,
    MultipleFiles,
    PathUnavailable,
    SourceMissing,
    DirectoryNotSupported,
    UnsupportedExtension,
    UnsupportedCategory,
    PreflightMismatch
}

public sealed record PayloadQualificationResult
{
    public required PayloadQualificationState State { get; init; }
    public required PayloadRejectionReason Reason { get; init; }
    public DragPayloadInfo? Payload { get; init; }
    public string? Detail { get; init; }

    public bool MayShowOverlay => State is PayloadQualificationState.Eligible or PayloadQualificationState.Supported;
    public bool IsAuthoritative => State == PayloadQualificationState.Supported;
}

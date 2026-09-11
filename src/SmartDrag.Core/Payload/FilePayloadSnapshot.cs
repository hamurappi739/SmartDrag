namespace SmartDrag.Core.Payload;

public sealed record FilePayloadSnapshot
{
    public required PayloadEvidenceSource EvidenceSource { get; init; }
    public required IReadOnlyList<FilePayloadCandidate> Files { get; init; }
    public string? EvidenceDetail { get; init; }
}

public sealed record FilePayloadCandidate
{
    public string? FullPath { get; init; }
    public string? DisplayName { get; init; }
    public string? Extension { get; init; }
    public bool Exists { get; init; }
    public bool IsDirectory { get; init; }
    public long? SizeBytes { get; init; }
}

public enum PayloadEvidenceSource
{
    Unknown = 0,
    ExplorerSelectionSnapshot,
    OleDataObject,
    AccessibilityEvent,
    LocalFilePicker
}

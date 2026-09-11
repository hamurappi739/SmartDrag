namespace SmartDrag.Core.Payload;

public sealed record DragPayloadInfo
{
    public required IReadOnlyList<DraggedFile> Files { get; init; }
    public required PayloadKind Kind { get; init; }
    public required bool IsSupported { get; init; }
    public string? RejectionReason { get; init; }
}

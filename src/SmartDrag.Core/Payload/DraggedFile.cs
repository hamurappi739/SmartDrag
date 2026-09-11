namespace SmartDrag.Core.Payload;

public sealed record DraggedFile
{
    public required string FullPath { get; init; }
    public string? Extension { get; init; }
    public long? SizeBytes { get; init; }
    public FileCategory Category { get; init; }
}

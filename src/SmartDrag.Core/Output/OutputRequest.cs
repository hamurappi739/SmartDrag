namespace SmartDrag.Core.Output;

public sealed record OutputRequest
{
    public required string SourcePath { get; init; }
    public required string Suffix { get; init; }
    public string? NewExtension { get; init; }
    public required OutputPolicy Policy { get; init; }
}

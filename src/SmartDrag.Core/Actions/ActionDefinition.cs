using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Actions;

public sealed record ActionDefinition
{
    public required ActionId Id { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlySet<FileCategory> SupportedInputTypes { get; init; }
    public required bool ExecutesLocally { get; init; }
    public required OutputKind OutputType { get; init; }
    public required bool CanRunInPlace { get; init; }
    public required bool CanBatch { get; init; }
    public required string IconId { get; init; }
}

public enum OutputKind
{
    File = 0,
    Files,
    Clipboard,
    None
}

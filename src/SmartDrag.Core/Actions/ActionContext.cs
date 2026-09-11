using SmartDrag.Core.Payload;
using SmartDrag.Core.Settings;

namespace SmartDrag.Core.Actions;

public sealed record ActionContext
{
    public required DragPayloadInfo Payload { get; init; }
    public required AppCapabilities Capabilities { get; init; }
    public required UserSettings Settings { get; init; }
}

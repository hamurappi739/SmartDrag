using SmartDrag.Core.Output;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Actions;

public sealed record ActionRequest
{
    /// <summary>
    /// Stable idempotency key for one logical user-authorized action. In the drag MVP this is the DragSessionId,
    /// because one physical drag may authorize at most one logical SmartDrag action.
    /// </summary>
    public required Guid RequestId { get; init; }
    public required ActionId ActionId { get; init; }
    public required IReadOnlyList<string> InputPaths { get; init; }
    public required OutputPolicy OutputPolicy { get; init; }
}

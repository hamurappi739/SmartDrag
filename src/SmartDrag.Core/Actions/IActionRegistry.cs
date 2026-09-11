using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Actions;

public interface IActionRegistry
{
    IReadOnlyList<ActionDefinition> GetAvailable(ActionContext context);
    bool TryGet(ActionId id, out ActionDefinition definition);
}

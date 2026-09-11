using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Actions;

public interface IActionHandler
{
    ActionId ActionId { get; }
    Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken);
}

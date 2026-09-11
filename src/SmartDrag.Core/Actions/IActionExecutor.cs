namespace SmartDrag.Core.Actions;

public interface IActionExecutor
{
    Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken);
}

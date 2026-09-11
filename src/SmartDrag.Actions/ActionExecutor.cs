using SmartDrag.Core.Actions;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Actions;

public sealed class ActionExecutor : IActionExecutor
{
    private readonly IReadOnlyDictionary<ActionId, IActionHandler> _handlers;

    public ActionExecutor(IEnumerable<IActionHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var dictionary = new Dictionary<ActionId, IActionHandler>();
        foreach (var handler in handlers)
        {
            if (!dictionary.TryAdd(handler.ActionId, handler))
            {
                throw new ArgumentException($"Duplicate action handler for ActionId '{handler.ActionId}'.", nameof(handlers));
            }
        }

        _handlers = dictionary;
    }

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_handlers.TryGetValue(request.ActionId, out var handler))
        {
            return ActionResult.Failed(new AppError(
                ErrorCode.Unknown,
                $"No handler registered for ActionId '{request.ActionId}'.",
                "This action is not available."));
        }

        try
        {
            return await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ActionResult.Failed(new AppError(
                ErrorCode.OperationCancelled,
                $"Action '{request.ActionId}' was cancelled.",
                "The operation was cancelled."));
        }
        catch (Exception ex)
        {
            return ActionResult.Failed(new AppError(
                ErrorCode.Unknown,
                $"Unhandled action handler exception for '{request.ActionId}': {ex}",
                "The operation failed."));
        }
    }
}

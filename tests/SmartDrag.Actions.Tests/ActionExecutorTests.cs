using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Output;
using SmartDrag.Core.Primitives;
using Xunit;

namespace SmartDrag.Actions.Tests;

public sealed class ActionExecutorTests
{
    [Fact]
    public async Task Unknown_action_returns_failure_instead_of_throwing()
    {
        var executor = new ActionExecutor(Array.Empty<IActionHandler>());

        var result = await executor.ExecuteAsync(Request("missing.action"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Unknown, result.Error?.Code);
    }

    [Fact]
    public async Task Unexpected_handler_exception_is_contained()
    {
        var executor = new ActionExecutor(new[]
        {
            new ThrowingHandler(new ActionId("image.compress"))
        });

        var result = await executor.ExecuteAsync(Request("image.compress"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.Unknown, result.Error?.Code);
    }

    [Fact]
    public void Duplicate_handlers_are_rejected_at_composition_time()
    {
        var id = new ActionId("image.compress");

        Assert.Throws<ArgumentException>(() => new ActionExecutor(new IActionHandler[]
        {
            new ThrowingHandler(id),
            new ThrowingHandler(id)
        }));
    }

    private static ActionRequest Request(string id) => new()
    {
        RequestId = Guid.NewGuid(),
        ActionId = new ActionId(id),
        InputPaths = new[] { "input.png" },
        OutputPolicy = OutputPolicy.SafeMvpDefault
    };

    private sealed class ThrowingHandler(ActionId id) : IActionHandler
    {
        public ActionId ActionId { get; } = id;

        public Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}

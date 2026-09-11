using Xunit;
using SmartDrag.Windows.Interop;
using SmartDrag.Windows.Ole;

namespace SmartDrag.Windows.Tests;

public sealed class DropEffectPolicyTests
{
    [Fact]
    public void CopyAllowed_ValidPayloadAndAction_ReturnsCopy()
    {
        var allowed = NativeConstants.DROPEFFECT_COPY | NativeConstants.DROPEFFECT_MOVE;
        var result = DropEffectPolicy.SelectNonDestructiveCopy(allowed, payloadValid: true, actionValid: true);

        Assert.Equal(NativeConstants.DROPEFFECT_COPY, result);
    }

    [Fact]
    public void OnlyMoveAllowed_ReturnsNone()
    {
        var result = DropEffectPolicy.SelectNonDestructiveCopy(
            NativeConstants.DROPEFFECT_MOVE,
            payloadValid: true,
            actionValid: true);

        Assert.Equal(NativeConstants.DROPEFFECT_NONE, result);
    }

    [Fact]
    public void InvalidPayload_ReturnsNone()
    {
        var result = DropEffectPolicy.SelectNonDestructiveCopy(
            NativeConstants.DROPEFFECT_COPY,
            payloadValid: false,
            actionValid: true);

        Assert.Equal(NativeConstants.DROPEFFECT_NONE, result);
    }
}

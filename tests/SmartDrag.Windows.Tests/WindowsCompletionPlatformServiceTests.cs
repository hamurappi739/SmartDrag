using SmartDrag.Windows.Completion;
using Xunit;

namespace SmartDrag.Windows.Tests;

public sealed class WindowsCompletionPlatformServiceTests
{
    [Fact]
    public void ClipboardCapability_IsSuppressedWithoutOwnerWindow()
    {
        var service = new WindowsCompletionPlatformService();

        Assert.True(service.Capabilities.CanOpenContainingFolder);
        Assert.False(service.Capabilities.CanCopyResultPath);
        Assert.False(service.Capabilities.CanStartResultDrag);
        Assert.False(service.Capabilities.CanDeleteGeneratedOutput);
    }

    [Fact]
    public void ClipboardCapability_RequiresExplicitNonZeroOwnerWindow()
    {
        var service = new WindowsCompletionPlatformService(new IntPtr(123));

        Assert.True(service.Capabilities.CanCopyResultPath);
    }
}

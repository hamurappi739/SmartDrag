using SmartDrag.Core.Actions;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Primitives;
using Xunit;

namespace SmartDrag.Core.Tests;

public sealed class OverlayHitTestPolicyTests
{
    [Fact]
    public void CenterOfEnabledAction_ResolvesThatAction()
    {
        var compress = BuiltInActionIds.CompressImage;
        var removeMetadata = BuiltInActionIds.RemoveImageMetadata;
        var targets = new[]
        {
            new OverlayHitTarget(compress, new RectD(12, 16, 212, 58)),
            new OverlayHitTarget(removeMetadata, new RectD(12, 64, 212, 106))
        };

        var result = OverlayHitTestPolicy.Resolve(new PointD(112, 85), targets);

        Assert.Equal(removeMetadata, result);
    }

    [Fact]
    public void DecorativeOrDisabledArea_DoesNotResolveAnAction()
    {
        var targets = new[]
        {
            new OverlayHitTarget(BuiltInActionIds.CompressImage, new RectD(12, 16, 212, 58), IsEnabled: false)
        };

        Assert.Null(OverlayHitTestPolicy.Resolve(new PointD(20, 20), targets));
        Assert.Null(OverlayHitTestPolicy.Resolve(new PointD(300, 300), targets));
    }

    [Fact]
    public void FirstMatchingActionWinsForOverlappingGeometry()
    {
        var first = new ActionId("first");
        var second = new ActionId("second");
        var result = OverlayHitTestPolicy.Resolve(
            new PointD(50, 50),
            new[]
            {
                new OverlayHitTarget(first, new RectD(0, 0, 100, 100)),
                new OverlayHitTarget(second, new RectD(0, 0, 100, 100))
            });

        Assert.Equal(first, result);
    }

    [Fact]
    public void InvalidGeometry_IsIgnored()
    {
        var target = new OverlayHitTarget(
            BuiltInActionIds.CompressImage,
            new RectD(double.NaN, 0, 10, 10));

        Assert.Null(OverlayHitTestPolicy.Resolve(new PointD(1, 1), new[] { target }));
    }
}

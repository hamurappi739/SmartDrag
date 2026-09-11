using SmartDrag.Core.Primitives;
using SmartDrag.Presentation;
using Xunit;

namespace SmartDrag.Presentation.Tests;

public sealed class PreviewWindowPlacementPolicyTests
{
    private static readonly RectD WorkArea = new(0, 0, 1920, 1080);

    [Fact]
    public void MissingPlacement_IsCenteredWithDefaults()
    {
        var placement = PreviewWindowPlacementPolicy.Resolve(null, WorkArea);

        Assert.Equal(470, placement.Left);
        Assert.Equal(190, placement.Top);
        Assert.Equal(980, placement.Width);
        Assert.Equal(700, placement.Height);
    }

    [Fact]
    public void OffscreenPlacement_IsCenteredOnAvailableMonitor()
    {
        var placement = PreviewWindowPlacementPolicy.Resolve(new PreviewWindowBounds(-4000, 3000, 980, 700), WorkArea);

        Assert.Equal(470, placement.Left);
        Assert.Equal(190, placement.Top);
    }

    [Fact]
    public void PartiallyVisiblePlacement_IsClampedToKeepQuarterVisible()
    {
        var placement = PreviewWindowPlacementPolicy.Resolve(new PreviewWindowBounds(1800, 1000, 980, 700), WorkArea);

        Assert.Equal(1675, placement.Left);
        Assert.Equal(905, placement.Top);
        Assert.True(placement.Right > WorkArea.Right - placement.Width * 0.25);
        Assert.True(placement.Bottom > WorkArea.Bottom - placement.Height * 0.25);
    }

    [Fact]
    public void OversizedPlacement_IsReducedToWorkArea()
    {
        var placement = PreviewWindowPlacementPolicy.Resolve(new PreviewWindowBounds(0, 0, 4000, 4000), WorkArea);

        Assert.Equal(1920, placement.Width);
        Assert.Equal(1080, placement.Height);
        Assert.Equal(0, placement.Left);
        Assert.Equal(0, placement.Top);
    }
}

using SmartDrag.Core.Overlay;
using SmartDrag.Core.Primitives;
using Xunit;

namespace SmartDrag.Core.Tests;

public sealed class OverlayPlacementEngineTests
{
    [Fact]
    public void PrefersBottomRightWhenSpaceExists()
    {
        var placement = OverlayPlacementEngine.Calculate(
            new PointD(100, 100),
            new RectD(0, 0, 1920, 1080),
            new SizeD(320, 160),
            28);

        Assert.Equal(128, placement.X);
        Assert.Equal(128, placement.Y);
    }

    [Fact]
    public void FlipsLeftAndUpNearBottomRightEdge()
    {
        var placement = OverlayPlacementEngine.Calculate(
            new PointD(1900, 1060),
            new RectD(0, 0, 1920, 1080),
            new SizeD(320, 160),
            28);

        Assert.Equal(1552, placement.X);
        Assert.Equal(872, placement.Y);
    }

    [Fact]
    public void SupportsNegativeVirtualDesktopCoordinates()
    {
        var placement = OverlayPlacementEngine.Calculate(
            new PointD(-2500, 100),
            new RectD(-2560, 0, 0, 1440),
            new SizeD(320, 160),
            28);

        Assert.Equal(-2472, placement.X);
        Assert.Equal(128, placement.Y);
    }

    [Fact]
    public void ClampsOversizedOverlayToWorkAreaOrigin()
    {
        var placement = OverlayPlacementEngine.Calculate(
            new PointD(10, 10),
            new RectD(0, 0, 100, 100),
            new SizeD(200, 200),
            28);

        Assert.Equal(0, placement.X);
        Assert.Equal(0, placement.Y);
    }
}

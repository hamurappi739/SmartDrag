using SmartDrag.Presentation;
using Xunit;

namespace SmartDrag.Presentation.Tests;

public sealed class PreviewThemePaletteTests
{
    [Fact]
    public void LightAndDarkPalettes_ExposeTheSameTokenSet()
    {
        var light = typeof(PreviewThemePalette).GetProperties()
            .Where(property => property.PropertyType == typeof(ThemeColor))
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();
        var dark = typeof(PreviewThemePalette).GetProperties()
            .Where(property => property.PropertyType == typeof(ThemeColor))
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(light, dark);
        Assert.Equal(15, light.Length);
    }

    [Fact]
    public void Palette_UsesAppleReferenceAccentAndSurfaces()
    {
        Assert.Equal(new ThemeColor(0, 122, 255), PreviewThemePalette.Light.Accent);
        Assert.Equal(new ThemeColor(10, 132, 255), PreviewThemePalette.Dark.Accent);
        Assert.Equal(new ThemeColor(245, 245, 247), PreviewThemePalette.Light.WindowBackground);
        Assert.Equal(new ThemeColor(28, 28, 30), PreviewThemePalette.Dark.WindowBackground);
    }

    [Fact]
    public void PrimaryTextContrast_RemainsAccessibleAgainstWindowBackground()
    {
        Assert.True(ThemeColor.ContrastRatio(PreviewThemePalette.Light.TextPrimary, PreviewThemePalette.Light.WindowBackground) >= 4.5);
        Assert.True(ThemeColor.ContrastRatio(PreviewThemePalette.Dark.TextPrimary, PreviewThemePalette.Dark.WindowBackground) >= 4.5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void For_ReturnsMatchingPalette(bool dark)
    {
        Assert.Same(dark ? PreviewThemePalette.Dark : PreviewThemePalette.Light, PreviewThemePalette.For(dark));
    }
}

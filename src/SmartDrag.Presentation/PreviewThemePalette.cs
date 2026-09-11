namespace SmartDrag.Presentation;

/// <summary>
/// Shared visual vocabulary for the Preview surface. The App adapter converts these toolkit-neutral RGB tokens to
/// WPF brushes; keeping the values here makes light/dark parity testable without loading a WPF window.
/// </summary>
public sealed record PreviewThemePalette
{
    public required ThemeColor WindowBackground { get; init; }
    public required ThemeColor SurfaceCard { get; init; }
    public required ThemeColor SurfaceElevated { get; init; }
    public required ThemeColor Separator { get; init; }
    public required ThemeColor SeparatorStrong { get; init; }
    public required ThemeColor TextPrimary { get; init; }
    public required ThemeColor TextSecondary { get; init; }
    public required ThemeColor TextTertiary { get; init; }
    public required ThemeColor Accent { get; init; }
    public required ThemeColor Success { get; init; }
    public required ThemeColor Error { get; init; }
    public required ThemeColor Warning { get; init; }
    public required ThemeColor DropZoneHover { get; init; }
    public required ThemeColor CancelSurface { get; init; }
    public required ThemeColor CancelBorder { get; init; }

    public static PreviewThemePalette Light { get; } = new()
    {
        WindowBackground = new(245, 245, 247),
        SurfaceCard = new(255, 255, 255),
        SurfaceElevated = new(255, 255, 255),
        Separator = new(229, 229, 234),
        SeparatorStrong = new(198, 198, 200),
        TextPrimary = new(29, 29, 31),
        TextSecondary = new(110, 110, 115),
        TextTertiary = new(174, 174, 178),
        Accent = new(0, 122, 255),
        Success = new(52, 199, 89),
        Error = new(255, 59, 48),
        Warning = new(255, 149, 0),
        DropZoneHover = new(242, 247, 255),
        CancelSurface = new(255, 242, 241),
        CancelBorder = new(255, 205, 200)
    };

    public static PreviewThemePalette Dark { get; } = new()
    {
        WindowBackground = new(28, 28, 30),
        SurfaceCard = new(44, 44, 46),
        SurfaceElevated = new(58, 58, 60),
        Separator = new(61, 61, 64),
        SeparatorStrong = new(95, 95, 98),
        TextPrimary = new(245, 245, 247),
        TextSecondary = new(152, 152, 157),
        TextTertiary = new(99, 99, 102),
        Accent = new(10, 132, 255),
        Success = new(48, 209, 88),
        Error = new(255, 69, 58),
        Warning = new(255, 159, 10),
        DropZoneHover = new(36, 59, 88),
        CancelSurface = new(68, 36, 38),
        CancelBorder = new(130, 55, 58)
    };

    public static PreviewThemePalette For(bool darkTheme) => darkTheme ? Dark : Light;
}

public readonly record struct ThemeColor(byte Red, byte Green, byte Blue)
{
    public double RelativeLuminance()
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.03928 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(Red) + 0.7152 * Channel(Green) + 0.0722 * Channel(Blue);
    }

    public static double ContrastRatio(ThemeColor first, ThemeColor second)
    {
        var lighter = Math.Max(first.RelativeLuminance(), second.RelativeLuminance());
        var darker = Math.Min(first.RelativeLuminance(), second.RelativeLuminance());
        return (lighter + 0.05) / (darker + 0.05);
    }
}

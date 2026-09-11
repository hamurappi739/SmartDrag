using SmartDrag.Core.Primitives;

namespace SmartDrag.Presentation;

/// <summary>
/// Toolkit-neutral placement rules for the Preview window. Persisted coordinates are untrusted input: the
/// result must remain usable when a monitor disappears or a preferences file was edited manually.
/// </summary>
public static class PreviewWindowPlacementPolicy
{
    public const double DefaultWidth = 980;
    public const double DefaultHeight = 700;
    public const double MinimumWidth = 720;
    public const double MinimumHeight = 480;
    public const double MinimumVisibleFraction = 0.25;

    public static PreviewWindowBounds Resolve(
        PreviewWindowBounds? persisted,
        RectD workArea,
        double defaultWidth = DefaultWidth,
        double defaultHeight = DefaultHeight,
        double minimumWidth = MinimumWidth,
        double minimumHeight = MinimumHeight)
    {
        if (!double.IsFinite(workArea.Left) || !double.IsFinite(workArea.Top) ||
            !double.IsFinite(workArea.Right) || !double.IsFinite(workArea.Bottom) ||
            workArea.Width <= 0 || workArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea), "Work area must be finite and positive.");
        }

        var width = ClampDimension(persisted?.Width ?? defaultWidth, minimumWidth, workArea.Width);
        var height = ClampDimension(persisted?.Height ?? defaultHeight, minimumHeight, workArea.Height);
        var left = persisted?.Left ?? workArea.Left + (workArea.Width - width) / 2;
        var top = persisted?.Top ?? workArea.Top + (workArea.Height - height) / 2;

        var candidate = new PreviewWindowBounds(left, top, width, height);
        return HasAnyVisibleArea(candidate, workArea)
            ? ClampToVisibleArea(candidate, workArea)
            : new PreviewWindowBounds(
                workArea.Left + (workArea.Width - width) / 2,
                workArea.Top + (workArea.Height - height) / 2,
                width,
                height);
    }

    private static double ClampDimension(double value, double minimum, double available)
    {
        if (!double.IsFinite(value))
        {
            value = minimum;
        }

        return Math.Clamp(value, Math.Min(minimum, available), available);
    }

    private static bool HasAnyVisibleArea(PreviewWindowBounds window, RectD workArea)
    {
        var overlapWidth = Math.Max(0, Math.Min(window.Right, workArea.Right) - Math.Max(window.Left, workArea.Left));
        var overlapHeight = Math.Max(0, Math.Min(window.Bottom, workArea.Bottom) - Math.Max(window.Top, workArea.Top));
        return overlapWidth > 0 && overlapHeight > 0;
    }

    private static PreviewWindowBounds ClampToVisibleArea(PreviewWindowBounds window, RectD workArea)
    {
        var minimumLeft = workArea.Left - window.Width * (1 - MinimumVisibleFraction);
        var maximumLeft = workArea.Right - window.Width * MinimumVisibleFraction;
        var minimumTop = workArea.Top - window.Height * (1 - MinimumVisibleFraction);
        var maximumTop = workArea.Bottom - window.Height * MinimumVisibleFraction;
        return window with
        {
            Left = Math.Clamp(window.Left, minimumLeft, maximumLeft),
            Top = Math.Clamp(window.Top, minimumTop, maximumTop)
        };
    }
}

public readonly record struct PreviewWindowBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

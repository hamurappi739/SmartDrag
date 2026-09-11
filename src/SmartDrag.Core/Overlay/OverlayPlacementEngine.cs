using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Overlay;

/// <summary>
/// Unit-agnostic placement math. Callers must pass all values in the same coordinate space
/// (physical pixels in the Win32 proof; DIPs may be used by a future renderer after conversion).
/// </summary>
public static class OverlayPlacementEngine
{
    public static OverlayPlacement Calculate(
        PointD cursor,
        RectD workArea,
        SizeD overlaySize,
        double cursorOffset)
    {
        if (overlaySize.Width <= 0 || overlaySize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overlaySize), "Overlay dimensions must be positive.");
        }

        var x = cursor.X + cursorOffset;
        var y = cursor.Y + cursorOffset;

        if (x + overlaySize.Width > workArea.Right)
        {
            x = cursor.X - cursorOffset - overlaySize.Width;
        }

        if (y + overlaySize.Height > workArea.Bottom)
        {
            y = cursor.Y - cursorOffset - overlaySize.Height;
        }

        var maxX = Math.Max(workArea.Left, workArea.Right - overlaySize.Width);
        var maxY = Math.Max(workArea.Top, workArea.Bottom - overlaySize.Height);
        x = Math.Clamp(x, workArea.Left, maxX);
        y = Math.Clamp(y, workArea.Top, maxY);

        return new OverlayPlacement(x, y, overlaySize.Width, overlaySize.Height);
    }
}

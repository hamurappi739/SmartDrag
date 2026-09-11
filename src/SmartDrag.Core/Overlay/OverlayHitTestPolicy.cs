using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Overlay;

/// <summary>
/// Resolves an overlay action from explicit button geometry. Decorative containers are intentionally absent from
/// this model, so hit-testing cannot accidentally treat a dimmer/grid as an actionable control.
/// </summary>
public static class OverlayHitTestPolicy
{
    public static ActionId? Resolve(PointD point, IEnumerable<OverlayHitTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        foreach (var target in targets)
        {
            if (!target.IsEnabled || !IsFinite(target.Bounds) || !Contains(target.Bounds, point))
            {
                continue;
            }

            return target.ActionId;
        }

        return null;
    }

    private static bool IsFinite(RectD bounds) =>
        double.IsFinite(bounds.Left) && double.IsFinite(bounds.Top) &&
        double.IsFinite(bounds.Right) && double.IsFinite(bounds.Bottom) &&
        bounds.Width > 0 && bounds.Height > 0;

    private static bool Contains(RectD bounds, PointD point) =>
        point.X >= bounds.Left && point.X <= bounds.Right &&
        point.Y >= bounds.Top && point.Y <= bounds.Bottom;
}

public readonly record struct OverlayHitTarget(ActionId ActionId, RectD Bounds, bool IsEnabled = true);

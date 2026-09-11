namespace SmartDrag.Core.Primitives;

public readonly record struct PointD(double X, double Y)
{
    public double DistanceTo(PointD other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

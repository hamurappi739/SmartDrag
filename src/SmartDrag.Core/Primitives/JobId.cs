namespace SmartDrag.Core.Primitives;

public readonly record struct JobId(Guid Value)
{
    public static JobId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

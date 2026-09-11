namespace SmartDrag.Core.Primitives;

public readonly record struct ActionId
{
    public ActionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator string(ActionId id) => id.Value;
}

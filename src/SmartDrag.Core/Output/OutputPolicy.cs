namespace SmartDrag.Core.Output;

public sealed record OutputPolicy
{
    public required OutputLocationMode LocationMode { get; init; }
    public required CollisionPolicy CollisionPolicy { get; init; }
    public required bool PreserveSource { get; init; }
    public string? ConfiguredDirectoryPath { get; init; }

    public static OutputPolicy SafeMvpDefault { get; } = new()
    {
        LocationMode = OutputLocationMode.SameDirectory,
        CollisionPolicy = CollisionPolicy.GenerateUniqueName,
        PreserveSource = true
    };
}

public enum OutputLocationMode
{
    SameDirectory = 0,
    ConfiguredDirectory
}

public enum CollisionPolicy
{
    GenerateUniqueName = 0,
    Fail
}

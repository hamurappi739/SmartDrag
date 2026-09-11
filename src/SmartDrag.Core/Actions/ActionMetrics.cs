namespace SmartDrag.Core.Actions;

public sealed record ActionMetrics
{
    public long? SourceSizeBytes { get; init; }
    public long? OutputSizeBytes { get; init; }

    public long? BytesSaved => SourceSizeBytes is { } source && OutputSizeBytes is { } output
        ? source - output
        : null;

    public double? ReductionRatio => SourceSizeBytes is > 0 && OutputSizeBytes is { } output
        ? 1.0 - ((double)output / SourceSizeBytes.Value)
        : null;
}

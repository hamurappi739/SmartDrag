namespace SmartDrag.Core.Settings;

public sealed record UserSettings
{
    public DragBehaviorSettings Drag { get; init; } = new();
    public OutputSettings Output { get; init; } = new();
    public OverlaySettings Overlay { get; init; } = new();
}

public sealed record DragBehaviorSettings
{
    public int ActivationDelayMs { get; init; } = 180;
    public double MinimumTravelDip { get; init; } = 12;
}

public sealed record OutputSettings
{
    public bool PreserveSource { get; init; } = true;
}

public sealed record OverlaySettings
{
    public double CursorOffsetDip { get; init; } = 28;
}

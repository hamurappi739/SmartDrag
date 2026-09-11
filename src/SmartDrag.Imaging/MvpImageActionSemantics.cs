using SmartDrag.Core.Actions;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Imaging;

public enum VisualOrientationPolicy
{
    PreserveVisualOrientation = 0,
    NormalizePixelsBeforeRemovingOrientationMetadata
}

public enum ColorProfilePolicy
{
    PreserveWhenRepresentable = 0
}

public enum NonVisualMetadataPolicy
{
    Preserve = 0,
    Remove
}

public enum AnimationPolicy
{
    Reject = 0
}

public enum CompressionIntent
{
    FormatAppropriateBalanced = 0,
    WebPBalanced
}

/// <summary>
/// Observable product semantics. Exact encoder quality/effort numbers are intentionally not embedded here;
/// those are selected by G3 corpus/benchmark evidence and can change without redefining the user-facing action.
/// </summary>
public sealed record ImageActionSemantics
{
    public required ActionId ActionId { get; init; }
    public required VisualOrientationPolicy Orientation { get; init; }
    public required ColorProfilePolicy ColorProfile { get; init; }
    public required NonVisualMetadataPolicy Metadata { get; init; }
    public required AnimationPolicy Animation { get; init; }
    public required CompressionIntent Compression { get; init; }
    public bool PreservePixelDimensions { get; init; } = true;
}

public static class MvpImageActionSemantics
{
    public static ImageActionSemantics Compress { get; } = new()
    {
        ActionId = BuiltInActionIds.CompressImage,
        Orientation = VisualOrientationPolicy.PreserveVisualOrientation,
        ColorProfile = ColorProfilePolicy.PreserveWhenRepresentable,
        Metadata = NonVisualMetadataPolicy.Preserve,
        Animation = AnimationPolicy.Reject,
        Compression = CompressionIntent.FormatAppropriateBalanced
    };

    public static ImageActionSemantics ConvertToWebP { get; } = new()
    {
        ActionId = BuiltInActionIds.ConvertImageToWebP,
        Orientation = VisualOrientationPolicy.PreserveVisualOrientation,
        ColorProfile = ColorProfilePolicy.PreserveWhenRepresentable,
        Metadata = NonVisualMetadataPolicy.Preserve,
        Animation = AnimationPolicy.Reject,
        Compression = CompressionIntent.WebPBalanced
    };

    public static ImageActionSemantics RemoveMetadata { get; } = new()
    {
        ActionId = BuiltInActionIds.RemoveImageMetadata,
        Orientation = VisualOrientationPolicy.NormalizePixelsBeforeRemovingOrientationMetadata,
        ColorProfile = ColorProfilePolicy.PreserveWhenRepresentable,
        Metadata = NonVisualMetadataPolicy.Remove,
        Animation = AnimationPolicy.Reject,
        Compression = CompressionIntent.FormatAppropriateBalanced
    };

    public static ImageActionSemantics For(ActionId actionId)
    {
        if (actionId == Compress.ActionId) return Compress;
        if (actionId == ConvertToWebP.ActionId) return ConvertToWebP;
        if (actionId == RemoveMetadata.ActionId) return RemoveMetadata;
        throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "No MVP image semantics exist for this ActionId.");
    }
}

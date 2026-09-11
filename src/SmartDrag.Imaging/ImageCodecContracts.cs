using SmartDrag.Core.Errors;

namespace SmartDrag.Imaging;

public enum ImageFormatKind
{
    Unknown = 0,
    Jpeg,
    Png,
    WebP
}

/// <summary>
/// Cheap decoded-header/metadata facts used by the execution gate before allocating full pixel buffers.
/// A codec adapter must populate these from file contents, never from extension alone.
/// </summary>
public sealed record ImageInspectionResult
{
    public required bool Success { get; init; }
    public required ImageFormatKind Format { get; init; }
    public long? SourceSizeBytes { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int FrameCount { get; init; } = 1;
    public bool HasAlpha { get; init; }
    public int? ExifOrientation { get; init; }
    public bool HasExif { get; init; }
    public bool HasXmp { get; init; }
    public bool HasIptc { get; init; }
    public bool HasIccProfile { get; init; }
    public AppError? Error { get; init; }
}

public interface IImageInspector
{
    Task<ImageInspectionResult> InspectAsync(string sourcePath, CancellationToken cancellationToken);
}

/// <summary>
/// Resource limits are deliberately supplied by composition/configuration rather than hidden inside a codec.
/// Concrete MVP values remain a G3 benchmark decision.
/// </summary>
public sealed record ImageSafetyLimits
{
    public required long MaxSourceBytes { get; init; }
    public required long MaxDecodedPixels { get; init; }
    public required int MaxDimension { get; init; }
}

public enum ImageExecutionRejectionReason
{
    None = 0,
    InspectionFailed,
    UnsupportedInputFormat,
    AnimatedInputNotSupported,
    InvalidDimensions,
    SourceTooLarge,
    DimensionTooLarge,
    DecodedPixelCountTooLarge
}

public sealed record ImageExecutionGuardDecision
{
    public required bool Allowed { get; init; }
    public required ImageExecutionRejectionReason Reason { get; init; }
    public string? Detail { get; init; }
}

public static class MvpImageExecutionGuard
{
    public static ImageExecutionGuardDecision Evaluate(ImageInspectionResult inspection, ImageSafetyLimits limits)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        ArgumentNullException.ThrowIfNull(limits);

        if (limits.MaxSourceBytes <= 0 || limits.MaxDecodedPixels <= 0 || limits.MaxDimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "All image safety limits must be positive.");
        }

        if (!inspection.Success)
        {
            return Reject(ImageExecutionRejectionReason.InspectionFailed, "Codec/header inspection did not succeed.");
        }

        if (inspection.Format is not (ImageFormatKind.Jpeg or ImageFormatKind.Png))
        {
            return Reject(ImageExecutionRejectionReason.UnsupportedInputFormat, $"Input format '{inspection.Format}' is outside the single-image MVP.");
        }

        if (inspection.FrameCount != 1)
        {
            return Reject(ImageExecutionRejectionReason.AnimatedInputNotSupported, $"MVP requires exactly one frame; inspection reported {inspection.FrameCount}.");
        }

        if (inspection.SourceSizeBytes is { } sourceSize && sourceSize > limits.MaxSourceBytes)
        {
            return Reject(ImageExecutionRejectionReason.SourceTooLarge, "Source size exceeds the configured execution limit.");
        }

        if (inspection.Width is not { } width || inspection.Height is not { } height || width <= 0 || height <= 0)
        {
            return Reject(ImageExecutionRejectionReason.InvalidDimensions, "Image dimensions are missing or invalid.");
        }

        if (width > limits.MaxDimension || height > limits.MaxDimension)
        {
            return Reject(ImageExecutionRejectionReason.DimensionTooLarge, "At least one dimension exceeds the configured limit.");
        }

        long pixels;
        try
        {
            pixels = checked((long)width * height);
        }
        catch (OverflowException)
        {
            return Reject(ImageExecutionRejectionReason.DecodedPixelCountTooLarge, "Decoded pixel count overflowed the guard calculation.");
        }

        if (pixels > limits.MaxDecodedPixels)
        {
            return Reject(ImageExecutionRejectionReason.DecodedPixelCountTooLarge, "Decoded pixel count exceeds the configured limit.");
        }

        return new ImageExecutionGuardDecision
        {
            Allowed = true,
            Reason = ImageExecutionRejectionReason.None
        };
    }

    private static ImageExecutionGuardDecision Reject(ImageExecutionRejectionReason reason, string detail) => new()
    {
        Allowed = false,
        Reason = reason,
        Detail = detail
    };
}

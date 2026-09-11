using SmartDrag.Imaging;
using Xunit;

namespace SmartDrag.Imaging.Tests;

public sealed class MvpImageExecutionGuardTests
{
    private static readonly ImageSafetyLimits Limits = new()
    {
        MaxSourceBytes = 100_000_000,
        MaxDecodedPixels = 50_000_000,
        MaxDimension = 20_000
    };

    [Fact]
    public void StaticJpegWithinLimits_IsAllowed()
    {
        var decision = MvpImageExecutionGuard.Evaluate(Inspection(ImageFormatKind.Jpeg, 6000, 4000), Limits);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void ExtensionCannotMakeUnknownCodecFormatSupported()
    {
        var decision = MvpImageExecutionGuard.Evaluate(Inspection(ImageFormatKind.Unknown, 100, 100), Limits);
        Assert.False(decision.Allowed);
        Assert.Equal(ImageExecutionRejectionReason.UnsupportedInputFormat, decision.Reason);
    }

    [Fact]
    public void MultipleFrames_AreRejected()
    {
        var decision = MvpImageExecutionGuard.Evaluate(Inspection(ImageFormatKind.Png, 100, 100) with { FrameCount = 2 }, Limits);
        Assert.False(decision.Allowed);
        Assert.Equal(ImageExecutionRejectionReason.AnimatedInputNotSupported, decision.Reason);
    }

    [Fact]
    public void DecodedPixelBomb_IsRejectedBeforeFullProcessing()
    {
        var decision = MvpImageExecutionGuard.Evaluate(Inspection(ImageFormatKind.Png, 10_000, 10_000), Limits);
        Assert.False(decision.Allowed);
        Assert.Equal(ImageExecutionRejectionReason.DecodedPixelCountTooLarge, decision.Reason);
    }

    [Fact]
    public void OversizedDimension_IsRejected()
    {
        var decision = MvpImageExecutionGuard.Evaluate(Inspection(ImageFormatKind.Jpeg, 20_001, 10), Limits);
        Assert.False(decision.Allowed);
        Assert.Equal(ImageExecutionRejectionReason.DimensionTooLarge, decision.Reason);
    }

    [Fact]
    public void FailedInspection_IsRejected()
    {
        var decision = MvpImageExecutionGuard.Evaluate(new ImageInspectionResult
        {
            Success = false,
            Format = ImageFormatKind.Unknown
        }, Limits);
        Assert.False(decision.Allowed);
        Assert.Equal(ImageExecutionRejectionReason.InspectionFailed, decision.Reason);
    }

    [Fact]
    public void RemoveMetadata_SemanticsRequireOrientationNormalizationAndIccPreservation()
    {
        Assert.Equal(VisualOrientationPolicy.NormalizePixelsBeforeRemovingOrientationMetadata, MvpImageActionSemantics.RemoveMetadata.Orientation);
        Assert.Equal(ColorProfilePolicy.PreserveWhenRepresentable, MvpImageActionSemantics.RemoveMetadata.ColorProfile);
        Assert.Equal(NonVisualMetadataPolicy.Remove, MvpImageActionSemantics.RemoveMetadata.Metadata);
    }

    private static ImageInspectionResult Inspection(ImageFormatKind format, int width, int height) => new()
    {
        Success = true,
        Format = format,
        Width = width,
        Height = height,
        FrameCount = 1,
        SourceSizeBytes = 10_000
    };
}

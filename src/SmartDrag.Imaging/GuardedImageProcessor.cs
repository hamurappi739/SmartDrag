using SmartDrag.Core.Errors;

namespace SmartDrag.Imaging;

/// <summary>
/// Mandatory production decorator between action handlers and a concrete codec. It performs content-based
/// inspection and resource-limit evaluation before any full decode/encode operation is delegated.
/// </summary>
public sealed class GuardedImageProcessor : IImageProcessor
{
    private readonly IImageInspector _inspector;
    private readonly IImageProcessor _inner;
    private readonly ImageSafetyLimits _limits;

    public GuardedImageProcessor(IImageInspector inspector, IImageProcessor inner, ImageSafetyLimits limits)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));

        // Validate the configuration at composition time, not on first user drag.
        _ = MvpImageExecutionGuard.Evaluate(new ImageInspectionResult
        {
            Success = true,
            Format = ImageFormatKind.Jpeg,
            SourceSizeBytes = 1,
            Width = 1,
            Height = 1,
            FrameCount = 1
        }, _limits);
    }

    public Task<ImageProcessingResult> CompressAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
        ExecuteGuardedAsync(request, _inner.CompressAsync, cancellationToken);

    public Task<ImageProcessingResult> ConvertToWebPAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
        ExecuteGuardedAsync(request, _inner.ConvertToWebPAsync, cancellationToken);

    public Task<ImageProcessingResult> RemoveMetadataAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
        ExecuteGuardedAsync(request, _inner.RemoveMetadataAsync, cancellationToken);

    private async Task<ImageProcessingResult> ExecuteGuardedAsync(
        ImageProcessingRequest request,
        Func<ImageProcessingRequest, CancellationToken, Task<ImageProcessingResult>> execute,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryValidatePaths(request, out var pathError))
        {
            return ImageProcessingResult.Failed(pathError);
        }

        ImageInspectionResult inspection;
        try
        {
            inspection = await _inspector.InspectAsync(request.SourcePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ImageProcessingResult.Failed(new AppError(
                ErrorCode.DecodeFailed,
                $"Image inspection threw before decode authorization: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not inspect this image safely."));
        }

        var decision = MvpImageExecutionGuard.Evaluate(inspection, _limits);
        if (!decision.Allowed)
        {
            return ImageProcessingResult.Failed(ToError(decision, inspection));
        }

        return await execute(request, cancellationToken).ConfigureAwait(false);
    }

    private static AppError ToError(ImageExecutionGuardDecision decision, ImageInspectionResult inspection)
    {
        if (decision.Reason == ImageExecutionRejectionReason.InspectionFailed && inspection.Error is not null)
        {
            return inspection.Error;
        }

        var code = decision.Reason switch
        {
            ImageExecutionRejectionReason.UnsupportedInputFormat or
            ImageExecutionRejectionReason.AnimatedInputNotSupported => ErrorCode.UnsupportedFormat,

            ImageExecutionRejectionReason.SourceTooLarge or
            ImageExecutionRejectionReason.DimensionTooLarge or
            ImageExecutionRejectionReason.DecodedPixelCountTooLarge => ErrorCode.ImageResourceLimitExceeded,

            ImageExecutionRejectionReason.InvalidDimensions => ErrorCode.DecodeFailed,
            _ => ErrorCode.DecodeFailed
        };

        return new AppError(
            code,
            $"Image execution guard rejected input: {decision.Reason}. {decision.Detail}",
            code == ErrorCode.ImageResourceLimitExceeded
                ? "This image is too large to process safely with the current SmartDrag limits."
                : "This image is not supported by the current SmartDrag image pipeline.");
    }

    private static bool TryValidatePaths(ImageProcessingRequest request, out AppError error)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SourcePath) || string.IsNullOrWhiteSpace(request.TemporaryOutputPath))
            {
                error = new AppError(
                    ErrorCode.InvalidInput,
                    "Image processing requires both a source path and a temporary output path.",
                    "SmartDrag could not prepare this image operation.");
                return false;
            }

            var source = Path.GetFullPath(request.SourcePath);
            var temporary = Path.GetFullPath(request.TemporaryOutputPath);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(source, temporary, comparison))
            {
                error = new AppError(
                    ErrorCode.InvalidOutputPolicy,
                    "Image processing rejected identical source and temporary output paths.",
                    "SmartDrag will not overwrite the original file.");
                return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            error = new AppError(
                ErrorCode.InvalidInput,
                $"Image processing path validation failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not prepare this image operation.");
            return false;
        }

        error = null!;
        return true;
    }
}

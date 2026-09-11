using SmartDrag.Core.Errors;

namespace SmartDrag.Imaging;

public interface IImageProcessor
{
    Task<ImageProcessingResult> CompressAsync(ImageProcessingRequest request, CancellationToken cancellationToken);
    Task<ImageProcessingResult> ConvertToWebPAsync(ImageProcessingRequest request, CancellationToken cancellationToken);
    Task<ImageProcessingResult> RemoveMetadataAsync(ImageProcessingRequest request, CancellationToken cancellationToken);
}

public sealed record ImageProcessingRequest
{
    public required string SourcePath { get; init; }
    public required string TemporaryOutputPath { get; init; }
}

public sealed record ImageProcessingResult
{
    public required bool Success { get; init; }
    public long? SourceSizeBytes { get; init; }
    public long? OutputSizeBytes { get; init; }
    public AppError? Error { get; init; }

    public static ImageProcessingResult Succeeded(long? sourceSizeBytes = null, long? outputSizeBytes = null) => new()
    {
        Success = true,
        SourceSizeBytes = sourceSizeBytes,
        OutputSizeBytes = outputSizeBytes
    };

    public static ImageProcessingResult Failed(AppError error) => new()
    {
        Success = false,
        Error = error
    };
}

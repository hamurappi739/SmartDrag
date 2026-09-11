using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartDrag.Core.Errors;
using SmartDrag.Imaging;

namespace SmartDrag.Windows.Imaging;

/// <summary>
/// Windows Imaging Component adapter used by the first functional vertical slice. Inspection reads container
/// headers and metadata before processing. Encoding is limited to the codecs Windows WIC exposes here (JPEG/PNG);
/// WebP remains an explicit controlled failure until a separately measured WebP encoder is accepted.
/// </summary>
public sealed class WindowsWicImageCodec : IImageInspector, IImageProcessor
{
    private readonly int _jpegQuality;

    public WindowsWicImageCodec(int jpegQuality = 82)
    {
        if (jpegQuality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(jpegQuality), "JPEG quality must be between 1 and 100.");
        }

        _jpegQuality = jpegQuality;
    }

    public Task<ImageInspectionResult> InspectAsync(string sourcePath, CancellationToken cancellationToken) =>
        Task.Run(() => Inspect(sourcePath, cancellationToken), cancellationToken);

    public Task<ImageProcessingResult> CompressAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
        Task.Run(() => Encode(request, removeMetadata: false, cancellationToken), cancellationToken);

    public Task<ImageProcessingResult> ConvertToWebPAsync(ImageProcessingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ImageProcessingResult.Failed(new AppError(
            ErrorCode.UnsupportedFormat,
            "Windows WIC does not expose a supported WebP encoder in this adapter.",
            "WebP conversion is not available in this build yet.")));
    }

    public Task<ImageProcessingResult> RemoveMetadataAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
        Task.Run(() => Encode(request, removeMetadata: true, cancellationToken), cancellationToken);

    private static ImageInspectionResult Inspect(string sourcePath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(sourcePath);
            var sourceSize = new FileInfo(fullPath).Length;
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.Default);

            if (decoder is JpegBitmapDecoder && !HasJpegEndMarker(stream))
            {
                return new ImageInspectionResult
                {
                    Success = false,
                    Format = ImageFormatKind.Jpeg,
                    SourceSizeBytes = sourceSize,
                    Error = new AppError(
                        ErrorCode.DecodeFailed,
                        "JPEG input is missing the required end-of-image marker.",
                        "This JPEG appears incomplete and cannot be processed safely.")
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            var frame = decoder.Frames[0];
            var format = GetFormat(decoder);
            var metadata = frame.Metadata as BitmapMetadata;
            return new ImageInspectionResult
            {
                Success = format != ImageFormatKind.Unknown,
                Format = format,
                SourceSizeBytes = sourceSize,
                Width = frame.PixelWidth,
                Height = frame.PixelHeight,
                FrameCount = decoder.Frames.Count,
                HasAlpha = format == ImageFormatKind.Png && HasAlphaChannel(frame.Format),
                ExifOrientation = ReadExifOrientation(metadata),
                HasExif = ContainsMetadata(metadata, "/app1/ifd"),
                HasXmp = ContainsMetadata(metadata, "/xmp"),
                HasIptc = ContainsMetadata(metadata, "/app13/irb/8bimiptc/iptc"),
                HasIccProfile = frame.ColorContexts?.Count > 0
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ImageInspectionResult
            {
                Success = false,
                Format = ImageFormatKind.Unknown,
                Error = new AppError(
                    ErrorCode.DecodeFailed,
                    $"WIC inspection failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                    "SmartDrag could not read this image safely.")
            };
        }
    }

    private ImageProcessingResult Encode(
        ImageProcessingRequest request,
        bool removeMetadata,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = Path.GetFullPath(request.SourcePath);
            var extension = Path.GetExtension(sourcePath);
            using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(
                input,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder is JpegBitmapDecoder && !HasJpegEndMarker(input))
            {
                return Failed(
                    ErrorCode.DecodeFailed,
                    "JPEG input is missing the required end-of-image marker.",
                    "This JPEG appears incomplete and cannot be processed safely.");
            }

            if (decoder.Frames.Count != 1)
            {
                return Failed(ErrorCode.UnsupportedFormat, "WIC reported multiple frames.", "Animated images are not supported by this action yet.");
            }

            var frame = decoder.Frames[0];
            var sourceFormat = GetFormat(decoder);
            if (sourceFormat is not (ImageFormatKind.Jpeg or ImageFormatKind.Png))
            {
                return Failed(ErrorCode.UnsupportedFormat, "WIC reported a non-JPEG/PNG source.", "This image format is not supported by the current image pipeline.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var bitmap = removeMetadata ? NormalizeOrientation(frame) : frame;
            var encoder = CreateEncoder(sourceFormat, extension);
            var metadata = removeMetadata ? null : frame.Metadata as BitmapMetadata;
            var outputFrame = BitmapFrame.Create(
                bitmap,
                removeMetadata ? null : frame.Thumbnail,
                metadata,
                frame.ColorContexts);
            encoder.Frames.Add(outputFrame);

            using (var output = new FileStream(request.TemporaryOutputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(output);
            }

            return ImageProcessingResult.Succeeded(
                new FileInfo(sourcePath).Length,
                new FileInfo(request.TemporaryOutputPath).Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failed(
                ErrorCode.EncodeFailed,
                $"WIC encoding failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not create the processed image safely.");
        }
    }

    private BitmapEncoder CreateEncoder(ImageFormatKind format, string extension) => format switch
    {
        ImageFormatKind.Jpeg => new JpegBitmapEncoder { QualityLevel = _jpegQuality },
        ImageFormatKind.Png => new PngBitmapEncoder(),
        _ => throw new NotSupportedException($"No WIC encoder is available for '{extension}'.")
    };

    private static BitmapSource NormalizeOrientation(BitmapFrame frame)
    {
        var orientation = ReadExifOrientation(frame.Metadata as BitmapMetadata) ?? 1;
        if (orientation is 3 or 6 or 8)
        {
            var angle = orientation switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0
            };
            return new TransformedBitmap(frame, new RotateTransform(angle));
        }

        return frame;
    }

    private static ImageFormatKind GetFormat(BitmapDecoder decoder) => decoder switch
    {
        JpegBitmapDecoder => ImageFormatKind.Jpeg,
        PngBitmapDecoder => ImageFormatKind.Png,
        _ => ImageFormatKind.Unknown
    };

    private static bool HasAlphaChannel(PixelFormat format) =>
        format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 ||
        format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64 ||
        format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float;

    private static int? ReadExifOrientation(BitmapMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        try
        {
            var value = metadata.GetQuery("/app1/ifd/{ushort=274}");
            return value switch
            {
                ushort ushortValue => ushortValue,
                short shortValue => shortValue,
                byte byteValue => byteValue,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool ContainsMetadata(BitmapMetadata? metadata, string query)
    {
        try
        {
            return metadata?.ContainsQuery(query) == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasJpegEndMarker(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 2)
        {
            return false;
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Seek(-2, SeekOrigin.End);
            return stream.ReadByte() == 0xFF && stream.ReadByte() == 0xD9;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static ImageProcessingResult Failed(ErrorCode code, string technical, string userMessage) =>
        ImageProcessingResult.Failed(new AppError(code, technical, userMessage));
}

using SmartDrag.Core.Errors;
using SmartDrag.Imaging;
using Xunit;

namespace SmartDrag.Imaging.Tests;

public sealed class GuardedImageProcessorTests
{
    private static readonly ImageSafetyLimits Limits = new()
    {
        MaxSourceBytes = 10_000_000,
        MaxDecodedPixels = 20_000_000,
        MaxDimension = 10_000
    };

    [Fact]
    public async Task UnsupportedContent_NeverReachesInnerCodec()
    {
        var inspector = new FakeInspector(new ImageInspectionResult
        {
            Success = true,
            Format = ImageFormatKind.Unknown,
            Width = 100,
            Height = 100,
            FrameCount = 1
        });
        var inner = new RecordingProcessor();
        var sut = new GuardedImageProcessor(inspector, inner, Limits);

        var result = await sut.CompressAsync(Request(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsupportedFormat, result.Error?.Code);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task ResourceLimitRejection_NeverReachesInnerCodec()
    {
        var inspector = new FakeInspector(new ImageInspectionResult
        {
            Success = true,
            Format = ImageFormatKind.Png,
            SourceSizeBytes = 1000,
            Width = 9000,
            Height = 9000,
            FrameCount = 1
        });
        var inner = new RecordingProcessor();
        var sut = new GuardedImageProcessor(inspector, inner, Limits);

        var result = await sut.ConvertToWebPAsync(Request(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ImageResourceLimitExceeded, result.Error?.Code);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task ValidInspectedImage_ReachesInnerCodecExactlyOnce()
    {
        var inspector = new FakeInspector(new ImageInspectionResult
        {
            Success = true,
            Format = ImageFormatKind.Jpeg,
            SourceSizeBytes = 1000,
            Width = 1600,
            Height = 900,
            FrameCount = 1
        });
        var inner = new RecordingProcessor();
        var sut = new GuardedImageProcessor(inspector, inner, Limits);

        var result = await sut.RemoveMetadataAsync(Request(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task IdenticalSourceAndTemporaryPaths_AreRejectedBeforeInspection()
    {
        var inspector = new FakeInspector(new ImageInspectionResult
        {
            Success = true,
            Format = ImageFormatKind.Jpeg,
            SourceSizeBytes = 1000,
            Width = 100,
            Height = 100,
            FrameCount = 1
        });
        var inner = new RecordingProcessor();
        var sut = new GuardedImageProcessor(inspector, inner, Limits);
        var request = new ImageProcessingRequest
        {
            SourcePath = "photo.jpg",
            TemporaryOutputPath = "photo.jpg"
        };

        var result = await sut.CompressAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidOutputPolicy, result.Error?.Code);
        Assert.Equal(0, inspector.Calls);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task MissingProcessingPath_IsRejectedBeforeInspection()
    {
        var inspector = new FakeInspector(new ImageInspectionResult
        {
            Success = true,
            Format = ImageFormatKind.Jpeg,
            SourceSizeBytes = 1000,
            Width = 100,
            Height = 100,
            FrameCount = 1
        });
        var inner = new RecordingProcessor();
        var sut = new GuardedImageProcessor(inspector, inner, Limits);

        var result = await sut.CompressAsync(new ImageProcessingRequest
        {
            SourcePath = "photo.jpg",
            TemporaryOutputPath = " "
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidInput, result.Error?.Code);
        Assert.Equal(0, inspector.Calls);
        Assert.Equal(0, inner.Calls);
    }

    private static ImageProcessingRequest Request() => new()
    {
        SourcePath = "photo.jpg",
        TemporaryOutputPath = "partial.jpg"
    };

    private sealed class FakeInspector(ImageInspectionResult result) : IImageInspector
    {
        public int Calls { get; private set; }

        public Task<ImageInspectionResult> InspectAsync(string sourcePath, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingProcessor : IImageProcessor
    {
        public int Calls { get; private set; }

        public Task<ImageProcessingResult> CompressAsync(ImageProcessingRequest request, CancellationToken cancellationToken) => Execute();
        public Task<ImageProcessingResult> ConvertToWebPAsync(ImageProcessingRequest request, CancellationToken cancellationToken) => Execute();
        public Task<ImageProcessingResult> RemoveMetadataAsync(ImageProcessingRequest request, CancellationToken cancellationToken) => Execute();

        private Task<ImageProcessingResult> Execute()
        {
            Calls++;
            return Task.FromResult(ImageProcessingResult.Succeeded());
        }
    }
}

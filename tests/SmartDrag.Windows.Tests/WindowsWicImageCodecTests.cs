using SmartDrag.Core.Errors;
using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Output;
using SmartDrag.Imaging;
using SmartDrag.Infrastructure;
using SmartDrag.Infrastructure.Recovery;
using SmartDrag.Windows.Imaging;
using SmartDrag.Windows.Artifacts;
using Xunit;

namespace SmartDrag.Windows.Tests;

public sealed class WindowsWicImageCodecTests
{
    [Fact]
    public async Task InspectAsync_RecognizesPngFromContent()
    {
        var codec = new WindowsWicImageCodec();

        var result = await codec.InspectAsync(Fixture("rgb-basic.png"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ImageFormatKind.Png, result.Format);
        Assert.Equal(1, result.FrameCount);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public async Task CompressAsync_WritesReadablePngToTemporaryPath()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CopyFixture("rgb-basic.png");
        var output = Path.Combine(directory.Path, "compressed.png");
        var codec = new WindowsWicImageCodec();

        var result = await codec.CompressAsync(new ImageProcessingRequest
        {
            SourcePath = source,
            TemporaryOutputPath = output
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(output));
        var inspected = await codec.InspectAsync(output, CancellationToken.None);
        Assert.True(inspected.Success);
        Assert.Equal(ImageFormatKind.Png, inspected.Format);
    }

    [Fact]
    public async Task CompressHandler_CreatesGeneratedOutputAndPreservesSource()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.CopyFixture("rgb-photo.jpg");
        var sourceBytes = await File.ReadAllBytesAsync(source);
        var outputManager = new PhysicalOutputManager(
            new InMemoryOutputRecoveryJournal(),
            new WindowsGeneratedArtifactIdentityService());
        var guarded = new GuardedImageProcessor(
            new WindowsWicImageCodec(),
            new WindowsWicImageCodec(),
            new ImageSafetyLimits
            {
                MaxSourceBytes = 100L * 1024 * 1024,
                MaxDecodedPixels = 64L * 1024 * 1024,
                MaxDimension = 12_000
            });
        var handler = new CompressImageActionHandler(guarded, outputManager);

        var result = await handler.ExecuteAsync(new ActionRequest
        {
            RequestId = Guid.NewGuid(),
            ActionId = BuiltInActionIds.CompressImage,
            InputPaths = new[] { source },
            OutputPolicy = OutputPolicy.SafeMvpDefault
        }, CancellationToken.None);

        Assert.True(result.Success);
        var artifact = Assert.Single(result.GeneratedArtifacts);
        Assert.NotEqual(source, artifact.Path, StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(artifact.Path));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task ConvertToWebPAsync_FailsControlledWhenWicEncoderIsUnavailable()
    {
        var codec = new WindowsWicImageCodec();

        var result = await codec.ConvertToWebPAsync(new ImageProcessingRequest
        {
            SourcePath = Fixture("rgb-basic.png"),
            TemporaryOutputPath = Path.Combine(Path.GetTempPath(), "smartdrag-unwritten.webp")
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsupportedFormat, result.Error?.Code);
    }

    [Fact]
    public async Task CompressAsync_TruncatedImageReturnsControlledFailure()
    {
        using var directory = new TemporaryDirectory();
        var output = Path.Combine(directory.Path, "truncated-output.jpg");
        var codec = new WindowsWicImageCodec();

        var result = await codec.CompressAsync(new ImageProcessingRequest
        {
            SourcePath = Fixture("truncated.jpg"),
            TemporaryOutputPath = output
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.False(File.Exists(output));
    }

    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "tests", "fixtures", "images", name));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmartDrag.Windows.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CopyFixture(string name)
        {
            var destination = System.IO.Path.Combine(Path, name);
            File.Copy(Fixture(name), destination);
            return destination;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}

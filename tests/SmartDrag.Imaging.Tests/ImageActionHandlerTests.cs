using SmartDrag.Core.Actions;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Output;
using SmartDrag.Imaging;
using Xunit;

namespace SmartDrag.Imaging.Tests;

public sealed class ImageActionHandlerTests
{
    [Fact]
    public async Task Convert_to_webp_uses_new_extension_and_commits()
    {
        var output = new FakeOutputManager();
        var processor = new FakeProcessor
        {
            Convert = (request, _) =>
            {
                Assert.EndsWith(".webp", request.TemporaryOutputPath, StringComparison.OrdinalIgnoreCase);
                return Task.FromResult(ImageProcessingResult.Succeeded());
            }
        };
        var handler = new ConvertImageToWebPActionHandler(processor, output);

        var result = await handler.ExecuteAsync(Request(BuiltInActionIds.ConvertImageToWebP), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, output.LastRequest?.Suffix);
        Assert.Equal(".webp", output.LastRequest?.NewExtension);
        Assert.Equal(1, output.CommitCount);
        Assert.Equal(0, output.AbandonCount);
    }


    [Fact]
    public async Task Successful_processing_marks_committed_output_as_generated_and_preserves_metrics()
    {
        var output = new FakeOutputManager();
        var processor = new FakeProcessor
        {
            Compress = (_, _) => Task.FromResult(ImageProcessingResult.Succeeded(1_000, 400))
        };
        var handler = new CompressImageActionHandler(processor, output);

        var result = await handler.ExecuteAsync(Request(BuiltInActionIds.CompressImage), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { "final.png" }, result.OutputPaths);
        Assert.Equal(new[] { "final.png" }, result.GeneratedArtifacts.Select(artifact => artifact.Path));
        Assert.Equal(1_000, result.Metrics?.SourceSizeBytes);
        Assert.Equal(400, result.Metrics?.OutputSizeBytes);
    }

    [Fact]
    public async Task Processor_failure_abandons_partial_output()
    {
        var output = new FakeOutputManager();
        var processor = new FakeProcessor
        {
            Compress = (_, _) => Task.FromResult(ImageProcessingResult.Failed(new AppError(
                ErrorCode.EncodeFailed,
                "encode failed",
                "Could not compress the image.")))
        };
        var handler = new CompressImageActionHandler(processor, output);

        var result = await handler.ExecuteAsync(Request(BuiltInActionIds.CompressImage), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.EncodeFailed, result.Error?.Code);
        Assert.Equal(1, output.AbandonCount);
        Assert.Equal(0, output.CommitCount);
    }

    [Fact]
    public async Task Cancellation_still_attempts_cleanup()
    {
        var output = new FakeOutputManager();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new FakeProcessor
        {
            RemoveMetadata = async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return ImageProcessingResult.Succeeded();
            }
        };
        var handler = new RemoveImageMetadataActionHandler(processor, output);
        using var cts = new CancellationTokenSource();

        var task = handler.ExecuteAsync(Request(BuiltInActionIds.RemoveImageMetadata), cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, output.AbandonCount);
    }

    [Fact]
    public async Task Multiple_inputs_are_rejected_before_reservation()
    {
        var output = new FakeOutputManager();
        var handler = new CompressImageActionHandler(new FakeProcessor(), output);
        var request = Request(BuiltInActionIds.CompressImage) with
        {
            InputPaths = new[] { "a.png", "b.png" }
        };

        var result = await handler.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InvalidInput, result.Error?.Code);
        Assert.Null(output.LastRequest);
    }

    private static ActionRequest Request(SmartDrag.Core.Primitives.ActionId id) => new()
    {
        RequestId = Guid.NewGuid(),
        ActionId = id,
        InputPaths = new[] { "photo.png" },
        OutputPolicy = OutputPolicy.SafeMvpDefault
    };

    private sealed class FakeProcessor : IImageProcessor
    {
        public Func<ImageProcessingRequest, CancellationToken, Task<ImageProcessingResult>> Compress { get; init; } =
            (_, _) => Task.FromResult(ImageProcessingResult.Succeeded());
        public Func<ImageProcessingRequest, CancellationToken, Task<ImageProcessingResult>> Convert { get; init; } =
            (_, _) => Task.FromResult(ImageProcessingResult.Succeeded());
        public Func<ImageProcessingRequest, CancellationToken, Task<ImageProcessingResult>> RemoveMetadata { get; init; } =
            (_, _) => Task.FromResult(ImageProcessingResult.Succeeded());

        public Task<ImageProcessingResult> CompressAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
            Compress(request, cancellationToken);

        public Task<ImageProcessingResult> ConvertToWebPAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
            Convert(request, cancellationToken);

        public Task<ImageProcessingResult> RemoveMetadataAsync(ImageProcessingRequest request, CancellationToken cancellationToken) =>
            RemoveMetadata(request, cancellationToken);
    }

    private sealed class FakeOutputManager : IOutputManager
    {
        public OutputRequest? LastRequest { get; private set; }
        public int CommitCount { get; private set; }
        public int AbandonCount { get; private set; }

        public Task<OutputReservationResult> ReserveAsync(OutputRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var extension = request.NewExtension ?? ".png";
            return Task.FromResult(OutputReservationResult.Succeeded(new OutputReservation
            {
                Id = Guid.NewGuid(),
                SourcePath = request.SourcePath,
                TemporaryPath = $"partial{extension}",
                CollisionBasePath = $"final{extension}",
                PreferredFinalPath = $"final{extension}",
                Policy = request.Policy
            }));
        }

        public Task<OutputCommitResult> CommitAsync(OutputReservation reservation, CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.FromResult(OutputCommitResult.Succeeded(new GeneratedArtifact { Path = reservation.PreferredFinalPath }));
        }

        public Task<OutputCleanupResult> AbandonAsync(OutputReservation reservation)
        {
            AbandonCount++;
            return Task.FromResult(OutputCleanupResult.Succeeded());
        }
    }
}

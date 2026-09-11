using SmartDrag.Core.Actions;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Output;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Imaging;

public sealed class CompressImageActionHandler(IImageProcessor processor, IOutputManager outputManager)
    : ImageActionHandlerBase(processor, outputManager)
{
    public override ActionId ActionId => BuiltInActionIds.CompressImage;
    protected override string OutputSuffix => "-compressed";

    protected override Task<ImageProcessingResult> ProcessAsync(
        ImageProcessingRequest request,
        CancellationToken cancellationToken) => Processor.CompressAsync(request, cancellationToken);
}

public sealed class ConvertImageToWebPActionHandler(IImageProcessor processor, IOutputManager outputManager)
    : ImageActionHandlerBase(processor, outputManager)
{
    public override ActionId ActionId => BuiltInActionIds.ConvertImageToWebP;
    protected override string OutputSuffix => string.Empty;
    protected override string? NewExtension => ".webp";

    protected override Task<ImageProcessingResult> ProcessAsync(
        ImageProcessingRequest request,
        CancellationToken cancellationToken) => Processor.ConvertToWebPAsync(request, cancellationToken);
}

public sealed class RemoveImageMetadataActionHandler(IImageProcessor processor, IOutputManager outputManager)
    : ImageActionHandlerBase(processor, outputManager)
{
    public override ActionId ActionId => BuiltInActionIds.RemoveImageMetadata;
    protected override string OutputSuffix => "-clean";

    protected override Task<ImageProcessingResult> ProcessAsync(
        ImageProcessingRequest request,
        CancellationToken cancellationToken) => Processor.RemoveMetadataAsync(request, cancellationToken);
}

public abstract class ImageActionHandlerBase : IActionHandler
{
    protected ImageActionHandlerBase(IImageProcessor processor, IOutputManager outputManager)
    {
        Processor = processor ?? throw new ArgumentNullException(nameof(processor));
        OutputManager = outputManager ?? throw new ArgumentNullException(nameof(outputManager));
    }

    protected IImageProcessor Processor { get; }
    protected IOutputManager OutputManager { get; }

    public abstract ActionId ActionId { get; }
    protected abstract string OutputSuffix { get; }
    protected virtual string? NewExtension => null;

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InputPaths.Count != 1)
        {
            return ActionResult.Failed(new AppError(
                ErrorCode.InvalidInput,
                $"MVP image action '{ActionId}' requires exactly one input path; received {request.InputPaths.Count}.",
                "This action currently works with one file at a time."));
        }

        var reservationResult = await OutputManager.ReserveAsync(new OutputRequest
        {
            SourcePath = request.InputPaths[0],
            Suffix = OutputSuffix,
            NewExtension = NewExtension,
            Policy = request.OutputPolicy
        }, cancellationToken).ConfigureAwait(false);

        if (!reservationResult.Success || reservationResult.Reservation is null)
        {
            return ActionResult.Failed(reservationResult.Error ?? Unknown("Output reservation failed without an error."));
        }

        var reservation = reservationResult.Reservation;
        try
        {
            var processing = await ProcessAsync(new ImageProcessingRequest
            {
                SourcePath = reservation.SourcePath,
                TemporaryOutputPath = reservation.TemporaryPath
            }, cancellationToken).ConfigureAwait(false);

            if (!processing.Success)
            {
                var cleanup = await OutputManager.AbandonAsync(reservation).ConfigureAwait(false);
                if (!cleanup.Success)
                {
                    return CleanupFailure(cleanup, processing.Error);
                }

                return ActionResult.Failed(processing.Error ?? Unknown("Image processor failed without an error."));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var commit = await OutputManager.CommitAsync(reservation, cancellationToken).ConfigureAwait(false);
            if (!commit.Success || commit.Artifact is null || string.IsNullOrWhiteSpace(commit.Artifact.Path))
            {
                var cleanup = await OutputManager.AbandonAsync(reservation).ConfigureAwait(false);
                if (!cleanup.Success)
                {
                    return CleanupFailure(cleanup, commit.Error);
                }

                return ActionResult.Failed(commit.Error ?? Unknown("Output commit failed without an error."));
            }

            return ActionResult.SucceededGeneratedWithMetrics(new ActionMetrics
            {
                SourceSizeBytes = processing.SourceSizeBytes,
                OutputSizeBytes = processing.OutputSizeBytes
            }, commit.Artifact);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await OutputManager.AbandonAsync(reservation).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await OutputManager.AbandonAsync(reservation).ConfigureAwait(false);
            throw;
        }
    }

    protected abstract Task<ImageProcessingResult> ProcessAsync(
        ImageProcessingRequest request,
        CancellationToken cancellationToken);

    private static ActionResult CleanupFailure(OutputCleanupResult cleanup, AppError? originalError)
    {
        var cleanupError = cleanup.Error ?? new AppError(
            ErrorCode.CleanupFailed,
            "Cleanup failed without an error object.",
            "SmartDrag could not remove a temporary file.");

        if (originalError is null)
        {
            return ActionResult.Failed(cleanupError);
        }

        return ActionResult.Failed(cleanupError with
        {
            TechnicalMessage = $"{cleanupError.TechnicalMessage}{Environment.NewLine}Original operation error: {originalError.TechnicalMessage}"
        });
    }

    private static AppError Unknown(string technicalMessage) => new(
        ErrorCode.Unknown,
        technicalMessage,
        "The operation failed.");
}

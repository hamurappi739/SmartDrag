using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Imaging;
using SmartDrag.Infrastructure;
using SmartDrag.Orchestration;
using SmartDrag.Presentation;
using SmartDrag.Runtime;
using SmartDrag.Windows.Artifacts;
using SmartDrag.Windows.Completion;

namespace SmartDrag.App.Composition;

/// <summary>
/// Single composition root for the deterministic production runtime beneath the still-blocked native activation
/// layer. It intentionally receives the concrete codec/overlay adapters rather than selecting them here.
/// </summary>
public sealed class ProductionRuntimeGraph : IAsyncDisposable
{
    private readonly StartupSafetyLease _startupSafetyLease;
    private readonly IJobQueue _jobQueue;
    private readonly CompletionCoordinator _completionCoordinator;
    private int _disposed;

    private ProductionRuntimeGraph(
        StartupSafetyLease startupSafetyLease,
        IJobQueue jobQueue,
        IProductionDragInteraction dragInteraction,
        CompletionCoordinator completionCoordinator,
        MvpPresentationCoordinator presentation,
        IFilePayloadSnapshotFactory payloadSnapshots,
        AppCapabilities capabilities)
    {
        _startupSafetyLease = startupSafetyLease;
        _jobQueue = jobQueue;
        _completionCoordinator = completionCoordinator;
        DragInteraction = dragInteraction;
        Presentation = presentation;
        PayloadSnapshots = payloadSnapshots;
        Capabilities = capabilities;
    }

    // Deliberately narrow application-facing surface. Presentation owns cancel/completion intents; native adapters
    // receive only the safe drag interface, not JobQueue/ActionExecutor/CompletionCommandExecutor internals.
    public IProductionDragInteraction DragInteraction { get; }
    public MvpPresentationCoordinator Presentation { get; }
    public IFilePayloadSnapshotFactory PayloadSnapshots { get; }
    public AppCapabilities Capabilities { get; }

    /// <summary>
    /// Builds the runtime object graph after single-instance/recovery safety bootstrap succeeded. This method does
    /// not register WinEvent hooks and does not bypass ProductionActivationPolicy; native activation remains a
    /// separate post-gate step.
    /// </summary>
    public static async Task<ProductionRuntimeGraph> CreateDeterministicRuntimeAsync(
        StartupSafetyBootstrapResult bootstrap,
        ProductionRuntimeDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(dependencies);

        if (!bootstrap.RuntimeMayStart || bootstrap.Lease is null)
        {
            throw new InvalidOperationException(
                $"Runtime composition requires a clean startup safety lease; bootstrap status was '{bootstrap.Status}'.");
        }

        SequentialJobQueue? queue = null;
        CompletionCoordinator? completionCoordinator = null;
        MvpPresentationCoordinator? presentation = null;
        try
        {
            var identity = new WindowsGeneratedArtifactIdentityService();
            var outputManager = new PhysicalOutputManager(bootstrap.Lease.RecoveryJournal, identity);
            var guardedProcessor = new GuardedImageProcessor(
                dependencies.ImageInspector,
                dependencies.CodecProcessor,
                dependencies.ImageSafetyLimits);

            var handlers = new IActionHandler[]
            {
                new CompressImageActionHandler(guardedProcessor, outputManager),
                new ConvertImageToWebPActionHandler(guardedProcessor, outputManager),
                new RemoveImageMetadataActionHandler(guardedProcessor, outputManager)
            };

            var registry = new ActionRegistry(BuiltInActionDefinitions.Mvp);
            var executor = new ActionExecutor(handlers);
            queue = new SequentialJobQueue(executor);
            var workflow = new DragWorkflowOrchestrator(registry);
            var dispatcher = new CommittedActionDispatcher(queue);
            var dragInteraction = new DragInteractionCoordinator(workflow, dispatcher, dependencies.OverlayService);

            var completionPlatform = new WindowsCompletionPlatformService(dependencies.CompletionOwnerHwnd);
            var completionCommands = new CompletionCommandExecutor(queue, completionPlatform, identity);
            completionCoordinator = new CompletionCoordinator(queue, completionCommands.Capabilities);
            presentation = new MvpPresentationCoordinator(queue, completionCoordinator, completionCommands, registry);
            var payloadSnapshots = new PhysicalFilePayloadSnapshotFactory();

            return new ProductionRuntimeGraph(
                bootstrap.Lease,
                queue,
                dragInteraction,
                completionCoordinator,
                presentation,
                payloadSnapshots,
                new AppCapabilities
                {
                    ImagingAvailable = true,
                    WebpEncodingAvailable = false
                });
        }
        catch
        {
            presentation?.Dispose();
            completionCoordinator?.Dispose();
            if (queue is not null)
            {
                await queue.DisposeAsync().ConfigureAwait(false);
            }

            // CreateDeterministicRuntimeAsync takes ownership of a clean bootstrap lease. If graph construction
            // fails, no caller-visible owner exists, so release the journal/mutex here rather than leaking them.
            bootstrap.Lease.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Presentation.Dispose();
        _completionCoordinator.Dispose();
        await _jobQueue.DisposeAsync().ConfigureAwait(false);
        _startupSafetyLease.Dispose();
    }
}

public sealed record ProductionRuntimeDependencies
{
    public required IOverlayService OverlayService { get; init; }
    public required IImageInspector ImageInspector { get; init; }
    public required IImageProcessor CodecProcessor { get; init; }
    public required ImageSafetyLimits ImageSafetyLimits { get; init; }
    public IntPtr CompletionOwnerHwnd { get; init; }
}

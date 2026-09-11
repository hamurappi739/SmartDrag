using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;
using SmartDrag.Orchestration;
using Xunit;

namespace SmartDrag.Orchestration.Tests;

public sealed class DragInteractionCoordinatorTests
{
    private static readonly AppCapabilities Capabilities = new()
    {
        ImagingAvailable = true,
        WebpEncodingAvailable = true
    };
    private static readonly UserSettings Settings = new();

    [Fact]
    public async Task PresentedSession_CanCommitExactlyOnce()
    {
        await using var queue = new RecordingQueue();
        var overlay = new RecordingOverlay();
        var workflow = new DragWorkflowOrchestrator(new ActionRegistry(BuiltInActionDefinitions.Mvp));
        var sut = new DragInteractionCoordinator(workflow, new CommittedActionDispatcher(queue), overlay);
        var dragId = Guid.NewGuid();

        var prepared = await sut.TryPresentAsync(
            dragId,
            Qualification("C:\\Temp\\photo.jpg", PayloadQualificationState.Eligible),
            Capabilities,
            Settings,
            new OverlayPlacement(100, 100, 240, 144),
            CancellationToken.None);

        var first = await sut.TryCommitMvpAsync(
            dragId,
            BuiltInActionIds.CompressImage,
            Qualification("C:\\Temp\\photo.jpg", PayloadQualificationState.Supported),
            Capabilities,
            Settings,
            CancellationToken.None);

        var duplicate = await sut.TryCommitMvpAsync(
            dragId,
            BuiltInActionIds.CompressImage,
            Qualification("C:\\Temp\\photo.jpg", PayloadQualificationState.Supported),
            Capabilities,
            Settings,
            CancellationToken.None);

        Assert.True(prepared.IsPrepared);
        Assert.Equal(ActionDispatchStatus.Accepted, first.Status);
        Assert.Equal(ActionDispatchStatus.Rejected, duplicate.Status);
        Assert.Equal(ActionCommitStatus.OverlaySessionInvalid, duplicate.CommitStatus);
        Assert.Single(queue.Requests);
        Assert.Equal(OverlayHideReason.ActionCommitted, overlay.LastHideReason);
    }

    [Fact]
    public async Task NewSession_SupersedesOldCapability()
    {
        await using var queue = new RecordingQueue();
        var overlay = new RecordingOverlay();
        var workflow = new DragWorkflowOrchestrator(new ActionRegistry(BuiltInActionDefinitions.Mvp));
        var sut = new DragInteractionCoordinator(workflow, new CommittedActionDispatcher(queue), overlay);
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        await sut.TryPresentAsync(oldId, Qualification("C:\\Temp\\old.jpg", PayloadQualificationState.Eligible), Capabilities, Settings, new(0, 0, 1, 1), CancellationToken.None);
        await sut.TryPresentAsync(newId, Qualification("C:\\Temp\\new.jpg", PayloadQualificationState.Eligible), Capabilities, Settings, new(0, 0, 1, 1), CancellationToken.None);

        var oldCommit = await sut.TryCommitMvpAsync(oldId, BuiltInActionIds.CompressImage, Qualification("C:\\Temp\\old.jpg", PayloadQualificationState.Supported), Capabilities, Settings, CancellationToken.None);

        Assert.Equal(ActionDispatchStatus.Rejected, oldCommit.Status);
        Assert.Equal(newId, sut.ActiveDragSessionId);
        Assert.Contains(OverlayHideReason.Suppressed, overlay.HideReasons);
    }


    [Fact]
    public async Task InvalidSettings_CannotCommitThroughProductionSurface()
    {
        await using var queue = new RecordingQueue();
        var overlay = new RecordingOverlay();
        var workflow = new DragWorkflowOrchestrator(new ActionRegistry(BuiltInActionDefinitions.Mvp));
        IProductionDragInteraction sut = new DragInteractionCoordinator(workflow, new CommittedActionDispatcher(queue), overlay);
        var dragId = Guid.NewGuid();
        var invalid = new UserSettings { Output = new OutputSettings { PreserveSource = false } };

        var presentation = await sut.TryPresentAsync(
            dragId,
            Qualification("C:\\Temp\\photo.jpg", PayloadQualificationState.Eligible),
            Capabilities,
            invalid,
            new(0, 0, 1, 1),
            CancellationToken.None);

        Assert.False(presentation.IsPrepared);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task EndNativeDrag_ClearsOnlyMatchingActiveSession()
    {
        await using var queue = new RecordingQueue();
        var overlay = new RecordingOverlay();
        var workflow = new DragWorkflowOrchestrator(new ActionRegistry(BuiltInActionDefinitions.Mvp));
        var sut = new DragInteractionCoordinator(workflow, new CommittedActionDispatcher(queue), overlay);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await sut.TryPresentAsync(firstId, Qualification("C:\\Temp\\first.jpg", PayloadQualificationState.Eligible), Capabilities, Settings, new(0, 0, 1, 1), CancellationToken.None);
        await sut.EndNativeDragAsync(firstId, cancelled: true, cancellationToken: CancellationToken.None);

        Assert.Null(sut.ActiveDragSessionId);
        Assert.Equal(OverlayHideReason.NativeDragCancelled, overlay.LastHideReason);

        await sut.TryPresentAsync(secondId, Qualification("C:\\Temp\\second.jpg", PayloadQualificationState.Eligible), Capabilities, Settings, new(0, 0, 1, 1), CancellationToken.None);
        await sut.EndNativeDragAsync(firstId, cancelled: true, cancellationToken: CancellationToken.None);

        Assert.Equal(secondId, sut.ActiveDragSessionId);
        Assert.Equal(OverlayHideReason.NativeDragCancelled, overlay.LastHideReason);
    }

    [Fact]
    public async Task OverlayPresentationFailure_ClearsCapabilityAndHidesWithError()
    {
        await using var queue = new RecordingQueue();
        var overlay = new RecordingOverlay { ThrowOnShow = true };
        var workflow = new DragWorkflowOrchestrator(new ActionRegistry(BuiltInActionDefinitions.Mvp));
        var sut = new DragInteractionCoordinator(workflow, new CommittedActionDispatcher(queue), overlay);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.TryPresentAsync(
            Guid.NewGuid(),
            Qualification("C:\\Temp\\broken.jpg", PayloadQualificationState.Eligible),
            Capabilities,
            Settings,
            new(0, 0, 1, 1),
            CancellationToken.None));

        Assert.Null(sut.ActiveDragSessionId);
        Assert.Equal(OverlayHideReason.Error, overlay.LastHideReason);
    }

    private static PayloadQualificationResult Qualification(string path, PayloadQualificationState state) => new()
    {
        State = state,
        Reason = PayloadRejectionReason.None,
        Payload = new DragPayloadInfo
        {
            Kind = PayloadKind.Files,
            IsSupported = true,
            Files = new[]
            {
                new DraggedFile { FullPath = path, Extension = ".jpg", Category = FileCategory.Image, SizeBytes = 100 }
            }
        }
    };

    private sealed class RecordingOverlay : IOverlayService
    {
        public List<OverlayHideReason> HideReasons { get; } = new();
        public bool ThrowOnShow { get; init; }
        public OverlayHideReason? LastHideReason => HideReasons.Count == 0 ? null : HideReasons[^1];
        public Task ShowAsync(OverlayModel model, OverlayPlacement placement, CancellationToken cancellationToken)
        {
            if (ThrowOnShow)
            {
                throw new InvalidOperationException("overlay failed");
            }

            return Task.CompletedTask;
        }
        public Task UpdateAsync(OverlayModel model, OverlayPlacement placement, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HideAsync(OverlayHideReason reason, CancellationToken cancellationToken)
        {
            HideReasons.Add(reason);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingQueue : IJobQueue
    {
        public List<ActionRequest> Requests { get; } = new();
        public event EventHandler<JobChangedEventArgs>? JobChanged
        {
            add { }
            remove { }
        }
        public JobId Enqueue(ActionRequest request)
        {
            Requests.Add(request);
            return JobId.New();
        }
        public bool TryCancel(JobId jobId) => false;
        public bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot) { snapshot = null!; return false; }
        public IReadOnlyList<JobSnapshot> GetSnapshots() => Array.Empty<JobSnapshot>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

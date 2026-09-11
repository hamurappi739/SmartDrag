using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Output;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;
using SmartDrag.Orchestration;
using Xunit;

namespace SmartDrag.Orchestration.Tests;

public sealed class CommittedActionDispatcherTests
{
    private static readonly AppCapabilities Capabilities = new()
    {
        ImagingAvailable = true,
        WebpEncodingAvailable = true
    };
    private static readonly UserSettings Settings = new();

    [Fact]
    public void Dispatch_RejectedDecision_ThrowsWithoutEnqueue()
    {
        var queue = new FakeQueue();
        var sut = new CommittedActionDispatcher(queue);
        var decision = Decision("C:\\Temp\\photo.jpg", "C:\\Temp\\different.jpg", BuiltInActionIds.CompressImage);

        Assert.False(decision.IsAccepted);
        Assert.Throws<InvalidOperationException>(() => sut.Dispatch(decision));
        Assert.Equal(0, queue.EnqueueCount);
    }

    [Fact]
    public void Dispatch_AcceptedDecision_EnqueuesExactlyOnce()
    {
        var queue = new FakeQueue();
        var sut = new CommittedActionDispatcher(queue);
        var decision = Decision("C:\\Temp\\photo.jpg", "C:\\Temp\\photo.jpg", BuiltInActionIds.CompressImage);

        var id = sut.Dispatch(decision);

        Assert.Equal(1, queue.EnqueueCount);
        Assert.Equal(decision.Request, queue.LastRequest);
        Assert.NotEqual(default, id);
    }

    [Fact]
    public void Dispatch_SameAcceptedRequestTwice_IsIdempotent()
    {
        var queue = new FakeQueue();
        var sut = new CommittedActionDispatcher(queue);
        var decision = Decision("C:\\Temp\\photo.jpg", "C:\\Temp\\photo.jpg", BuiltInActionIds.CompressImage);

        var first = sut.Dispatch(decision);
        var second = sut.Dispatch(decision);

        Assert.Equal(first, second);
        Assert.Equal(1, queue.EnqueueCount);
    }

    [Fact]
    public void Dispatch_SameRequestIdWithDifferentAction_ThrowsWithoutSecondEnqueue()
    {
        var queue = new FakeQueue();
        var sut = new CommittedActionDispatcher(queue);
        var dragId = Guid.NewGuid();
        var compress = Decision("C:\\Temp\\photo.jpg", "C:\\Temp\\photo.jpg", BuiltInActionIds.CompressImage, dragId);
        var convert = Decision("C:\\Temp\\photo.jpg", "C:\\Temp\\photo.jpg", BuiltInActionIds.ConvertImageToWebP, dragId);

        sut.Dispatch(compress);

        Assert.Throws<InvalidOperationException>(() => sut.Dispatch(convert));
        Assert.Equal(1, queue.EnqueueCount);
    }

    private static ActionCommitDecision Decision(
        string preflightPath,
        string authoritativePath,
        ActionId actionId,
        Guid? dragId = null)
    {
        var orchestrator = new DragWorkflowOrchestrator(new ActionRegistry(BuiltInActionDefinitions.Mvp));
        var prepared = orchestrator.PrepareOverlay(
            dragId ?? Guid.NewGuid(),
            Qualification(preflightPath, PayloadQualificationState.Eligible),
            Capabilities,
            Settings).Session!;

        return orchestrator.TryCommitDrop(
            prepared,
            actionId,
            Qualification(authoritativePath, PayloadQualificationState.Supported),
            Capabilities,
            Settings,
            OutputPolicy.SafeMvpDefault);
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
                new DraggedFile
                {
                    FullPath = path,
                    Extension = ".jpg",
                    Category = FileCategory.Image,
                    SizeBytes = 1234
                }
            }
        }
    };

    private sealed class FakeQueue : IJobQueue
    {
        public int EnqueueCount { get; private set; }
        public ActionRequest? LastRequest { get; private set; }
        public event EventHandler<JobChangedEventArgs>? JobChanged
        {
            add { }
            remove { }
        }

        public JobId Enqueue(ActionRequest request)
        {
            EnqueueCount++;
            LastRequest = request;
            return JobId.New();
        }

        public bool TryCancel(JobId jobId) => false;
        public bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot) { snapshot = null!; return false; }
        public IReadOnlyList<JobSnapshot> GetSnapshots() => Array.Empty<JobSnapshot>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

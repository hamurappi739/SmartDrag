using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Output;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;
using SmartDrag.Orchestration;
using Xunit;

namespace SmartDrag.Orchestration.Tests;

public sealed class DragWorkflowOrchestratorTests
{
    private static readonly AppCapabilities Capabilities = new()
    {
        ImagingAvailable = true,
        WebpEncodingAvailable = true
    };
    private static readonly UserSettings Settings = new();

    [Fact]
    public void PrepareOverlay_EligibleSingleImage_BindsExactOfferedActions()
    {
        var sut = CreateSut();
        var result = sut.PrepareOverlay(Guid.NewGuid(), Preflight("C:\\Temp\\photo.jpg"), Capabilities, Settings);

        Assert.True(result.IsPrepared);
        Assert.NotNull(result.Session);
        Assert.Equal(3, result.Session!.OfferedActionIds.Count);
        Assert.Equal("photo.jpg", result.Session.Overlay.PayloadLabel);
        Assert.Contains(BuiltInActionIds.CompressImage, result.Session.OfferedActionIds);
        Assert.Contains(BuiltInActionIds.ConvertImageToWebP, result.Session.OfferedActionIds);
        Assert.Contains(BuiltInActionIds.RemoveImageMetadata, result.Session.OfferedActionIds);
    }

    [Fact]
    public void PrepareOverlay_FreezesOverlayActionListAndBindsSessionId()
    {
        var dragSessionId = Guid.NewGuid();
        var session = CreateSut().PrepareOverlay(
            dragSessionId,
            Preflight("C:\\Temp\\photo.jpg"),
            Capabilities,
            Settings).Session!;

        Assert.Equal(dragSessionId, session.Overlay.DragSessionId);
        var actions = Assert.IsAssignableFrom<IList<OverlayActionItem>>(session.Overlay.Actions);
        Assert.True(actions.IsReadOnly);
        Assert.Equal(session.OfferedActionIds.Count, session.Overlay.Actions.Count);
    }

    [Fact]
    public void PrepareOverlay_UnknownPayload_IsSuppressed()
    {
        var sut = CreateSut();
        var result = sut.PrepareOverlay(Guid.NewGuid(), new PayloadQualificationResult
        {
            State = PayloadQualificationState.Unknown,
            Reason = PayloadRejectionReason.EvidenceUnavailable
        }, Capabilities, Settings);

        Assert.False(result.IsPrepared);
        Assert.Equal(OverlayPreparationStatus.Suppressed, result.Status);
    }

    [Fact]
    public void TryCommitDrop_MatchingAuthoritativePayload_CreatesSafeActionRequest()
    {
        var sut = CreateSut();
        var dragSessionId = Guid.NewGuid();
        var prepared = sut.PrepareOverlay(dragSessionId, Preflight("C:\\Temp\\photo.jpg"), Capabilities, Settings).Session!;

        var result = sut.TryCommitDrop(
            prepared,
            BuiltInActionIds.CompressImage,
            Authoritative("C:\\Temp\\photo.jpg"),
            Capabilities,
            Settings,
            OutputPolicy.SafeMvpDefault);

        Assert.True(result.IsAccepted);
        Assert.NotNull(result.Request);
        Assert.Equal(BuiltInActionIds.CompressImage, result.Request!.ActionId);
        Assert.Equal(dragSessionId, result.Request.RequestId);
        Assert.True(result.Request.OutputPolicy.PreserveSource);
        Assert.Single(result.Request.InputPaths);
    }

    [Fact]
    public void TryCommitDrop_DifferentAuthoritativePath_IsRejected()
    {
        var sut = CreateSut();
        var prepared = sut.PrepareOverlay(Guid.NewGuid(), Preflight("C:\\Temp\\photo.jpg"), Capabilities, Settings).Session!;

        var result = sut.TryCommitDrop(
            prepared,
            BuiltInActionIds.CompressImage,
            Authoritative("C:\\Temp\\other.jpg"),
            Capabilities,
            Settings,
            OutputPolicy.SafeMvpDefault);

        Assert.False(result.IsAccepted);
        Assert.Equal(ActionCommitStatus.PreflightMismatch, result.Status);
    }

    [Fact]
    public void TryCommitDrop_ActionNotOfferedByOverlay_IsRejected()
    {
        var registry = new ActionRegistry(BuiltInActionDefinitions.Mvp.Where(x => x.Id == BuiltInActionIds.CompressImage));
        var sut = new DragWorkflowOrchestrator(registry);
        var prepared = sut.PrepareOverlay(Guid.NewGuid(), Preflight("C:\\Temp\\photo.jpg"), Capabilities, Settings).Session!;

        var result = sut.TryCommitDrop(
            prepared,
            BuiltInActionIds.ConvertImageToWebP,
            Authoritative("C:\\Temp\\photo.jpg"),
            Capabilities,
            Settings,
            OutputPolicy.SafeMvpDefault);

        Assert.False(result.IsAccepted);
        Assert.Equal(ActionCommitStatus.ActionWasNotOffered, result.Status);
    }

    [Fact]
    public void TryCommitDrop_PreserveSourceFalse_IsRejected()
    {
        var sut = CreateSut();
        var prepared = sut.PrepareOverlay(Guid.NewGuid(), Preflight("C:\\Temp\\photo.jpg"), Capabilities, Settings).Session!;
        var unsafePolicy = OutputPolicy.SafeMvpDefault with { PreserveSource = false };

        var result = sut.TryCommitDrop(
            prepared,
            BuiltInActionIds.CompressImage,
            Authoritative("C:\\Temp\\photo.jpg"),
            Capabilities,
            Settings,
            unsafePolicy);

        Assert.False(result.IsAccepted);
        Assert.Equal(ActionCommitStatus.UnsafeOutputPolicy, result.Status);
    }

    [Fact]
    public void TryCommitDrop_CapabilityDisappeared_AfterOverlay_IsRejected()
    {
        var sut = CreateSut();
        var prepared = sut.PrepareOverlay(Guid.NewGuid(), Preflight("C:\\Temp\\photo.jpg"), Capabilities, Settings).Session!;
        var unavailable = new AppCapabilities { ImagingAvailable = false };

        var result = sut.TryCommitDrop(
            prepared,
            BuiltInActionIds.CompressImage,
            Authoritative("C:\\Temp\\photo.jpg"),
            unavailable,
            Settings,
            OutputPolicy.SafeMvpDefault);

        Assert.False(result.IsAccepted);
        Assert.Equal(ActionCommitStatus.ActionNoLongerAvailable, result.Status);
    }

    private static DragWorkflowOrchestrator CreateSut() =>
        new(new ActionRegistry(BuiltInActionDefinitions.Mvp));

    private static PayloadQualificationResult Preflight(string path) => Qualification(path, PayloadQualificationState.Eligible);
    private static PayloadQualificationResult Authoritative(string path) => Qualification(path, PayloadQualificationState.Supported);

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
}

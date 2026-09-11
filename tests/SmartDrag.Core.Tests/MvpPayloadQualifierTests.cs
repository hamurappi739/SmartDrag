using SmartDrag.Core.Payload;
using Xunit;

namespace SmartDrag.Core.Tests;

public sealed class MvpPayloadQualifierTests
{
    [Fact]
    public void ExplorerSelection_SingleExistingPng_IsEligibleButNotAuthoritative()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.ExplorerSelectionSnapshot,
            Candidate(@"C:\\work\\image.png", exists: true)));

        Assert.Equal(PayloadQualificationState.Eligible, result.State);
        Assert.True(result.MayShowOverlay);
        Assert.False(result.IsAuthoritative);
    }

    [Fact]
    public void OleDataObject_SingleExistingJpeg_IsSupportedAndAuthoritative()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.OleDataObject,
            Candidate(@"C:\\work\\image.jpg", exists: true)));

        Assert.Equal(PayloadQualificationState.Supported, result.State);
        Assert.True(result.IsAuthoritative);
    }

    [Fact]
    public void LocalFilePicker_SingleExistingPng_IsSupportedAndAuthoritative()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.LocalFilePicker,
            Candidate(@"C:\\work\\image.png", exists: true)));

        Assert.Equal(PayloadQualificationState.Supported, result.State);
        Assert.True(result.IsAuthoritative);
    }

    [Fact]
    public void MultipleFiles_AreRejected()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.ExplorerSelectionSnapshot,
            Candidate(@"C:\\work\\one.png", true),
            Candidate(@"C:\\work\\two.png", true)));

        Assert.Equal(PayloadQualificationState.Rejected, result.State);
        Assert.Equal(PayloadRejectionReason.MultipleFiles, result.Reason);
    }

    [Fact]
    public void Directory_IsRejected()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.ExplorerSelectionSnapshot,
            new FilePayloadCandidate
            {
                FullPath = @"C:\\work\\folder",
                DisplayName = "folder",
                Extension = string.Empty,
                Exists = true,
                IsDirectory = true
            }));

        Assert.Equal(PayloadRejectionReason.DirectoryNotSupported, result.Reason);
    }

    [Fact]
    public void PreG3_WebP_IsConservativelyRejectedUntilCodecGateExpandsMatrix()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.ExplorerSelectionSnapshot,
            Candidate(@"C:\\work\\image.webp", exists: true)));

        Assert.Equal(PayloadQualificationState.Rejected, result.State);
        Assert.Equal(PayloadRejectionReason.UnsupportedExtension, result.Reason);
    }

    [Fact]
    public void PreflightExtension_IsDerivedFromFullPathInsteadOfDisplayMetadata()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.ExplorerSelectionSnapshot,
            new FilePayloadCandidate
            {
                FullPath = @"C:\\work\\image.png",
                DisplayName = "image",
                Extension = ".txt",
                Exists = true,
                IsDirectory = false
            }));

        Assert.Equal(PayloadQualificationState.Eligible, result.State);
        Assert.Equal(".png", Assert.Single(result.Payload!.Files).Extension);
    }

    [Fact]
    public void AccessibilityNameWithoutPath_RemainsUnknown()
    {
        var result = MvpPayloadQualifier.Qualify(new FilePayloadSnapshot
        {
            EvidenceSource = PayloadEvidenceSource.AccessibilityEvent,
            Files = new[]
            {
                new FilePayloadCandidate
                {
                    DisplayName = "image.png",
                    Extension = ".png",
                    Exists = true,
                    IsDirectory = false
                }
            }
        });

        Assert.Equal(PayloadQualificationState.Unknown, result.State);
        Assert.False(result.MayShowOverlay);
    }


    [Fact]
    public void MalformedResolvedPath_IsRejectedWithoutThrowing()
    {
        var result = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.OleDataObject,
            new FilePayloadCandidate
            {
                FullPath = "bad\0path.png",
                DisplayName = null,
                Extension = ".png",
                Exists = true,
                IsDirectory = false
            }));

        Assert.Equal(PayloadQualificationState.Rejected, result.State);
        Assert.Equal(PayloadRejectionReason.PathUnavailable, result.Reason);
    }

    [Fact]
    public void AuthoritativePathMustMatchPreflight()
    {
        var preflight = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.ExplorerSelectionSnapshot,
            Candidate(@"C:\\work\\one.png", true)));
        var authoritative = MvpPayloadQualifier.Qualify(Snapshot(
            PayloadEvidenceSource.OleDataObject,
            Candidate(@"C:\\work\\two.png", true)));

        Assert.False(MvpPayloadQualifier.PathsMatchPreflight(preflight, authoritative));
    }

    private static FilePayloadSnapshot Snapshot(PayloadEvidenceSource source, params FilePayloadCandidate[] files) => new()
    {
        EvidenceSource = source,
        Files = files
    };

    private static FilePayloadCandidate Candidate(string path, bool exists) => new()
    {
        FullPath = path,
        DisplayName = Path.GetFileName(path),
        Extension = Path.GetExtension(path),
        Exists = exists,
        IsDirectory = false
    };
}

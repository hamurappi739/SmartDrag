using SmartDrag.Core.Actions;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;
using SmartDrag.Runtime;
using Xunit;

namespace SmartDrag.Runtime.Tests;

public sealed class CompletionProjectorTests
{
    private static readonly CompletionCapabilities SafeCapabilities = new()
    {
        CanOpenContainingFolder = true,
        CanCopyResultPath = true,
        CanStartResultDrag = false,
        CanDeleteGeneratedOutput = false
    };

    [Fact]
    public void CompletedJob_ProjectsOnlyImplementedSafeCommandsAndMetrics()
    {
        var snapshot = CompletedGeneratedJob();

        var model = CompletionProjector.FromTerminalJob(snapshot, SafeCapabilities);

        Assert.Equal(CompletionStatus.Completed, model.Status);
        Assert.Equal(13_800_000, model.BytesSaved);
        Assert.Contains(CompletionCommand.OpenContainingFolder, model.Commands);
        Assert.Contains(CompletionCommand.CopyResultPath, model.Commands);
        Assert.DoesNotContain(CompletionCommand.StartResultDrag, model.Commands);
        Assert.DoesNotContain(CompletionCommand.DeleteGeneratedOutput, model.Commands);
        Assert.Contains(CompletionCommand.Dismiss, model.Commands);
    }

    [Fact]
    public void DeleteRequiresBothCapabilityAndGeneratedOwnership()
    {
        var capabilities = SafeCapabilities with { CanDeleteGeneratedOutput = true };
        var model = CompletionProjector.FromTerminalJob(CompletedGeneratedJob(), capabilities);

        Assert.Contains(CompletionCommand.DeleteGeneratedOutput, model.Commands);
    }

    [Fact]
    public void ProjectedCommands_AreReadOnly()
    {
        var model = CompletionProjector.FromTerminalJob(CompletedGeneratedJob(), SafeCapabilities);

        var commands = Assert.IsAssignableFrom<IList<CompletionCommand>>(model.Commands);

        Assert.True(commands.IsReadOnly);
    }

    [Fact]
    public void FailedJob_DoesNotOfferResultFileCommands()
    {
        var snapshot = new JobSnapshot
        {
            Id = JobId.New(),
            RequestId = Guid.NewGuid(),
            ActionId = BuiltInActionIds.ConvertImageToWebP,
            State = JobState.Failed,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        var model = CompletionProjector.FromTerminalJob(snapshot, SafeCapabilities);

        Assert.Equal(CompletionStatus.Failed, model.Status);
        Assert.Equal(new[] { CompletionCommand.Dismiss }, model.Commands);
    }

    [Fact]
    public void CompletedJob_WithoutGeneratedOwnership_DoesNotOfferDelete()
    {
        var snapshot = new JobSnapshot
        {
            Id = JobId.New(),
            RequestId = Guid.NewGuid(),
            ActionId = BuiltInActionIds.CompressImage,
            State = JobState.Completed,
            EnqueuedAt = DateTimeOffset.UtcNow,
            InputPaths = new[] { "source.png" },
            OutputPaths = new[] { "external-result.png" }
        };

        var model = CompletionProjector.FromTerminalJob(
            snapshot,
            SafeCapabilities with { CanDeleteGeneratedOutput = true });

        Assert.DoesNotContain(CompletionCommand.DeleteGeneratedOutput, model.Commands);
    }

    [Fact]
    public void NonTerminalJob_IsRejected()
    {
        var snapshot = new JobSnapshot
        {
            Id = JobId.New(),
            RequestId = Guid.NewGuid(),
            ActionId = BuiltInActionIds.RemoveImageMetadata,
            State = JobState.Running,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => CompletionProjector.FromTerminalJob(snapshot, SafeCapabilities));
    }

    private static JobSnapshot CompletedGeneratedJob() => new()
    {
        Id = JobId.New(),
        RequestId = Guid.NewGuid(),
        ActionId = BuiltInActionIds.CompressImage,
        State = JobState.Completed,
        EnqueuedAt = DateTimeOffset.UtcNow,
        InputPaths = new[] { @"C:\work\source.png" },
        OutputPaths = new[] { @"C:\work\source-compressed.png" },
        GeneratedArtifacts = new[]
        {
            StrongArtifact(@"C:\work\source-compressed.png")
        },
        Metrics = new ActionMetrics
        {
            SourceSizeBytes = 15_000_000,
            OutputSizeBytes = 1_200_000
        }
    };
    private static GeneratedArtifact StrongArtifact(string path) => new()
    {
        Path = path,
        Identity = new ArtifactIdentity
        {
            Scheme = "test-id-v1",
            VolumeId = "VOL",
            ObjectId = "OBJ"
        }
    };

}

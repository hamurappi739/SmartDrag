using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;
using SmartDrag.Presentation;
using Xunit;

namespace SmartDrag.Presentation.Tests;

public sealed class MvpPresentationPolicyTests
{
    [Fact]
    public void DiagnosticsPolicy_ReadsAggregateCountsWithoutTechnicalDetails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"smartdrag-diagnostics-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {"Passed":true,"DurationMs":42,"Checks":[{"Name":"one","Passed":true},{"Name":"two","Passed":true}]}
            """);
        try
        {
            Assert.True(PreviewDiagnosticsPolicy.TryReadSummary(path, out var summary));
            Assert.True(summary.Passed);
            Assert.Equal(2, summary.PassedChecks);
            Assert.Equal(2, summary.TotalChecks);
            Assert.Equal(42, summary.DurationMs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DiagnosticsPolicy_RejectsMalformedOrOversizedReport()
    {
        var path = Path.Combine(Path.GetTempPath(), $"smartdrag-diagnostics-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "not-json");
        try
        {
            Assert.False(PreviewDiagnosticsPolicy.TryReadSummary(path, out _));
        }
        finally
        {
            File.Delete(path);
        }

        var oversizedPath = Path.Combine(Path.GetTempPath(), $"smartdrag-diagnostics-{Guid.NewGuid():N}.json");
        File.WriteAllText(oversizedPath, $"{{\"Passed\":true,\"Checks\":[],\"Padding\":\"{new string('x', 65 * 1024)}\"}}");
        try
        {
            Assert.False(PreviewDiagnosticsPolicy.TryReadSummary(oversizedPath, out _));
        }
        finally
        {
            File.Delete(oversizedPath);
        }
    }

    [Fact]
    public void DiagnosticsPolicy_FormatsRussianAndEnglishSummary()
    {
        var summary = new PreviewDiagnosticsSummary
        {
            Passed = false,
            PassedChecks = 2,
            TotalChecks = 3,
            DurationMs = 12
        };

        Assert.Equal("Self-check found issues (2/3) · 12 ms", PreviewDiagnosticsPolicy.FormatSummary(summary, russian: false));
        Assert.Equal("Самопроверка: есть проблемы (2/3) · 12 мс", PreviewDiagnosticsPolicy.FormatSummary(summary, russian: true));
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, true)]
    [InlineData(true, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, true, false)]
    public void DiagnosticsPolicy_AllowsSelfCheckOnlyWhenPreviewIsIdle(
        bool inputBusy,
        bool pickerBusy,
        bool activeDrag,
        bool activeOperation,
        bool visibleCompletion,
        bool selfCheckRunning,
        bool expected)
    {
        Assert.Equal(expected, PreviewDiagnosticsPolicy.CanRunSelfCheck(
            inputBusy,
            pickerBusy,
            activeDrag,
            activeOperation,
            visibleCompletion,
            selfCheckRunning));
    }

    [Fact]
    public void ProcessLog_RotatesAndBoundsEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"smartdrag-preview-log-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, new string('x', (int)PreviewProcessLog.MaximumLogBytes + 1));
        try
        {
            Assert.True(PreviewProcessLog.TryAppend(path, "test-event", new string('d', PreviewProcessLog.MaximumDetailCharacters + 10)));
            Assert.True(File.Exists(path + ".1"));
            var current = File.ReadAllText(path);
            Assert.Contains("test-event", current, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('d', PreviewProcessLog.MaximumDetailCharacters + 1), current, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".1");
        }
    }

    [Fact]
    public void ProcessLog_RejectsMissingPathOrEvent()
    {
        Assert.False(PreviewProcessLog.TryAppend(null, "event"));
        Assert.False(PreviewProcessLog.TryAppend(Path.Combine(Path.GetTempPath(), "ignored.log"), " "));
    }

    [Fact]
    public async Task SafeTaskRunner_ObservesFailureAndInvokesCallback()
    {
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        SafeTaskRunner.FireAndForget(
            () => Task.FromException(new InvalidOperationException("delayed failure")),
            "presentation-test",
            exception => observed.TrySetResult(exception));

        var exception = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("delayed failure", exception.Message);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void FilePickerPolicy_OnlyAllowsIdlePreview(
        bool inputBusy,
        bool activeDrag,
        bool activeOperation,
        bool expected)
    {
        Assert.Equal(expected, PreviewFilePickerPolicy.CanPickImage(inputBusy, activeDrag, activeOperation));
    }

    [Theory]
    [InlineData(true, false, false, false, false, true)]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(true, true, false, false, false, false)]
    [InlineData(true, false, true, false, false, false)]
    [InlineData(true, false, false, true, false, false)]
    public void DropPolicy_OnlyAdvertisesDropWhenPreviewIsIdle(
        bool hasFileDrop,
        bool inputBusy,
        bool pickerBusy,
        bool activeDrag,
        bool activeOperation,
        bool expected)
    {
        Assert.Equal(expected, PreviewDropPolicy.CanAccept(
            hasFileDrop,
            inputBusy,
            pickerBusy,
            activeDrag,
            activeOperation));
    }

    [Fact]
    public void ResultActionPolicy_HidesOutputCommandsWhenOutputIsUnavailableOrDeleted()
    {
        var commands = new[]
        {
            CompletionCommand.CopyResultPath,
            CompletionCommand.OpenContainingFolder,
            CompletionCommand.DeleteGeneratedOutput,
            CompletionCommand.Dismiss
        };

        var missing = PreviewResultActionPolicy.Resolve(commands, outputAvailable: false, outputWasDeleted: false);
        Assert.True(missing.ShowActionRow);
        Assert.False(missing.ShowCopyPath);
        Assert.False(missing.ShowOpenFolder);
        Assert.False(missing.ShowDeleteOutput);
        Assert.True(missing.ShowDismiss);

        var deleted = PreviewResultActionPolicy.Resolve(commands, outputAvailable: true, outputWasDeleted: true);
        Assert.False(deleted.ShowCopyPath);
        Assert.False(deleted.ShowOpenFolder);
        Assert.False(deleted.ShowDeleteOutput);
    }

    [Fact]
    public void ResultActionPolicy_UsesOnlyRuntimeGrantedCommands()
    {
        var state = PreviewResultActionPolicy.Resolve(
            new[] { CompletionCommand.CopyResultPath, CompletionCommand.Dismiss },
            outputAvailable: true,
            outputWasDeleted: false);

        Assert.True(state.ShowCopyPath);
        Assert.False(state.ShowOpenFolder);
        Assert.False(state.ShowDeleteOutput);
        Assert.True(state.ShowDismiss);
    }

    [Fact]
    public void CommandErrorPolicy_NeverDisplaysAdapterDetails()
    {
        const string technical = "System.UnauthorizedAccessException: C:\\Users\\private\\secret.png (0x80070005)";

        var english = PreviewCommandErrorPolicy.Localize(
            CompletionCommand.OpenContainingFolder,
            technical,
            russian: false);
        var russian = PreviewCommandErrorPolicy.Localize(
            CompletionCommand.DeleteGeneratedOutput,
            technical,
            russian: true);

        Assert.Equal("Could not open the output folder.", english);
        Assert.Equal("Не удалось удалить созданный файл.", russian);
        Assert.DoesNotContain("private", english, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x80070005", russian, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandErrorPolicy_ReturnsNothingForMissingError()
    {
        Assert.Null(PreviewCommandErrorPolicy.Localize(CompletionCommand.CopyResultPath, null, russian: false));
        Assert.Null(PreviewCommandErrorPolicy.Localize(CompletionCommand.CopyResultPath, " ", russian: true));
    }

    [Fact]
    public void FailurePolicy_UsesStableUserSafeCopy()
    {
        const string technicalReason = "PayloadNotAuthoritative: C:\\Users\\private\\source.png";

        var image = PreviewFailurePolicy.ImageRejected(russian: false);
        var panel = PreviewFailurePolicy.ActionPanelUnavailable(russian: true);
        var action = PreviewFailurePolicy.ActionNotStarted(russian: false);

        Assert.Contains("original file", image, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Действие не запущено", panel, StringComparison.Ordinal);
        Assert.Contains("original file", action, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PayloadNotAuthoritative", image, StringComparison.Ordinal);
        Assert.DoesNotContain("private", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(technicalReason, action, StringComparison.Ordinal);
    }

    [Fact]
    public void FailurePolicy_DoesNotEchoTechnicalDetails()
    {
        var messages = new[]
        {
            PreviewFailurePolicy.ImageRejected(russian: true),
            PreviewFailurePolicy.ActionPanelUnavailable(russian: false),
            PreviewFailurePolicy.ActionNotStarted(russian: true)
        };

        Assert.All(messages, message =>
        {
            Assert.DoesNotContain("\\", message, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0x", message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void HistoryDisplayPolicy_MapsUnknownValuesToSafeLabels()
    {
        const string technical = "C:\\Users\\private\\SmartDrag.Exception";

        Assert.Equal("Operation", PreviewHistoryDisplayPolicy.LocalizeState(technical, russian: false));
        Assert.Equal("Действие SmartDrag", PreviewHistoryDisplayPolicy.LocalizeAction(technical, russian: true));
        Assert.Equal("SmartDrag.Exception", PreviewHistoryDisplayPolicy.SafeName(technical));
    }

    [Fact]
    public void HistoryDisplayPolicy_PreservesKnownVocabularyAndStripsPaths()
    {
        Assert.Equal("Готово", PreviewHistoryDisplayPolicy.LocalizeState("Completed", russian: true));
        Assert.Equal("Remove Metadata", PreviewHistoryDisplayPolicy.LocalizeAction("image.remove-metadata", russian: false));
        Assert.Equal("photo.png", PreviewHistoryDisplayPolicy.SafeName("C:\\Users\\private\\photo.png"));
    }

    [Theory]
    [InlineData(true, false, false, 1, PreviewInputDecision.IgnoreBusy)]
    [InlineData(false, true, false, 1, PreviewInputDecision.FinishActiveDrag)]
    [InlineData(false, false, true, 1, PreviewInputDecision.DismissVisibleCompletion)]
    [InlineData(false, false, false, 0, PreviewInputDecision.RejectPayload)]
    [InlineData(false, false, false, 2, PreviewInputDecision.RejectPayload)]
    [InlineData(false, false, false, 1, PreviewInputDecision.InspectSingleFile)]
    public void InputPolicy_ResolvesSafeTransition(
        bool inputBusy,
        bool activeDrag,
        bool visibleCompletion,
        int fileCount,
        PreviewInputDecision expected)
    {
        Assert.Equal(
            expected,
            PreviewInputPolicy.Decide(inputBusy, activeDrag, visibleCompletion, fileCount));
    }

    [Theory]
    [InlineData(PreviewKeyboardKey.Escape, PreviewKeyboardModifiers.None, true, false, false, false, PreviewKeyboardIntent.CancelOperation)]
    [InlineData(PreviewKeyboardKey.Escape, PreviewKeyboardModifiers.None, false, true, false, false, PreviewKeyboardIntent.DismissCompletion)]
    [InlineData(PreviewKeyboardKey.Escape, PreviewKeyboardModifiers.None, false, false, true, false, PreviewKeyboardIntent.CancelDrag)]
    [InlineData(PreviewKeyboardKey.Escape, PreviewKeyboardModifiers.None, false, false, false, false, PreviewKeyboardIntent.ResetPreview)]
    [InlineData(PreviewKeyboardKey.Delete, PreviewKeyboardModifiers.None, false, true, false, true, PreviewKeyboardIntent.DeleteOutput)]
    [InlineData(PreviewKeyboardKey.Delete, PreviewKeyboardModifiers.None, false, true, false, false, PreviewKeyboardIntent.None)]
    public void KeyboardPolicy_ResolvesStatefulEscapeAndDelete(
        PreviewKeyboardKey key,
        PreviewKeyboardModifiers modifiers,
        bool hasOperation,
        bool hasCompletion,
        bool hasDrag,
        bool canDelete,
        PreviewKeyboardIntent expected)
    {
        var intent = PreviewKeyboardPolicy.Resolve(
            key,
            modifiers,
            new PreviewKeyboardState(hasOperation, hasCompletion, hasDrag, canDelete));

        Assert.Equal(expected, intent);
    }

    [Theory]
    [InlineData(PreviewKeyboardKey.V, PreviewKeyboardModifiers.Control, PreviewKeyboardIntent.PasteFiles)]
    [InlineData(PreviewKeyboardKey.O, PreviewKeyboardModifiers.Control, PreviewKeyboardIntent.ChooseImage)]
    [InlineData(PreviewKeyboardKey.L, PreviewKeyboardModifiers.Control, PreviewKeyboardIntent.ToggleLanguage)]
    [InlineData(PreviewKeyboardKey.T, PreviewKeyboardModifiers.Control | PreviewKeyboardModifiers.Shift, PreviewKeyboardIntent.ToggleTheme)]
    [InlineData(PreviewKeyboardKey.H, PreviewKeyboardModifiers.Control | PreviewKeyboardModifiers.Shift, PreviewKeyboardIntent.ClearHistory)]
    [InlineData(PreviewKeyboardKey.H, PreviewKeyboardModifiers.Control, PreviewKeyboardIntent.None)]
    public void KeyboardPolicy_ResolvesExactGlobalGestures(
        PreviewKeyboardKey key,
        PreviewKeyboardModifiers modifiers,
        PreviewKeyboardIntent expected)
    {
        var intent = PreviewKeyboardPolicy.Resolve(
            key,
            modifiers,
            new PreviewKeyboardState(false, false, false, false));

        Assert.Equal(expected, intent);
    }

    [Fact]
    public void RunningJob_UsesIndeterminateProgressAndDoesNotExposeFullPath()
    {
        var snapshot = Job(JobState.Running, @"C:\Users\Someone\Private\photo.png");

        var model = MvpPresentationPolicy.ProjectOperation(new[] { snapshot }, _ => "Compress");

        Assert.NotNull(model);
        Assert.True(model!.UsesIndeterminateProgress);
        Assert.True(model.CanCancel);
        Assert.Equal("photo.png", model.SourceDisplayName);
        Assert.DoesNotContain("Users", model.SourceDisplayName, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void DisplayName_SanitizesControlCharactersWithoutLeakingParentPath()
    {
        var snapshot = Job(JobState.Running, "C:\\Secret\\bad\u0001name.png");
        var model = MvpPresentationPolicy.ProjectOperation(new[] { snapshot }, _ => "Compress");
        Assert.Equal("bad�name.png", model!.SourceDisplayName);
        Assert.DoesNotContain("Secret", model.SourceDisplayName);
    }

    [Fact]
    public void CancellingJob_DisablesCancelIntent()
    {
        var model = MvpPresentationPolicy.ProjectOperation(new[] { Job(JobState.Cancelling, "photo.png") }, _ => "Compress");
        Assert.NotNull(model);
        Assert.False(model!.CanCancel);
        Assert.Equal("Cancelling…", model.StatusText);
    }

    [Fact]
    public void OperationProjection_ReportsQueuedBehindCountAndSafeSourceName()
    {
        var running = Job(JobState.Running, @"C:\Private\first.png");
        var queued = Job(JobState.Queued, @"C:\Private\second.png");

        var model = MvpPresentationPolicy.ProjectOperation(new[] { running, queued }, _ => "Compress");

        Assert.NotNull(model);
        Assert.Equal(1, model!.QueuedBehindCount);
        Assert.Equal("first.png", model.SourceDisplayName);
        Assert.DoesNotContain("Private", model.SourceDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompressionCompletion_ShowsMeasuredSavingsOnlyWhenPositive()
    {
        var completion = new CompletionModel
        {
            JobId = JobId.New(),
            Status = CompletionStatus.Completed,
            ActionId = BuiltInActionIds.CompressImage,
            SourceSizeBytes = 10_000_000,
            OutputSizeBytes = 2_500_000,
            Commands = new[] { CompletionCommand.Dismiss }
        };

        var model = MvpPresentationPolicy.ProjectCompletion(completion);

        Assert.Equal("Compressed", model.Title);
        Assert.Contains("Saved", model.Detail);
        Assert.Contains("75", model.Detail);
    }

    [Fact]
    public void CompletionProjection_ExposesReadOnlyCommandList()
    {
        var completion = new CompletionModel
        {
            JobId = JobId.New(),
            Status = CompletionStatus.Completed,
            ActionId = BuiltInActionIds.CompressImage,
            Commands = new[] { CompletionCommand.CopyResultPath, CompletionCommand.Dismiss }
        };

        var model = MvpPresentationPolicy.ProjectCompletion(completion);
        var commands = Assert.IsAssignableFrom<IList<CompletionCommand>>(model.Commands);

        Assert.True(commands.IsReadOnly);
    }

    [Fact]
    public void FailedCompletion_UsesUserMessageNotTechnicalMessage()
    {
        var completion = new CompletionModel
        {
            JobId = JobId.New(),
            Status = CompletionStatus.Failed,
            ActionId = BuiltInActionIds.CompressImage,
            Error = new AppError(ErrorCode.EncodeFailed, @"C:\Secret\file.png exploded", "Could not encode this image."),
            Commands = new[] { CompletionCommand.Dismiss }
        };

        var model = MvpPresentationPolicy.ProjectCompletion(completion);

        Assert.Equal("Could not encode this image.", model.Detail);
        Assert.DoesNotContain("Secret", model.Detail);
    }

    private static JobSnapshot Job(JobState state, string path) => new()
    {
        Id = JobId.New(),
        RequestId = Guid.NewGuid(),
        ActionId = BuiltInActionIds.CompressImage,
        State = state,
        EnqueuedAt = DateTimeOffset.UtcNow,
        InputPaths = new[] { path }
    };
}

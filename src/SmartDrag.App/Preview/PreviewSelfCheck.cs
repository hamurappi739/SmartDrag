using System.Diagnostics;
using System.IO;
using System.Text.Json;
using SmartDrag.App.Composition;
using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Output;
using SmartDrag.Core.Primitives;
using SmartDrag.Imaging;
using SmartDrag.Infrastructure;
using SmartDrag.Infrastructure.Preferences;
using SmartDrag.Infrastructure.Recovery;
using SmartDrag.Runtime;
using SmartDrag.Windows.Artifacts;
using SmartDrag.Windows.Completion;
using SmartDrag.Windows.Imaging;

namespace SmartDrag.App.Preview;

/// <summary>
/// Headless smoke check for the preview's real codec/guard/output/queue/completion path. It is intentionally
/// separate from the WPF window so CI or a handoff reviewer can validate the local vertical slice without manual UI.
/// </summary>
public static class PreviewSelfCheck
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> RunAsync(string? requestedReportPath = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<SelfCheckEntry>();
        var reportPath = ResolveReportPath(requestedReportPath);
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var fixtureDirectory = FindFixtureDirectory();
            var sourceJpeg = Path.Combine(fixtureDirectory, "rgb-photo.jpg");
            var sourcePng = Path.Combine(fixtureDirectory, "rgb-basic.png");
            var truncated = Path.Combine(fixtureDirectory, "truncated.jpg");
            RequireFiles(sourceJpeg, sourcePng, truncated);

            var limits = new ImageSafetyLimits
            {
                MaxSourceBytes = 100L * 1024 * 1024,
                MaxDecodedPixels = 64L * 1024 * 1024,
                MaxDimension = 12_000
            };
            checks.Add(Check("native-activation-disabled", !ProductionActivationPolicy.NativeActivationEnabled));
            var codec = new WindowsWicImageCodec();
            var guarded = new GuardedImageProcessor(codec, codec, limits);
            var identity = new WindowsGeneratedArtifactIdentityService();
            var outputDirectory = Directory.CreateTempSubdirectory("smartdrag-preview-self-check-");
            try
            {
                // SafeMvpDefault intentionally writes beside its source. Work on private copies so the committed
                // corpus remains immutable even when this self-check is run from the repository root.
                var testJpeg = Path.Combine(outputDirectory.FullName, "rgb-photo.jpg");
                var testPng = Path.Combine(outputDirectory.FullName, "rgb-basic.png");
                var testTruncated = Path.Combine(outputDirectory.FullName, "truncated.jpg");
                File.Copy(sourceJpeg, testJpeg);
                File.Copy(sourcePng, testPng);
                File.Copy(truncated, testTruncated);

                var identityProbePath = Path.Combine(outputDirectory.FullName, "identity-probe.bin");
                await File.WriteAllTextAsync(identityProbePath, "original-generated-object");
                var identityCapture = await identity.CaptureAsync(identityProbePath, CancellationToken.None);
                checks.Add(Check("artifact-identity-captured", identityCapture.Success && identityCapture.Identity is not null));
                var identityArtifact = new GeneratedArtifact
                {
                    Path = identityProbePath,
                    Identity = identityCapture.Identity
                };
                var identityDelete = await identity.DeleteIfIdentityMatchesAsync(identityArtifact, CancellationToken.None);
                checks.Add(Check("artifact-identity-delete", identityDelete.Success && !File.Exists(identityProbePath)));

                await File.WriteAllTextAsync(identityProbePath, "replacement-object");
                var replacementDelete = await identity.DeleteIfIdentityMatchesAsync(identityArtifact, CancellationToken.None);
                checks.Add(Check(
                    "artifact-replacement-protected",
                    replacementDelete.Status == GeneratedArtifactDeletionStatus.IdentityMismatch
                        && File.Exists(identityProbePath)
                        && await File.ReadAllTextAsync(identityProbePath) == "replacement-object"));

                var preferencesPath = Path.Combine(outputDirectory.FullName, "preferences.json");
                var preferencesStore = new PreviewPreferencesStore(preferencesPath);
                var preferencesSaved = preferencesStore.TrySave(new PreviewPreferences
                {
                    Language = "en",
                    DarkTheme = true
                });
                var loadedPreferences = preferencesStore.Load();
                checks.Add(Check(
                    "preferences-roundtrip",
                    preferencesSaved && loadedPreferences.Language == "en" && loadedPreferences.DarkTheme));

                var historyPath = Path.Combine(outputDirectory.FullName, "history.json");
                var historyStore = new PreviewHistoryStore(historyPath);
                var historySaved = historyStore.TrySave(new[]
                {
                    new PreviewHistoryEntry
                    {
                        Id = "self-check-history",
                        State = nameof(JobState.Completed),
                        ActionId = BuiltInActionIds.CompressImage.Value,
                        SourceName = $"{outputDirectory.FullName}\\private-source.jpg",
                        OutputName = $"{outputDirectory.FullName}\\private-output.jpg",
                        OccurredAt = DateTimeOffset.UtcNow
                    }
                });
                var loadedHistory = historyStore.Load();
                checks.Add(Check(
                    "history-sanitized-roundtrip",
                    historySaved && loadedHistory.Count == 1
                        && loadedHistory[0].SourceName == "private-source.jpg"
                        && loadedHistory[0].OutputName == "private-output.jpg"
                        && !File.ReadAllText(historyPath).Contains(outputDirectory.FullName, StringComparison.OrdinalIgnoreCase)));

                var outputManager = new PhysicalOutputManager(new InMemoryOutputRecoveryJournal(), identity);
                var handlers = new IActionHandler[]
                {
                    new CompressImageActionHandler(guarded, outputManager),
                    new ConvertImageToWebPActionHandler(guarded, outputManager),
                    new RemoveImageMetadataActionHandler(guarded, outputManager)
                };
                await using var queue = new SequentialJobQueue(new ActionExecutor(handlers));
                var completionCommands = new CompletionCommandExecutor(queue, new WindowsCompletionPlatformService());
                using var completionCoordinator = new CompletionCoordinator(queue, completionCommands.Capabilities);
                var completions = new List<CompletionModel>();
                completionCoordinator.CompletionReady += (_, args) => completions.Add(args.Completion);

                var jpegInspection = await codec.InspectAsync(testJpeg, CancellationToken.None);
                var pngInspection = await codec.InspectAsync(testPng, CancellationToken.None);
                checks.Add(Check("inspect-jpeg", jpegInspection.Success && jpegInspection.Format == ImageFormatKind.Jpeg));
                checks.Add(Check("inspect-png", pngInspection.Success && pngInspection.Format == ImageFormatKind.Png));
                checks.Add(Check("guard-jpeg", MvpImageExecutionGuard.Evaluate(jpegInspection, limits).Allowed));
                checks.Add(Check("guard-png", MvpImageExecutionGuard.Evaluate(pngInspection, limits).Allowed));

                var webpTemporaryOutput = Path.Combine(outputDirectory.FullName, "webp-should-not-exist.webp");
                var webpResult = await guarded.ConvertToWebPAsync(new ImageProcessingRequest
                {
                    SourcePath = testJpeg,
                    TemporaryOutputPath = webpTemporaryOutput
                }, CancellationToken.None);
                checks.Add(Check(
                    "webp-controlled-unavailable",
                    !webpResult.Success
                        && webpResult.Error?.Code == ErrorCode.UnsupportedFormat
                        && !File.Exists(webpTemporaryOutput)));

                var sourceJpegBytes = await File.ReadAllBytesAsync(testJpeg);
                var jpegJob = queue.Enqueue(new ActionRequest
                {
                    RequestId = Guid.NewGuid(),
                    ActionId = BuiltInActionIds.CompressImage,
                    InputPaths = new[] { testJpeg },
                    OutputPolicy = OutputPolicy.SafeMvpDefault
                });
                var pngJob = queue.Enqueue(new ActionRequest
                {
                    RequestId = Guid.NewGuid(),
                    ActionId = BuiltInActionIds.RemoveImageMetadata,
                    InputPaths = new[] { testPng },
                    OutputPolicy = OutputPolicy.SafeMvpDefault
                });
                var badJob = queue.Enqueue(new ActionRequest
                {
                    RequestId = Guid.NewGuid(),
                    ActionId = BuiltInActionIds.CompressImage,
                    InputPaths = new[] { testTruncated },
                    OutputPolicy = OutputPolicy.SafeMvpDefault
                });

                await WaitForTerminalAsync(queue, jpegJob, pngJob, badJob);
                var jpegSnapshot = queue.TryGetSnapshot(jpegJob, out var jpegTerminal) ? jpegTerminal : null;
                var pngSnapshot = queue.TryGetSnapshot(pngJob, out var pngTerminal) ? pngTerminal : null;
                var badSnapshot = queue.TryGetSnapshot(badJob, out var badTerminal) ? badTerminal : null;
                checks.Add(Check("queue-jpeg-completed", jpegSnapshot?.State == JobState.Completed));
                checks.Add(Check("queue-png-completed", pngSnapshot?.State == JobState.Completed));
                checks.Add(Check("queue-truncated-failed", badSnapshot?.State == JobState.Failed));
                checks.Add(Check("completion-published", completions.Count == 3));

                var cancellationActionId = new ActionId("self-check.cancellable");
                var cancellationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await using (var cancellationQueue = new SequentialJobQueue(
                    new ActionExecutor(new[] { new CancellableSelfCheckHandler(cancellationActionId, cancellationStarted) })))
                {
                    var runningCancellationJob = cancellationQueue.Enqueue(new ActionRequest
                    {
                        RequestId = Guid.NewGuid(),
                        ActionId = cancellationActionId,
                        InputPaths = new[] { testJpeg },
                        OutputPolicy = OutputPolicy.SafeMvpDefault
                    });
                    var queuedCancellationJob = cancellationQueue.Enqueue(new ActionRequest
                    {
                        RequestId = Guid.NewGuid(),
                        ActionId = cancellationActionId,
                        InputPaths = new[] { testPng },
                        OutputPolicy = OutputPolicy.SafeMvpDefault
                    });

                    await cancellationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
                    var queuedCancelRequested = cancellationQueue.TryCancel(queuedCancellationJob);
                    var runningCancelRequested = cancellationQueue.TryCancel(runningCancellationJob);
                    await WaitForTerminalAsync(cancellationQueue, runningCancellationJob, queuedCancellationJob);
                    cancellationQueue.TryGetSnapshot(runningCancellationJob, out var runningCancellationSnapshot);
                    cancellationQueue.TryGetSnapshot(queuedCancellationJob, out var queuedCancellationSnapshot);
                    checks.Add(Check(
                        "cancel-running",
                        runningCancelRequested && runningCancellationSnapshot.State == JobState.Cancelled));
                    checks.Add(Check(
                        "cancel-queued",
                        queuedCancelRequested && queuedCancellationSnapshot.State == JobState.Cancelled));
                }

                var jpegOutput = jpegSnapshot?.OutputPaths.SingleOrDefault();
                var pngOutput = pngSnapshot?.OutputPaths.SingleOrDefault();
                checks.Add(Check("jpeg-output-readable", jpegOutput is not null && (await codec.InspectAsync(jpegOutput, CancellationToken.None)).Success));
                checks.Add(Check("png-output-readable", pngOutput is not null && (await codec.InspectAsync(pngOutput, CancellationToken.None)).Success));
                checks.Add(Check("source-preserved", sourceJpegBytes.SequenceEqual(await File.ReadAllBytesAsync(testJpeg))));
                checks.Add(Check("truncated-output-absent", badSnapshot?.OutputPaths.Count == 0));
            }
            finally
            {
                try { outputDirectory.Delete(recursive: true); } catch { }
            }
        }
        catch (Exception ex)
        {
            checks.Add(new SelfCheckEntry("unexpected-exception", false, $"{ex.GetType().Name}: {ex.Message}"));
        }

        stopwatch.Stop();
        var report = new SelfCheckReport
        {
            Passed = checks.Count > 0 && checks.All(check => check.Passed),
            StartedAt = startedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            Checks = checks
        };
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine($"SmartDrag preview self-check: {(report.Passed ? "PASS" : "FAIL")}");
        Console.WriteLine($"Report: {reportPath}");
        return report.Passed ? 0 : 1;
    }

    private static async Task WaitForTerminalAsync(IJobQueue queue, params JobId[] jobs)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline && jobs.Any(job => !queue.TryGetSnapshot(job, out var snapshot) || !snapshot.IsTerminal))
        {
            await Task.Delay(25);
        }

        if (jobs.Any(job => !queue.TryGetSnapshot(job, out var snapshot) || !snapshot.IsTerminal))
        {
            throw new TimeoutException("Preview self-check queue did not reach terminal state within 15 seconds.");
        }
    }

    private static string FindFixtureDirectory()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in candidates)
        {
            DirectoryInfo? directory = new DirectoryInfo(start);
            for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "images");
                if (Directory.Exists(candidate)) return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate tests/fixtures/images for preview self-check.");
    }

    private static void RequireFiles(params string[] paths)
    {
        var missing = paths.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0) throw new FileNotFoundException($"Missing self-check fixture: {string.Join(", ", missing)}");
    }

    private static string ResolveReportPath(string? requested) => Path.GetFullPath(
        string.IsNullOrWhiteSpace(requested) ? Path.Combine("artifacts", "preview-self-check", "latest.json") : requested);

    private static SelfCheckEntry Check(string name, bool passed) => new(name, passed, passed ? null : "Condition was not met.");

    private sealed class CancellableSelfCheckHandler(ActionId actionId, TaskCompletionSource<bool> started) : IActionHandler
    {
        public ActionId ActionId { get; } = actionId;

        public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
        {
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ActionResult.Succeeded();
        }
    }

    private sealed record SelfCheckEntry(string Name, bool Passed, string? Detail);

    private sealed record SelfCheckReport
    {
        public required bool Passed { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public required long DurationMs { get; init; }
        public required IReadOnlyList<SelfCheckEntry> Checks { get; init; }
    }
}

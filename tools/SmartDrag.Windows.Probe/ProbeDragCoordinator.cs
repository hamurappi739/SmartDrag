using System.Diagnostics;
using SmartDrag.Core.Payload;
using SmartDrag.Infrastructure;
using SmartDrag.Windows.Diagnostics;
using SmartDrag.Windows.Drag;
using SmartDrag.Windows.Interop;
using SmartDrag.Windows.Shell;

namespace SmartDrag.Windows.Probe;

internal sealed class ProbeDragCoordinator
{
    private const int ActivationDelayMs = 180;
    private const double MinimumTravelPixels = 12;

    private readonly ProbeMode _mode;
    private readonly ProbeWindow _window;
    private readonly ProbeDropTarget _dropTarget;
    private readonly TimelineLogger _logger;
    private readonly ExplorerSelectionSnapshotReader _selectionReader;
    private readonly PhysicalFilePayloadSnapshotFactory _snapshotFactory;
    private Candidate? _candidate;

    public ProbeDragCoordinator(
        ProbeMode mode,
        ProbeWindow window,
        ProbeDropTarget dropTarget,
        TimelineLogger logger,
        ExplorerSelectionSnapshotReader selectionReader,
        PhysicalFilePayloadSnapshotFactory snapshotFactory)
    {
        _mode = mode;
        _window = window;
        _dropTarget = dropTarget;
        _logger = logger;
        _selectionReader = selectionReader;
        _snapshotFactory = snapshotFactory;
    }

    public void OnSignal(DragSignal signal)
    {
        var processId = ResolveProcessId(signal.SourceHwnd);
        var processName = TryGetProcessName(processId);
        _logger.Write("winevent.drag-signal", new
        {
            kind = signal.Kind.ToString(),
            hwnd = $"0x{signal.SourceHwnd:X}",
            processId,
            signal.SourceThreadId,
            signal.ObjectId,
            signal.ChildId,
            signal.EventTimeMs,
            processName
        });

        switch (signal.Kind)
        {
            case DragSignalKind.Started:
                StartCandidate(signal, processName);
                break;

            case DragSignalKind.Cancelled:
            case DragSignalKind.Completed:
                Reset();
                break;
        }
    }

    public void Tick()
    {
        if (_candidate is null || _window.IsVisible)
        {
            return;
        }

        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var elapsed = Environment.TickCount64 - _candidate.StartTick;
        var dx = cursor.X - _candidate.StartPoint.X;
        var dy = cursor.Y - _candidate.StartPoint.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (elapsed < ActivationDelayMs || distance < MinimumTravelPixels)
        {
            return;
        }

        var foregroundBefore = NativeMethods.GetForegroundWindow();
        _dropTarget.SetExpectation(new ProbePayloadExpectation(_mode, _candidate.PreflightQualification));
        try
        {
            _window.ShowNearCursor(cursor);
        }
        catch (Exception ex)
        {
            _logger.Write("overlay.show-failed", new
            {
                exceptionType = ex.GetType().FullName,
                hresult = ex.HResult
            });
            Reset();
            return;
        }

        var foregroundAfter = NativeMethods.GetForegroundWindow();

        _logger.Write("overlay.shown", new
        {
            mode = _mode.ToString(),
            elapsedMs = elapsed,
            travelPixels = distance,
            preflightState = _candidate.PreflightQualification?.State.ToString(),
            preflightReason = _candidate.PreflightQualification?.Reason.ToString(),
            foregroundBefore = $"0x{foregroundBefore:X}",
            foregroundAfter = $"0x{foregroundAfter:X}",
            foregroundChanged = foregroundBefore != foregroundAfter
        });
    }

    public void OnDropCommitted(ProbeDropCommittedEventArgs args)
    {
        _logger.Write("probe.action-committed", new
        {
            mode = _mode.ToString(),
            actionId = args.Action.Id.Value,
            extension = SafeExtensionForDiagnostics(args.Path),
            preflightMatched = args.PreflightMatched,
            note = "Probe does not process the file. Source must remain unchanged."
        });

        Reset();
    }

    private void StartCandidate(DragSignal signal, string? processName)
    {
        if (!string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Write("candidate.suppressed.non-explorer", new { processName });
            Reset();
            return;
        }

        if (!NativeMethods.GetCursorPos(out var startPoint))
        {
            return;
        }

        PayloadQualificationResult? preflight = null;
        if (_mode == ProbeMode.G2ExplorerSelection)
        {
            var selection = _selectionReader.Read(signal.SourceHwnd);
            _logger.Write("g2.explorer-selection", new
            {
                selection.Success,
                rootHwnd = $"0x{selection.RootHwnd:X}",
                fileCount = selection.Paths.Count,
                extensions = selection.Paths.Select(SafeExtensionForDiagnostics).ToArray(),
                selection.Error
            });

            if (!selection.Success)
            {
                _logger.Write("candidate.suppressed.preflight-unavailable", new { selection.Error });
                Reset();
                return;
            }

            var snapshot = _snapshotFactory.Create(
                selection.Paths,
                PayloadEvidenceSource.ExplorerSelectionSnapshot,
                "ShellFolderView.SelectedItems preflight");
            preflight = MvpPayloadQualifier.Qualify(snapshot);

            _logger.Write("g2.preflight-qualified", new
            {
                state = preflight.State.ToString(),
                reason = preflight.Reason.ToString(),
                preflight.Detail,
                mayShowOverlay = preflight.MayShowOverlay
            });

            if (!preflight.MayShowOverlay)
            {
                _logger.Write("candidate.suppressed.preflight-rejected", new
                {
                    state = preflight.State.ToString(),
                    reason = preflight.Reason.ToString()
                });
                Reset();
                return;
            }
        }
        else
        {
            _logger.Write("p0.signal-only-warning", new
            {
                warning = "Interop-only mode may show overlay before payload support is known. Never copy this behavior into production."
            });
        }

        _candidate = new Candidate(signal, startPoint, Environment.TickCount64, preflight);
    }

    private void Reset()
    {
        _candidate = null;
        _dropTarget.SetExpectation(null);
        _window.Hide();
    }

    private static uint ResolveProcessId(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return 0;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return processId;
    }

    private static string? TryGetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeExtensionForDiagnostics(string path)
    {
        try
        {
            return Path.GetExtension(path);
        }
        catch
        {
            return null;
        }
    }

    private sealed record Candidate(
        DragSignal Signal,
        POINT StartPoint,
        long StartTick,
        PayloadQualificationResult? PreflightQualification);
}

using SmartDrag.Infrastructure;
using SmartDrag.Windows.Diagnostics;
using SmartDrag.Windows.Drag;
using SmartDrag.Windows.Interop;
using SmartDrag.Windows.Ole;
using SmartDrag.Windows.Shell;

namespace SmartDrag.Windows.Probe;

internal static class Program
{
    private const uint WM_QUIT = 0x0012;

    [STAThread]
    private static int Main(string[] args)
    {
        var mode = ParseMode(args);
        var smoke = args.Any(argument => string.Equals(argument, "--smoke", StringComparison.OrdinalIgnoreCase));
        NativeMethods.SetProcessDpiAwarenessContext(NativeConstants.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        var logPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "logs",
            $"smartdrag-probe-{mode}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");

        using var logger = new TimelineLogger(logPath);
        using var ole = new OleThreadScope();
        using var window = new ProbeWindow(logger);
        var snapshotFactory = new PhysicalFilePayloadSnapshotFactory();
        var selectionReader = new ExplorerSelectionSnapshotReader();
        var dropTarget = new ProbeDropTarget(window, logger, snapshotFactory);
        using var registration = new OleDropTargetRegistration(window.Handle, dropTarget);
        using var dragSignals = new WinEventDragSignalSource();
        var coordinator = new ProbeDragCoordinator(
            mode,
            window,
            dropTarget,
            logger,
            selectionReader,
            snapshotFactory);

        window.Tick += (_, _) =>
        {
            while (dragSignals.TryRead(out var signal))
            {
                coordinator.OnSignal(signal);
            }

            coordinator.Tick();
        };
        dropTarget.DropCommitted += (_, eventArgs) => coordinator.OnDropCommitted(eventArgs);

        dragSignals.Start();

        logger.Write("probe.started", new
        {
            mode = mode.ToString(),
            processId = Environment.ProcessId,
            threadId = NativeMethods.GetCurrentThreadId(),
            logPath,
            warning = mode == ProbeMode.P0SignalOnly
                ? "Interop-only proof: overlay eligibility is intentionally relaxed. Never use this behavior in production."
                : "G2 experiment: overlay requires Explorer selected-path preflight and Drop revalidation."
        });

        Console.WriteLine("SmartDrag Windows Probe");
        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine(mode == ProbeMode.P0SignalOnly
            ? "P0 signal-only mode proves Explorer coexistence; it does NOT satisfy production payload qualification."
            : "G2 mode suppresses the overlay unless Explorer selected paths qualify as exactly one PNG/JPEG file.");
        Console.WriteLine("1) Focus Explorer.");
        Console.WriteLine("2) Drag a file long enough for the qualification threshold.");
        Console.WriteLine("3) Ignore the overlay and verify normal Explorer drop still works.");
        Console.WriteLine("4) Repeat and intentionally drag onto an action tile, then release.");
        Console.WriteLine("5) The probe logs the action but does NOT modify/process the file.");
        Console.WriteLine($"Log: {logPath}");
        Console.WriteLine(smoke ? "Smoke mode: the probe will stop automatically after startup." : "Press Ctrl+C to stop.");

        var mainThreadId = NativeMethods.GetCurrentThreadId();
        using var smokeTimer = smoke
            ? new Timer(_ => NativeMethods.PostThreadMessageW(mainThreadId, WM_QUIT, 0, nint.Zero), null, 750, Timeout.Infinite)
            : null;
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            NativeMethods.PostThreadMessageW(mainThreadId, WM_QUIT, 0, nint.Zero);
        };

        while (true)
        {
            var result = NativeMethods.GetMessageW(out var message, nint.Zero, 0, 0);
            if (result == 0)
            {
                break;
            }

            if (result < 0)
            {
                logger.Write("probe.get-message-failed");
                return 1;
            }

            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessageW(ref message);
        }

        logger.Write("probe.stopped", new
        {
            droppedLogEntries = logger.DroppedEntries,
            droppedWinEventSignals = dragSignals.DroppedSignals
        });
        return 0;
    }

    private static ProbeMode ParseMode(string[] args)
    {
        foreach (var argument in args)
        {
            if (string.Equals(argument, "--mode=g2", StringComparison.OrdinalIgnoreCase))
            {
                return ProbeMode.G2ExplorerSelection;
            }

            if (string.Equals(argument, "--mode=p0", StringComparison.OrdinalIgnoreCase))
            {
                return ProbeMode.P0SignalOnly;
            }
        }

        return ProbeMode.P0SignalOnly;
    }
}

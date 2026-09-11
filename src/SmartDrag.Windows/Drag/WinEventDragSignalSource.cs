using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Drag;

/// <summary>
/// Observes Windows accessibility drag lifecycle events.
/// Start/Dispose must be called on a thread that pumps Win32 messages.
/// The callback intentionally performs only immutable signal capture and queueing.
/// No process lookup, COM traversal, file-system access, logging, or UI work is allowed in the callback.
/// </summary>
public sealed class WinEventDragSignalSource : IDisposable
{
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly ConcurrentQueue<DragSignal> _signals = new();
    private nint _hook;
    private long _droppedSignals;
    private bool _disposed;

    public WinEventDragSignalSource()
    {
        _callback = OnWinEvent;
    }

    public long DroppedSignals => Interlocked.Read(ref _droppedSignals);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hook != nint.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_OBJECT_DRAGSTART,
            NativeConstants.EVENT_OBJECT_DRAGCOMPLETE,
            nint.Zero,
            _callback,
            0,
            0,
            NativeConstants.WINEVENT_OUTOFCONTEXT | NativeConstants.WINEVENT_SKIPOWNPROCESS);

        if (_hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWinEventHook failed.");
        }
    }

    private void OnWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint eventTimeMs)
    {
        try
        {
            var kind = eventType switch
            {
                NativeConstants.EVENT_OBJECT_DRAGSTART => DragSignalKind.Started,
                NativeConstants.EVENT_OBJECT_DRAGCANCEL => DragSignalKind.Cancelled,
                NativeConstants.EVENT_OBJECT_DRAGCOMPLETE => DragSignalKind.Completed,
                _ => (DragSignalKind?)null
            };

            if (kind is null)
            {
                return;
            }

            _signals.Enqueue(new DragSignal(
                kind.Value,
                hwnd,
                idObject,
                idChild,
                idEventThread,
                eventTimeMs,
                DateTimeOffset.UtcNow));
        }
        catch (Exception)
        {
            // Never propagate managed exceptions through the WinEvent callback boundary.
            // The signal is diagnostic evidence; dropping one is safer than destabilizing the host thread.
            Interlocked.Increment(ref _droppedSignals);
        }
    }

    public bool TryRead(out DragSignal signal)
    {
        if (_signals.TryDequeue(out var queued) && queued is not null)
        {
            signal = queued;
            return true;
        }

        signal = null!;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hook != nint.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = nint.Zero;
        }
    }
}

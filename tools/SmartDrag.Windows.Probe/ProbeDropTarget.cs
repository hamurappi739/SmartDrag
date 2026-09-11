using System.Runtime.InteropServices.ComTypes;
using SmartDrag.Core.Payload;
using SmartDrag.Infrastructure;
using SmartDrag.Windows.Diagnostics;
using SmartDrag.Windows.Interop;
using SmartDrag.Windows.Ole;

namespace SmartDrag.Windows.Probe;

/// <summary>
/// Diagnostic OLE drop target. This class is a process boundary with Explorer/other drag sources:
/// no managed exception is allowed to escape an IDropTarget callback.
/// </summary>
internal sealed class ProbeDropTarget : IDropTargetNative
{
    private const int S_OK = 0;

    private readonly ProbeWindow _window;
    private readonly TimelineLogger _logger;
    private readonly PhysicalFilePayloadSnapshotFactory _snapshotFactory;
    private ProbePayloadExpectation? _expectation;
    private bool _formatAvailable;
    private ProbeAction? _hoveredAction;

    public ProbeDropTarget(
        ProbeWindow window,
        TimelineLogger logger,
        PhysicalFilePayloadSnapshotFactory snapshotFactory)
    {
        _window = window;
        _logger = logger;
        _snapshotFactory = snapshotFactory;
    }

    public event EventHandler<ProbeDropCommittedEventArgs>? DropCommitted;

    public void SetExpectation(ProbePayloadExpectation? expectation) => _expectation = expectation;

    public int DragEnter(IDataObject dataObject, uint keyState, POINTL point, ref uint effect)
    {
        try
        {
            var sourceAllowedEffects = effect;

            // Per Shell guidance, do not render/extract the full Shell data object merely for hover feedback.
            // QueryGetData is enough here. Actual CF_HDROP extraction happens on Drop.
            _formatAvailable = FileDropDataReader.CanRead(dataObject);
            _hoveredAction = _window.HitTestScreenPoint(point);
            _window.SetHoveredAction(_hoveredAction);

            var preflightAllows = PreflightAllowsDrop();
            effect = DropEffectPolicy.SelectNonDestructiveCopy(
                sourceAllowedEffects,
                _formatAvailable && preflightAllows,
                _hoveredAction is not null);

            _logger.Write("ole.drag-enter", new
            {
                mode = _expectation?.Mode.ToString(),
                sourceAllowedEffects,
                returnedEffect = effect,
                cfHdropAvailable = _formatAvailable,
                preflightAllows,
                hoveredAction = _hoveredAction?.Id.Value
            });
        }
        catch (Exception ex)
        {
            FailOleCallback("ole.drag-enter.exception", ex, ref effect);
        }

        return S_OK;
    }

    public int DragOver(uint keyState, POINTL point, ref uint effect)
    {
        try
        {
            var sourceAllowedEffects = effect;
            _hoveredAction = _window.HitTestScreenPoint(point);
            _window.SetHoveredAction(_hoveredAction);

            effect = DropEffectPolicy.SelectNonDestructiveCopy(
                sourceAllowedEffects,
                _formatAvailable && PreflightAllowsDrop(),
                _hoveredAction is not null);
        }
        catch (Exception ex)
        {
            FailOleCallback("ole.drag-over.exception", ex, ref effect);
        }

        return S_OK;
    }

    public int DragLeave()
    {
        try
        {
            _logger.Write("ole.drag-leave");
            ResetDragTargetState();
        }
        catch (Exception ex)
        {
            // DragLeave has no pdwEffect output. Keep the COM boundary no-throw and best-effort reset local state.
            SafeResetDragTargetState();
            WriteBoundaryFailure("ole.drag-leave.exception", ex);
        }

        return S_OK;
    }

    public int Drop(IDataObject dataObject, uint keyState, POINTL point, ref uint effect)
    {
        try
        {
            var sourceAllowedEffects = effect;
            var action = _window.HitTestScreenPoint(point);

            var dataRead = FileDropDataReader.TryReadPaths(dataObject, out var paths);
            var snapshot = _snapshotFactory.Create(
                paths,
                PayloadEvidenceSource.OleDataObject,
                "CF_HDROP extracted during IDropTarget.Drop");
            var authoritative = dataRead
                ? MvpPayloadQualifier.Qualify(snapshot)
                : new PayloadQualificationResult
                {
                    State = PayloadQualificationState.Rejected,
                    Reason = PayloadRejectionReason.NotFiles,
                    Detail = "CF_HDROP could not be read on Drop."
                };

            var preflightMatched = _expectation?.Mode switch
            {
                ProbeMode.G2ExplorerSelection when _expectation.PreflightQualification is { } preflight =>
                    MvpPayloadQualifier.PathsMatchPreflight(preflight, authoritative),
                ProbeMode.P0SignalOnly => true,
                _ => false
            };

            var payloadValid = authoritative.IsAuthoritative && preflightMatched;
            effect = DropEffectPolicy.SelectNonDestructiveCopy(sourceAllowedEffects, payloadValid, action is not null);

            _logger.Write("ole.drop", new
            {
                mode = _expectation?.Mode.ToString(),
                sourceAllowedEffects,
                returnedEffect = effect,
                dataRead,
                authoritativeState = authoritative.State.ToString(),
                authoritativeReason = authoritative.Reason.ToString(),
                fileCount = paths.Count,
                extensions = paths.Select(SafeExtensionForDiagnostics).ToArray(),
                preflightMatched,
                actionId = action?.Id.Value
            });

            if (effect == NativeConstants.DROPEFFECT_COPY && action is not null && paths.Count == 1)
            {
                // Any subscriber is still managed application code. It is deliberately inside this try/catch
                // so a future probe observer cannot throw back into the source application's OLE drag loop.
                DropCommitted?.Invoke(this, new ProbeDropCommittedEventArgs(action, paths[0], preflightMatched));
            }

            ResetDragTargetState();
        }
        catch (Exception ex)
        {
            FailOleCallback("ole.drop.exception", ex, ref effect);
        }

        return S_OK;
    }

    private bool PreflightAllowsDrop() => _expectation?.Mode switch
    {
        ProbeMode.G2ExplorerSelection => _expectation.PreflightQualification?.MayShowOverlay == true,
        ProbeMode.P0SignalOnly => true,
        _ => false
    };

    private void FailOleCallback(string eventName, Exception exception, ref uint effect)
    {
        // Safe default: do not claim the drop, never fall back to MOVE, and do not propagate managed faults.
        effect = NativeConstants.DROPEFFECT_NONE;
        SafeResetDragTargetState();
        WriteBoundaryFailure(eventName, exception);
    }

    private void WriteBoundaryFailure(string eventName, Exception exception)
    {
        // Do not log exception text here: COM/Shell failures can contain user-controlled paths.
        _logger.Write(eventName, new
        {
            exceptionType = exception.GetType().FullName,
            hresult = exception.HResult
        });
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

    private void SafeResetDragTargetState()
    {
        try
        {
            ResetDragTargetState();
        }
        catch
        {
            _formatAvailable = false;
            _hoveredAction = null;
        }
    }

    private void ResetDragTargetState()
    {
        _formatAvailable = false;
        _hoveredAction = null;
        _window.SetHoveredAction(null);
    }
}

internal sealed record ProbeDropCommittedEventArgs(ProbeAction Action, string Path, bool PreflightMatched);

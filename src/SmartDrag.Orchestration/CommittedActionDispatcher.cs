using SmartDrag.Core.Jobs;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Orchestration;

/// <summary>
/// Idempotent adapter from an accepted commit decision to JobQueue. The request id is the drag-session id,
/// so duplicate delivery of the same accepted Drop returns the same JobId and never enqueues twice.
/// A conflicting action for an already-dispatched request is treated as a programming/integration fault.
/// </summary>
public sealed class CommittedActionDispatcher
{
    private readonly IJobQueue _jobQueue;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, DispatchRecord> _dispatched = new();

    public CommittedActionDispatcher(IJobQueue jobQueue)
    {
        _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
    }

    public JobId Dispatch(ActionCommitDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.IsAccepted || decision.Request is null)
        {
            throw new InvalidOperationException("Only an accepted commit decision may be dispatched.");
        }

        var request = decision.Request;
        lock (_gate)
        {
            if (_dispatched.TryGetValue(request.RequestId, out var existing))
            {
                if (existing.ActionId != request.ActionId)
                {
                    throw new InvalidOperationException(
                        $"Request '{request.RequestId}' was already dispatched for action '{existing.ActionId}', not '{request.ActionId}'.");
                }

                return existing.JobId;
            }

            var jobId = _jobQueue.Enqueue(request);
            _dispatched.Add(request.RequestId, new DispatchRecord(request.ActionId, jobId));
            return jobId;
        }
    }

    private readonly record struct DispatchRecord(ActionId ActionId, JobId JobId);
}

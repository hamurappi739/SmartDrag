using SmartDrag.Core.Actions;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Jobs;

/// <summary>
/// Background execution queue. Enqueue is idempotent by ActionRequest.RequestId:
/// an equivalent repeated request returns the original JobId; reusing the same RequestId for different
/// action/input/output semantics is an integration error and must be rejected.
/// </summary>
public interface IJobQueue : IAsyncDisposable
{
    event EventHandler<JobChangedEventArgs>? JobChanged;

    JobId Enqueue(ActionRequest request);
    bool TryCancel(JobId jobId);
    bool TryGetSnapshot(JobId jobId, out JobSnapshot snapshot);
    IReadOnlyList<JobSnapshot> GetSnapshots();
}

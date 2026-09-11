using SmartDrag.Core.Actions;

namespace SmartDrag.Orchestration;

public enum ActionCommitStatus
{
    Accepted = 0,
    OverlaySessionInvalid,
    PayloadNotAuthoritative,
    PreflightMismatch,
    ActionWasNotOffered,
    ActionNoLongerAvailable,
    InvalidSettings,
    UnsafeOutputPolicy
}

/// <summary>
/// Capability object produced only by DragWorkflowOrchestrator. External UI/Win32 adapters cannot forge an
/// Accepted decision with an arbitrary ActionRequest because construction is internal to this assembly.
/// </summary>
public sealed class ActionCommitDecision
{
    internal ActionCommitDecision(ActionCommitStatus status, string reason, ActionRequest? request = null)
    {
        Status = status;
        Reason = reason;
        Request = request;
    }

    public ActionCommitStatus Status { get; }
    public string Reason { get; }
    public ActionRequest? Request { get; }
    public bool IsAccepted => Status == ActionCommitStatus.Accepted && Request is not null;
}

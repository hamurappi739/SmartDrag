using Xunit;
using SmartDrag.Core.Drag;

namespace SmartDrag.Core.Tests;

public sealed class DragSessionReducerTests
{
    [Fact]
    public void Idle_ProbableDragDetected_BecomesCandidate()
    {
        var state = DragSessionReducer.Reduce(DragSessionState.Idle, DragSessionEvent.ProbableDragDetected);
        Assert.Equal(DragSessionState.Candidate, state);
    }

    [Fact]
    public void HappyPath_ReachesCompleted()
    {
        var state = DragSessionState.Candidate;
        state = DragSessionReducer.Reduce(state, DragSessionEvent.QualificationThresholdReached);
        state = DragSessionReducer.Reduce(state, DragSessionEvent.PayloadSupported);
        state = DragSessionReducer.Reduce(state, DragSessionEvent.OverlayShown);
        state = DragSessionReducer.Reduce(state, DragSessionEvent.ActionSelected);
        state = DragSessionReducer.Reduce(state, DragSessionEvent.ExecutionStarted);
        state = DragSessionReducer.Reduce(state, DragSessionEvent.ExecutionSucceeded);

        Assert.Equal(DragSessionState.Completed, state);
    }

    [Fact]
    public void UnsupportedPayload_IsSuppressed()
    {
        var state = DragSessionReducer.Reduce(
            DragSessionState.Candidate,
            DragSessionEvent.QualificationThresholdReached);

        state = DragSessionReducer.Reduce(state, DragSessionEvent.PayloadRejected);

        Assert.Equal(DragSessionState.Suppressed, state);
    }

    [Fact]
    public void CancellationRequest_UsesCancellingState()
    {
        var state = DragSessionReducer.Reduce(DragSessionState.Executing, DragSessionEvent.CancellationRequested);
        Assert.Equal(DragSessionState.Cancelling, state);

        state = DragSessionReducer.Reduce(state, DragSessionEvent.ExecutionCancelled);
        Assert.Equal(DragSessionState.Cancelled, state);
    }

    [Fact]
    public void InvalidTransition_Throws()
    {
        Assert.Throws<InvalidDragTransitionException>(() =>
            DragSessionReducer.Reduce(DragSessionState.Candidate, DragSessionEvent.ExecutionSucceeded));
    }
}

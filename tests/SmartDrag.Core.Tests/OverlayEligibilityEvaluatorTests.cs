using SmartDrag.Core.Drag;
using SmartDrag.Core.Payload;
using Xunit;

namespace SmartDrag.Core.Tests;

public sealed class OverlayEligibilityEvaluatorTests
{
    [Fact]
    public void UnknownPayload_NeverShowsOverlay()
    {
        var decision = OverlayEligibilityEvaluator.Evaluate(new PayloadQualificationResult
        {
            State = PayloadQualificationState.Unknown,
            Reason = PayloadRejectionReason.EvidenceUnavailable
        }, availableActionCount: 3);

        Assert.False(decision.ShouldShow);
        Assert.Equal(OverlayEligibilityReason.PayloadUnknown, decision.Reason);
    }

    [Fact]
    public void EligiblePayloadWithoutActions_NeverShowsEmptyOverlay()
    {
        var decision = OverlayEligibilityEvaluator.Evaluate(new PayloadQualificationResult
        {
            State = PayloadQualificationState.Eligible,
            Reason = PayloadRejectionReason.None
        }, availableActionCount: 0);

        Assert.False(decision.ShouldShow);
        Assert.Equal(OverlayEligibilityReason.NoAvailableActions, decision.Reason);
    }

    [Fact]
    public void EligiblePayloadWithActions_ShowsOverlay()
    {
        var decision = OverlayEligibilityEvaluator.Evaluate(new PayloadQualificationResult
        {
            State = PayloadQualificationState.Eligible,
            Reason = PayloadRejectionReason.None
        }, availableActionCount: 3);

        Assert.True(decision.ShouldShow);
        Assert.Equal(OverlayEligibilityReason.Eligible, decision.Reason);
    }
}

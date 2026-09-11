using SmartDrag.Core.Payload;

namespace SmartDrag.Core.Drag;

public enum OverlayEligibilityReason
{
    Eligible = 0,
    PayloadUnknown,
    PayloadRejected,
    NoAvailableActions
}

public sealed record OverlayEligibilityDecision
{
    public required bool ShouldShow { get; init; }
    public required OverlayEligibilityReason Reason { get; init; }
    public PayloadRejectionReason? PayloadReason { get; init; }
}

public static class OverlayEligibilityEvaluator
{
    public static OverlayEligibilityDecision Evaluate(
        PayloadQualificationResult qualification,
        int availableActionCount)
    {
        ArgumentNullException.ThrowIfNull(qualification);
        if (availableActionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableActionCount));
        }

        if (qualification.State == PayloadQualificationState.Unknown)
        {
            return new OverlayEligibilityDecision
            {
                ShouldShow = false,
                Reason = OverlayEligibilityReason.PayloadUnknown,
                PayloadReason = qualification.Reason
            };
        }

        if (qualification.State == PayloadQualificationState.Rejected)
        {
            return new OverlayEligibilityDecision
            {
                ShouldShow = false,
                Reason = OverlayEligibilityReason.PayloadRejected,
                PayloadReason = qualification.Reason
            };
        }

        if (availableActionCount == 0)
        {
            return new OverlayEligibilityDecision
            {
                ShouldShow = false,
                Reason = OverlayEligibilityReason.NoAvailableActions
            };
        }

        return new OverlayEligibilityDecision
        {
            ShouldShow = true,
            Reason = OverlayEligibilityReason.Eligible
        };
    }
}

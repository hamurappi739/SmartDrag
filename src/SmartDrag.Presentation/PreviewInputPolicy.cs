namespace SmartDrag.Presentation;

public enum PreviewInputDecision
{
    IgnoreBusy,
    FinishActiveDrag,
    DismissVisibleCompletion,
    RejectPayload,
    InspectSingleFile
}

/// <summary>
/// Centralizes the safe-preview transition rules before a new file enters qualification and inspection.
/// </summary>
public static class PreviewInputPolicy
{
    public static PreviewInputDecision Decide(
        bool inputBusy,
        bool activeDragSession,
        bool visibleCompletion,
        int fileCount)
    {
        if (inputBusy)
        {
            return PreviewInputDecision.IgnoreBusy;
        }

        if (activeDragSession)
        {
            return PreviewInputDecision.FinishActiveDrag;
        }

        if (visibleCompletion)
        {
            return PreviewInputDecision.DismissVisibleCompletion;
        }

        return fileCount == 1
            ? PreviewInputDecision.InspectSingleFile
            : PreviewInputDecision.RejectPayload;
    }
}

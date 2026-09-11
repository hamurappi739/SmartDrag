namespace SmartDrag.Presentation;

public static class PreviewDropPolicy
{
    public static bool CanAccept(
        bool hasFileDrop,
        bool inputBusy,
        bool pickerBusy,
        bool activeDrag,
        bool activeOperation) =>
        hasFileDrop
        && !inputBusy
        && !pickerBusy
        && !activeDrag
        && !activeOperation;
}

namespace SmartDrag.Presentation;

/// <summary>
/// Toolkit-neutral file-picker boundary. The WPF adapter owns the native dialog; Preview logic only receives a
/// selected path or a normal cancellation.
/// </summary>
public interface IPreviewFilePicker
{
    string? PickImage(PreviewFilePickerOptions options);
}

public sealed record PreviewFilePickerOptions
{
    public required string Title { get; init; }
    public required string Filter { get; init; }
    public string? InitialDirectory { get; init; }
}

public static class PreviewFilePickerPolicy
{
    public static bool CanPickImage(bool inputBusy, bool activeDrag, bool activeOperation) =>
        !inputBusy && !activeDrag && !activeOperation;
}

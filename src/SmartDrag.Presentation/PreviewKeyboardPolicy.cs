namespace SmartDrag.Presentation;

[Flags]
public enum PreviewKeyboardModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4
}

public enum PreviewKeyboardKey
{
    Other,
    Escape,
    Delete,
    V,
    O,
    L,
    T,
    H
}

public enum PreviewKeyboardIntent
{
    None,
    CancelOperation,
    DismissCompletion,
    CancelDrag,
    ResetPreview,
    PasteFiles,
    ChooseImage,
    ToggleLanguage,
    ToggleTheme,
    ClearHistory,
    DeleteOutput
}

public readonly record struct PreviewKeyboardState(
    bool HasActiveOperation,
    bool HasCompletion,
    bool HasDragSession,
    bool CanDeleteOutput);

/// <summary>
/// Toolkit-neutral keyboard intent policy for the safe Preview host. It only chooses an intent; the WPF host remains
/// responsible for invoking the existing presentation/runtime authorities.
/// </summary>
public static class PreviewKeyboardPolicy
{
    public static PreviewKeyboardIntent Resolve(
        PreviewKeyboardKey key,
        PreviewKeyboardModifiers modifiers,
        PreviewKeyboardState state)
    {
        if (key == PreviewKeyboardKey.V && modifiers == PreviewKeyboardModifiers.Control)
        {
            return PreviewKeyboardIntent.PasteFiles;
        }

        if (key == PreviewKeyboardKey.O && modifiers == PreviewKeyboardModifiers.Control)
        {
            return PreviewKeyboardIntent.ChooseImage;
        }

        if (key == PreviewKeyboardKey.L && modifiers == PreviewKeyboardModifiers.Control)
        {
            return PreviewKeyboardIntent.ToggleLanguage;
        }

        if (key == PreviewKeyboardKey.T
            && modifiers == (PreviewKeyboardModifiers.Control | PreviewKeyboardModifiers.Shift))
        {
            return PreviewKeyboardIntent.ToggleTheme;
        }

        if (key == PreviewKeyboardKey.H
            && modifiers == (PreviewKeyboardModifiers.Control | PreviewKeyboardModifiers.Shift))
        {
            return PreviewKeyboardIntent.ClearHistory;
        }

        if (modifiers != PreviewKeyboardModifiers.None)
        {
            return PreviewKeyboardIntent.None;
        }

        return key switch
        {
            PreviewKeyboardKey.Escape when state.HasActiveOperation => PreviewKeyboardIntent.CancelOperation,
            PreviewKeyboardKey.Escape when state.HasCompletion => PreviewKeyboardIntent.DismissCompletion,
            PreviewKeyboardKey.Escape when state.HasDragSession => PreviewKeyboardIntent.CancelDrag,
            PreviewKeyboardKey.Escape => PreviewKeyboardIntent.ResetPreview,
            PreviewKeyboardKey.Delete when state.HasCompletion && state.CanDeleteOutput => PreviewKeyboardIntent.DeleteOutput,
            _ => PreviewKeyboardIntent.None
        };
    }
}

using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Ole;

public static class DropEffectPolicy
{
    public static uint SelectNonDestructiveCopy(uint sourceAllowedEffects, bool payloadValid, bool actionValid)
    {
        if (!payloadValid || !actionValid)
        {
            return NativeConstants.DROPEFFECT_NONE;
        }

        return (sourceAllowedEffects & NativeConstants.DROPEFFECT_COPY) != 0
            ? NativeConstants.DROPEFFECT_COPY
            : NativeConstants.DROPEFFECT_NONE;
    }
}

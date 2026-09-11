namespace SmartDrag.App.Composition;

/// <summary>
/// Deliberate production kill-switch while empirical gates are unresolved. This is not a feature flag and must
/// not be toggled merely to make the app launch. Enabling native activation requires retained G0/G1/G2/G3
/// evidence and an ADR/checkpoint update.
/// </summary>
public static class ProductionActivationPolicy
{
    public const bool NativeActivationEnabled = false;

    public static void ThrowIfNativeActivationBlocked()
    {
        if (!NativeActivationEnabled)
        {
            throw new InvalidOperationException(
                "Production native activation is blocked until G0 build, G1 Explorer coexistence, " +
                "G2 payload qualification, and G3 codec gates are explicitly accepted.");
        }
    }
}

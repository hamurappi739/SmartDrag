using SmartDrag.Core.Settings;

namespace SmartDrag.Core.Output;

/// <summary>
/// The production MVP has exactly one output-safety policy. UI/Win32 adapters are not allowed to construct a
/// destructive alternative from user input. Future output-location features must extend this factory under ADR.
/// </summary>
public static class MvpOutputPolicyFactory
{
    public static OutputPolicy Create(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var validation = MvpSettingsPolicy.Validate(settings);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Invalid MVP settings: {validation.Summary}", nameof(settings));
        }

        return OutputPolicy.SafeMvpDefault;
    }
}

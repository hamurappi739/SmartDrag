namespace SmartDrag.Core.Settings;

/// <summary>
/// Central validation for settings that are allowed to influence the MVP runtime. These ranges are engineering
/// guardrails, not empirically tuned UX claims. Production code must reject invalid settings rather than silently
/// accepting values that can destabilize overlay timing/placement or weaken source preservation.
/// </summary>
public static class MvpSettingsPolicy
{
    public const int MinActivationDelayMs = 80;
    public const int MaxActivationDelayMs = 1200;
    public const double MinTravelDip = 4;
    public const double MaxTravelDip = 96;
    public const double MinCursorOffsetDip = 12;
    public const double MaxCursorOffsetDip = 160;

    public static MvpSettingsValidationResult Validate(UserSettings? settings)
    {
        if (settings is null)
        {
            return MvpSettingsValidationResult.Invalid("Settings are missing.");
        }

        var errors = new List<string>();
        if (settings.Drag.ActivationDelayMs is < MinActivationDelayMs or > MaxActivationDelayMs)
        {
            errors.Add($"ActivationDelayMs must be between {MinActivationDelayMs} and {MaxActivationDelayMs}.");
        }

        if (!double.IsFinite(settings.Drag.MinimumTravelDip) ||
            settings.Drag.MinimumTravelDip < MinTravelDip ||
            settings.Drag.MinimumTravelDip > MaxTravelDip)
        {
            errors.Add($"MinimumTravelDip must be finite and between {MinTravelDip} and {MaxTravelDip}.");
        }

        if (!double.IsFinite(settings.Overlay.CursorOffsetDip) ||
            settings.Overlay.CursorOffsetDip < MinCursorOffsetDip ||
            settings.Overlay.CursorOffsetDip > MaxCursorOffsetDip)
        {
            errors.Add($"CursorOffsetDip must be finite and between {MinCursorOffsetDip} and {MaxCursorOffsetDip}.");
        }

        if (!settings.Output.PreserveSource)
        {
            errors.Add("PreserveSource must remain enabled in the MVP.");
        }

        return errors.Count == 0
            ? MvpSettingsValidationResult.Valid()
            : MvpSettingsValidationResult.Invalid(errors);
    }
}

public sealed record MvpSettingsValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }

    public string Summary => IsValid ? "Valid" : string.Join(" ", Errors);

    public static MvpSettingsValidationResult Valid() => new()
    {
        IsValid = true,
        Errors = Array.Empty<string>()
    };

    public static MvpSettingsValidationResult Invalid(string error) => Invalid(new[] { error });

    public static MvpSettingsValidationResult Invalid(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray()
    };
}

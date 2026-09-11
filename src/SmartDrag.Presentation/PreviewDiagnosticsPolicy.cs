using System.Text.Json;

namespace SmartDrag.Presentation;

public sealed record PreviewDiagnosticsSummary
{
    public bool Passed { get; init; }
    public int PassedChecks { get; init; }
    public int TotalChecks { get; init; }
    public long DurationMs { get; init; }
}

/// <summary>
/// Reads only the bounded, aggregate self-check result needed by the Preview UI. Check details and technical paths
/// never cross this presentation boundary.
/// </summary>
public static class PreviewDiagnosticsPolicy
{
    private const long MaximumReportBytes = 64 * 1024;

    public static bool CanRunSelfCheck(
        bool inputBusy,
        bool pickerBusy,
        bool activeDrag,
        bool activeOperation,
        bool visibleCompletion,
        bool selfCheckRunning) =>
        !inputBusy
        && !pickerBusy
        && !activeDrag
        && !activeOperation
        && !visibleCompletion
        && !selfCheckRunning;

    public static bool TryReadSummary(string? reportPath, out PreviewDiagnosticsSummary summary)
    {
        summary = new PreviewDiagnosticsSummary();
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(reportPath);
            if (!info.Exists || info.Length <= 0 || info.Length > MaximumReportBytes)
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetProperty(root, "Passed", out var passedElement)
                || passedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !TryGetProperty(root, "Checks", out var checksElement)
                || checksElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var total = 0;
            var passed = 0;
            foreach (var check in checksElement.EnumerateArray())
            {
                if (check.ValueKind != JsonValueKind.Object
                    || !TryGetProperty(check, "Passed", out var checkPassed)
                    || checkPassed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return false;
                }

                total++;
                if (checkPassed.GetBoolean())
                {
                    passed++;
                }
            }

            var duration = 0L;
            if (TryGetProperty(root, "DurationMs", out var durationElement)
                && durationElement.ValueKind == JsonValueKind.Number)
            {
                duration = Math.Max(0, durationElement.GetInt64());
            }

            summary = new PreviewDiagnosticsSummary
            {
                Passed = passedElement.GetBoolean() && passed == total && total > 0,
                PassedChecks = passed,
                TotalChecks = total,
                DurationMs = duration
            };
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or OverflowException)
        {
            return false;
        }
    }

    public static string FormatSummary(PreviewDiagnosticsSummary summary, bool russian)
    {
        var checks = $"{summary.PassedChecks}/{summary.TotalChecks}";
        var duration = summary.DurationMs > 0
            ? russian
                ? $" · {summary.DurationMs} мс"
                : $" · {summary.DurationMs} ms"
            : string.Empty;
        return summary.Passed
            ? russian
                ? $"Самопроверка пройдена ({checks}){duration}"
                : $"Self-check passed ({checks}){duration}"
            : russian
                ? $"Самопроверка: есть проблемы ({checks}){duration}"
                : $"Self-check found issues ({checks}){duration}";
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

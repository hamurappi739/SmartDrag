using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;

namespace SmartDrag.Core.Actions;

public sealed record ActionResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<string> OutputPaths { get; init; } = Array.Empty<string>();
    public ActionMetrics? Metrics { get; init; }
    public IReadOnlyList<GeneratedArtifact> GeneratedArtifacts { get; init; } = Array.Empty<GeneratedArtifact>();
    public AppError? Error { get; init; }

    public static ActionResult Succeeded(params string[] outputPaths) => new()
    {
        Success = true,
        OutputPaths = outputPaths
    };

    public static ActionResult SucceededWithMetrics(ActionMetrics metrics, params string[] outputPaths) => new()
    {
        Success = true,
        OutputPaths = outputPaths,
        Metrics = metrics
    };

    public static ActionResult SucceededGeneratedWithMetrics(ActionMetrics metrics, params GeneratedArtifact[] artifacts) => new()
    {
        Success = true,
        OutputPaths = artifacts.Select(artifact => artifact.Path).ToArray(),
        GeneratedArtifacts = artifacts,
        Metrics = metrics
    };

    public static ActionResult Failed(AppError error) => new()
    {
        Success = false,
        Error = error
    };
}

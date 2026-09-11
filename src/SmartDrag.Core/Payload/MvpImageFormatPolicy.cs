namespace SmartDrag.Core.Payload;

/// <summary>
/// Pre-G3 format gate. PNG/JPEG are the only formats allowed to drive production overlay eligibility
/// before the image-codec spike proves a wider source-format matrix.
/// </summary>
public static class MvpImageFormatPolicy
{
    private static readonly HashSet<string> PreG3Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    public static bool IsPreG3EligibleExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && PreG3Extensions.Contains(Normalize(extension));

    public static string Normalize(string extension) =>
        extension.StartsWith('.') ? extension : "." + extension;

    public static IReadOnlyCollection<string> GetPreG3Extensions() => PreG3Extensions.ToArray();
}

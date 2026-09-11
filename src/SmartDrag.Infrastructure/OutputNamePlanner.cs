namespace SmartDrag.Infrastructure;

public static class OutputNamePlanner
{
    public static string BuildCandidate(string sourcePath, string suffix, string? newExtension = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(suffix);

        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = newExtension ?? Path.GetExtension(sourcePath);

        if (extension.Length > 0 && extension[0] != '.')
        {
            extension = "." + extension;
        }

        return Path.Combine(directory, $"{baseName}{suffix}{extension}");
    }

    public static string FindUnique(string preferredPath, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredPath);
        ArgumentNullException.ThrowIfNull(exists);

        if (!exists(preferredPath))
        {
            return preferredPath;
        }

        var directory = Path.GetDirectoryName(preferredPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(preferredPath);
        var extension = Path.GetExtension(preferredPath);

        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({index}){extension}");
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Unable to allocate a unique output filename.");
    }
}

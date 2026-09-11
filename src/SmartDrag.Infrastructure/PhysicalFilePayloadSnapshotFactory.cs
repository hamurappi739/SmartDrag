using SmartDrag.Core.Payload;

namespace SmartDrag.Infrastructure;

public sealed class PhysicalFilePayloadSnapshotFactory : IFilePayloadSnapshotFactory
{
    public FilePayloadSnapshot Create(
        IReadOnlyList<string> paths,
        PayloadEvidenceSource evidenceSource,
        string? evidenceDetail = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var files = paths.Select(CreateCandidate).ToArray();
        return new FilePayloadSnapshot
        {
            EvidenceSource = evidenceSource,
            Files = files,
            EvidenceDetail = evidenceDetail
        };
    }

    private static FilePayloadCandidate CreateCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new FilePayloadCandidate
            {
                FullPath = null,
                DisplayName = null,
                Extension = null,
                Exists = false,
                IsDirectory = false
            };
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            // The original string may itself be invalid for Path APIs. Keep rejected evidence minimal
            // instead of risking a second exception while constructing the failure snapshot.
            return new FilePayloadCandidate
            {
                FullPath = null,
                DisplayName = null,
                Extension = null,
                Exists = false,
                IsDirectory = false
            };
        }

        var isDirectory = Directory.Exists(fullPath);
        var exists = isDirectory || File.Exists(fullPath);
        long? size = null;
        if (exists && !isDirectory)
        {
            try
            {
                size = new FileInfo(fullPath).Length;
            }
            catch
            {
                // Size is diagnostic/UX metadata only and must not turn an otherwise usable file into a failure.
            }
        }

        return new FilePayloadCandidate
        {
            FullPath = fullPath,
            DisplayName = Path.GetFileName(fullPath),
            Extension = Path.GetExtension(fullPath),
            Exists = exists,
            IsDirectory = isDirectory,
            SizeBytes = size
        };
    }
}

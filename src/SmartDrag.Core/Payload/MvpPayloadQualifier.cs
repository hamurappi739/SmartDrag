namespace SmartDrag.Core.Payload;

public static class MvpPayloadQualifier
{
    public static PayloadQualificationResult Qualify(FilePayloadSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.EvidenceSource == PayloadEvidenceSource.Unknown)
        {
            return Unknown(PayloadRejectionReason.EvidenceUnavailable, "No usable payload evidence is available.");
        }

        if (snapshot.Files.Count == 0)
        {
            return Rejected(PayloadRejectionReason.NoFiles, "Payload evidence contains no files.");
        }

        if (snapshot.Files.Count != 1)
        {
            return Rejected(PayloadRejectionReason.MultipleFiles, $"MVP requires exactly one file; evidence contains {snapshot.Files.Count}.");
        }

        var candidate = snapshot.Files[0];
        if (string.IsNullOrWhiteSpace(candidate.FullPath))
        {
            // Accessibility-only names can help diagnostics, but they are not enough to show production UI.
            return Unknown(PayloadRejectionReason.PathUnavailable, "The candidate has no resolved full path.");
        }

        if (!TryNormalizeFullPath(candidate.FullPath, out var normalizedPath))
        {
            return Rejected(PayloadRejectionReason.PathUnavailable, "The candidate path could not be normalized safely.");
        }

        if (!candidate.Exists)
        {
            return Rejected(PayloadRejectionReason.SourceMissing, "The candidate file does not exist.");
        }

        if (candidate.IsDirectory)
        {
            return Rejected(PayloadRejectionReason.DirectoryNotSupported, "Directories are outside the file-only image MVP.");
        }

        // The path is authoritative for preflight classification. Explorer display metadata can
        // omit hidden extensions or contain stale/mismatched values, so never trust candidate.Extension.
        string extension;
        try
        {
            extension = Path.GetExtension(normalizedPath);
        }
        catch
        {
            return Rejected(PayloadRejectionReason.PathUnavailable, "The candidate extension could not be determined safely.");
        }

        if (!MvpImageFormatPolicy.IsPreG3EligibleExtension(extension))
        {
            return Rejected(PayloadRejectionReason.UnsupportedExtension, $"Extension '{extension}' is not in the pre-G3 image allowlist.");
        }

        var file = new DraggedFile
        {
            FullPath = normalizedPath,
            Extension = MvpImageFormatPolicy.Normalize(extension),
            SizeBytes = candidate.SizeBytes,
            Category = FileCategory.Image
        };

        var payload = new DragPayloadInfo
        {
            Files = new[] { file },
            Kind = PayloadKind.Files,
            IsSupported = true
        };

        var state = snapshot.EvidenceSource is PayloadEvidenceSource.OleDataObject or PayloadEvidenceSource.LocalFilePicker
            ? PayloadQualificationState.Supported
            : snapshot.EvidenceSource == PayloadEvidenceSource.ExplorerSelectionSnapshot
                ? PayloadQualificationState.Eligible
                : PayloadQualificationState.Unknown;

        return new PayloadQualificationResult
        {
            State = state,
            Reason = state == PayloadQualificationState.Unknown
                ? PayloadRejectionReason.EvidenceUnavailable
                : PayloadRejectionReason.None,
            Payload = state == PayloadQualificationState.Unknown ? null : payload,
            Detail = snapshot.EvidenceDetail
        };
    }

    public static bool PathsMatchPreflight(PayloadQualificationResult preflight, PayloadQualificationResult authoritative)
    {
        if (!preflight.MayShowOverlay || !authoritative.IsAuthoritative ||
            preflight.Payload?.Files.Count != 1 || authoritative.Payload?.Files.Count != 1)
        {
            return false;
        }

        if (!TryNormalizeFullPath(preflight.Payload.Files[0].FullPath, out var preflightPath) ||
            !TryNormalizeFullPath(authoritative.Payload.Files[0].FullPath, out var authoritativePath))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(preflightPath, authoritativePath, comparison);
    }

    private static bool TryNormalizeFullPath(string path, out string normalizedPath)
    {
        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            normalizedPath = string.Empty;
            return false;
        }
    }

    private static PayloadQualificationResult Unknown(PayloadRejectionReason reason, string detail) => new()
    {
        State = PayloadQualificationState.Unknown,
        Reason = reason,
        Detail = detail
    };

    private static PayloadQualificationResult Rejected(PayloadRejectionReason reason, string detail) => new()
    {
        State = PayloadQualificationState.Rejected,
        Reason = reason,
        Detail = detail
    };
}

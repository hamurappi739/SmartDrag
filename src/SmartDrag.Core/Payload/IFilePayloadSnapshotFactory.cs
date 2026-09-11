namespace SmartDrag.Core.Payload;

public interface IFilePayloadSnapshotFactory
{
    FilePayloadSnapshot Create(IReadOnlyList<string> paths, PayloadEvidenceSource evidenceSource, string? evidenceDetail = null);
}

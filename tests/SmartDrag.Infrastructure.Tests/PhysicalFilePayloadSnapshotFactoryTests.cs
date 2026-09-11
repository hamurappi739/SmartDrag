using SmartDrag.Core.Payload;
using SmartDrag.Infrastructure;
using Xunit;

namespace SmartDrag.Infrastructure.Tests;

public sealed class PhysicalFilePayloadSnapshotFactoryTests
{
    [Fact]
    public void ExistingFile_ProducesPathAndSizeSnapshot()
    {
        var directory = Directory.CreateTempSubdirectory("smartdrag-payload-");
        try
        {
            var path = Path.Combine(directory.FullName, "image.png");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            var factory = new PhysicalFilePayloadSnapshotFactory();

            var snapshot = factory.Create(
                new[] { path },
                PayloadEvidenceSource.ExplorerSelectionSnapshot);

            var file = Assert.Single(snapshot.Files);
            Assert.True(file.Exists);
            Assert.False(file.IsDirectory);
            Assert.Equal(4, file.SizeBytes);
            Assert.Equal(".png", file.Extension);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Directory_IsRepresentedButNotPretendedToBeAFile()
    {
        var directory = Directory.CreateTempSubdirectory("smartdrag-payload-dir-");
        try
        {
            var factory = new PhysicalFilePayloadSnapshotFactory();
            var snapshot = factory.Create(
                new[] { directory.FullName },
                PayloadEvidenceSource.ExplorerSelectionSnapshot);

            var file = Assert.Single(snapshot.Files);
            Assert.True(file.Exists);
            Assert.True(file.IsDirectory);
            Assert.Null(file.SizeBytes);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
    [Fact]
    public void InvalidPath_IsCapturedAsUnresolvedEvidenceWithoutThrowing()
    {
        var factory = new PhysicalFilePayloadSnapshotFactory();

        var snapshot = factory.Create(
            new[] { "bad\0path.png" },
            PayloadEvidenceSource.ExplorerSelectionSnapshot);

        var file = Assert.Single(snapshot.Files);
        Assert.Null(file.FullPath);
        Assert.Null(file.DisplayName);
        Assert.Null(file.Extension);
        Assert.False(file.Exists);
    }

}

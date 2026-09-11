using SmartDrag.Core.Artifacts;
using SmartDrag.Windows.Artifacts;
using Xunit;

namespace SmartDrag.Windows.Tests.Artifacts;

public sealed class WindowsGeneratedArtifactIdentityServiceTests
{
    [Fact]
    public async Task Capture_ProducesVersionedWindowsFileIdentity()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("generated.png", "data");
        var sut = new WindowsGeneratedArtifactIdentityService();

        var result = await sut.CaptureAsync(path, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(WindowsGeneratedArtifactIdentityService.IdentityScheme, result.Identity?.Scheme);
        Assert.Equal(16, result.Identity?.VolumeId.Length);
        Assert.Equal(32, result.Identity?.ObjectId.Length);
    }

    [Fact]
    public async Task Capture_RejectsDirectoryIdentity()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(target);
        var sut = new WindowsGeneratedArtifactIdentityService();

        var result = await sut.CaptureAsync(target, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(SmartDrag.Core.Errors.ErrorCode.ArtifactIdentityUnavailable, result.Error?.Code);
        Assert.Null(result.Identity);
    }

    [Fact]
    public async Task DeleteIfIdentityMatches_DeletesTheOpenedVerifiedObject()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("generated.png", "data");
        var sut = new WindowsGeneratedArtifactIdentityService();
        var captured = await sut.CaptureAsync(path, CancellationToken.None);
        var artifact = new GeneratedArtifact { Path = path, Identity = captured.Identity };

        var result = await sut.DeleteIfIdentityMatchesAsync(artifact, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteIfIdentityMatches_RefusesReplacementAtSamePath()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("generated.png", "original");
        var other = directory.CreateFile("other.png", "other");
        var sut = new WindowsGeneratedArtifactIdentityService();
        var originalIdentity = (await sut.CaptureAsync(path, CancellationToken.None)).Identity;
        var otherIdentity = (await sut.CaptureAsync(other, CancellationToken.None)).Identity;
        Assert.NotEqual(originalIdentity, otherIdentity);

        File.Delete(path);
        File.Move(other, path);
        var artifact = new GeneratedArtifact { Path = path, Identity = originalIdentity };

        var result = await sut.DeleteIfIdentityMatchesAsync(artifact, CancellationToken.None);

        Assert.Equal(GeneratedArtifactDeletionStatus.IdentityMismatch, result.Status);
        Assert.True(File.Exists(path));
        Assert.Equal("other", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task DeleteIfIdentityMatches_RefusesDirectoryReplacement()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("generated.png", "original");
        var sut = new WindowsGeneratedArtifactIdentityService();
        var originalIdentity = (await sut.CaptureAsync(path, CancellationToken.None)).Identity;
        File.Delete(path);
        Directory.CreateDirectory(path);
        var artifact = new GeneratedArtifact { Path = path, Identity = originalIdentity };

        var result = await sut.DeleteIfIdentityMatchesAsync(artifact, CancellationToken.None);

        Assert.Contains(result.Status, new[]
        {
            GeneratedArtifactDeletionStatus.IdentityMismatch,
            GeneratedArtifactDeletionStatus.Failed
        });
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task DeleteIfIdentityMatches_RequiresStrongIdentity()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("generated.png", "data");
        var sut = new WindowsGeneratedArtifactIdentityService();

        var result = await sut.DeleteIfIdentityMatchesAsync(new GeneratedArtifact { Path = path }, CancellationToken.None);

        Assert.Equal(GeneratedArtifactDeletionStatus.IdentityUnavailable, result.Status);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task DeleteIfIdentityMatches_RejectsUnknownIdentityScheme()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.CreateFile("generated.png", "data");
        var sut = new WindowsGeneratedArtifactIdentityService();

        var result = await sut.DeleteIfIdentityMatchesAsync(new GeneratedArtifact
        {
            Path = path,
            Identity = new ArtifactIdentity
            {
                Scheme = "test-identity-v0",
                VolumeId = "volume",
                ObjectId = "object"
            }
        }, CancellationToken.None);

        Assert.Equal(GeneratedArtifactDeletionStatus.UnsupportedIdentityScheme, result.Status);
        Assert.True(File.Exists(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmartDrag.Windows.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateFile(string name, string contents)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}

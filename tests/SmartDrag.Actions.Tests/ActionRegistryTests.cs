using Xunit;
using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Settings;

namespace SmartDrag.Actions.Tests;

public sealed class ActionRegistryTests
{
    [Fact]
    public void MvpSingleImage_ReturnsThreeActions()
    {
        var registry = new ActionRegistry(BuiltInActionDefinitions.Mvp);
        var context = new ActionContext
        {
            Payload = new DragPayloadInfo
            {
                Kind = PayloadKind.Files,
                IsSupported = true,
                Files =
                [
                    new DraggedFile
                    {
                        FullPath = @"C:\temp\image.png",
                        Extension = ".png",
                        Category = FileCategory.Image
                    }
                ]
            },
            Capabilities = new AppCapabilities { ImagingAvailable = true, WebpEncodingAvailable = true },
            Settings = new UserSettings()
        };

        var actions = registry.GetAvailable(context);

        Assert.Equal(3, actions.Count);
    }

    [Fact]
    public void MvpSingleImage_HidesWebpUntilEncoderCapabilityIsAvailable()
    {
        var registry = new ActionRegistry(BuiltInActionDefinitions.Mvp);
        var context = new ActionContext
        {
            Payload = new DragPayloadInfo
            {
                Kind = PayloadKind.Files,
                IsSupported = true,
                Files =
                [
                    new DraggedFile
                    {
                        FullPath = @"C:\temp\image.png",
                        Extension = ".png",
                        Category = FileCategory.Image
                    }
                ]
            },
            Capabilities = new AppCapabilities { ImagingAvailable = true },
            Settings = new UserSettings()
        };

        var actions = registry.GetAvailable(context);

        Assert.Equal(2, actions.Count);
        Assert.DoesNotContain(actions, action => action.Id == BuiltInActionIds.ConvertImageToWebP);
    }

    [Fact]
    public void Constructor_RejectsActionWithoutDisplayName()
    {
        var definition = BuiltInActionDefinitions.Mvp[0] with { DisplayName = " " };

        var exception = Assert.Throws<ArgumentException>(() => new ActionRegistry(new[] { definition }));

        Assert.Contains("display name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsActionWithoutSupportedInputTypes()
    {
        var definition = BuiltInActionDefinitions.Mvp[0] with
        {
            SupportedInputTypes = new HashSet<FileCategory>()
        };

        var exception = Assert.Throws<ArgumentException>(() => new ActionRegistry(new[] { definition }));

        Assert.Contains("supported input type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsActionWithoutIconId()
    {
        var definition = BuiltInActionDefinitions.Mvp[0] with { IconId = "" };

        var exception = Assert.Throws<ArgumentException>(() => new ActionRegistry(new[] { definition }));

        Assert.Contains("icon id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

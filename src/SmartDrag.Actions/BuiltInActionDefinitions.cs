using SmartDrag.Core.Actions;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Actions;

public static class BuiltInActionDefinitions
{
    private static readonly IReadOnlySet<FileCategory> ImageInput = new HashSet<FileCategory>
    {
        FileCategory.Image
    };

    public static IReadOnlyList<ActionDefinition> Mvp { get; } =
    [
        new ActionDefinition
        {
            Id = BuiltInActionIds.CompressImage,
            DisplayName = "Compress",
            SupportedInputTypes = ImageInput,
            ExecutesLocally = true,
            OutputType = OutputKind.File,
            CanRunInPlace = false,
            CanBatch = false,
            IconId = "compress"
        },
        new ActionDefinition
        {
            Id = BuiltInActionIds.ConvertImageToWebP,
            DisplayName = "Convert to WebP",
            SupportedInputTypes = ImageInput,
            ExecutesLocally = true,
            OutputType = OutputKind.File,
            CanRunInPlace = false,
            CanBatch = false,
            IconId = "webp"
        },
        new ActionDefinition
        {
            Id = BuiltInActionIds.RemoveImageMetadata,
            DisplayName = "Remove Metadata",
            SupportedInputTypes = ImageInput,
            ExecutesLocally = true,
            OutputType = OutputKind.File,
            CanRunInPlace = false,
            CanBatch = false,
            IconId = "metadata-remove"
        }
    ];
}

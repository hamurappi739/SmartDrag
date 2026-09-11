using SmartDrag.Core.Actions;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Windows.Probe;

internal sealed record ProbeAction(ActionId Id, string Label)
{
    public static IReadOnlyList<ProbeAction> All { get; } =
    [
        new(BuiltInActionIds.CompressImage, "Compress"),
        new(BuiltInActionIds.ConvertImageToWebP, "Convert to WebP"),
        new(BuiltInActionIds.RemoveImageMetadata, "Remove Metadata")
    ];
}

using SmartDrag.Core.Primitives;

namespace SmartDrag.Core.Actions;

public static class BuiltInActionIds
{
    public static readonly ActionId CompressImage = new("image.compress");
    public static readonly ActionId ConvertImageToWebP = new("image.convert.webp");
    public static readonly ActionId RemoveImageMetadata = new("image.remove-metadata");
}

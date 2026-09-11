namespace SmartDrag.Core.Actions;

public sealed record AppCapabilities
{
    public bool ImagingAvailable { get; init; }
    /// <summary>
    /// Concrete WebP encoding is deliberately opt-in and remains false until the G3 codec benchmark is accepted.
    /// </summary>
    public bool WebpEncodingAvailable { get; init; }
    public bool PdfAvailable { get; init; }
    public bool ArchiveAvailable { get; init; }
    public bool VideoAvailable { get; init; }
}

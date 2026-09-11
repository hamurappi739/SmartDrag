namespace SmartDrag.Core.Overlay;

public interface IOverlayService
{
    Task ShowAsync(OverlayModel model, OverlayPlacement placement, CancellationToken cancellationToken);
    Task UpdateAsync(OverlayModel model, OverlayPlacement placement, CancellationToken cancellationToken);
    Task HideAsync(OverlayHideReason reason, CancellationToken cancellationToken);
}

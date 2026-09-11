using System.Runtime.InteropServices;
using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Ole;

public sealed class OleDropTargetRegistration : IDisposable
{
    private nint _hwnd;
    private IDropTargetNative? _dropTarget;

    public OleDropTargetRegistration(nint hwnd, IDropTargetNative dropTarget)
    {
        if (hwnd == nint.Zero)
        {
            throw new ArgumentException("A valid HWND is required.", nameof(hwnd));
        }

        ArgumentNullException.ThrowIfNull(dropTarget);

        var hr = NativeMethods.RegisterDragDrop(hwnd, dropTarget);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        _hwnd = hwnd;
        _dropTarget = dropTarget; // keep managed CCW alive for registration lifetime
    }

    public void Dispose()
    {
        if (_hwnd == nint.Zero)
        {
            return;
        }

        NativeMethods.RevokeDragDrop(_hwnd);
        _hwnd = nint.Zero;
        _dropTarget = null;
    }
}

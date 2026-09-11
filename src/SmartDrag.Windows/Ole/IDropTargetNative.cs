using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Ole;

[ComVisible(true)]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDropTargetNative
{
    [PreserveSig]
    int DragEnter(IDataObject dataObject, uint keyState, POINTL point, ref uint effect);

    [PreserveSig]
    int DragOver(uint keyState, POINTL point, ref uint effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(IDataObject dataObject, uint keyState, POINTL point, ref uint effect);
}

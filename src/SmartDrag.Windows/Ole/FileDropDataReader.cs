using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Ole;

public static class FileDropDataReader
{
    private static FORMATETC CreateFormat() => new()
    {
        cfFormat = NativeConstants.CF_HDROP,
        dwAspect = DVASPECT.DVASPECT_CONTENT,
        lindex = -1,
        ptd = nint.Zero,
        tymed = TYMED.TYMED_HGLOBAL
    };

    public static bool CanRead(IDataObject dataObject)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        try
        {
            var format = CreateFormat();
            return dataObject.QueryGetData(ref format) >= 0;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidCastException)
        {
            return false;
        }
    }

    public static bool TryReadPaths(IDataObject dataObject, out IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        paths = Array.Empty<string>();

        var format = CreateFormat();
        try
        {
            if (dataObject.QueryGetData(ref format) < 0)
            {
                return false;
            }

            dataObject.GetData(ref format, out var medium);
            try
            {
                if (medium.tymed != TYMED.TYMED_HGLOBAL || medium.unionmember == nint.Zero)
                {
                    return false;
                }

                const uint QueryCount = 0xFFFFFFFF;
                var count = NativeMethods.DragQueryFileW(medium.unionmember, QueryCount, null, 0);
                if (count == 0)
                {
                    return false;
                }

                var result = new List<string>((int)count);
                for (uint index = 0; index < count; index++)
                {
                    var length = NativeMethods.DragQueryFileW(medium.unionmember, index, null, 0);
                    if (length == 0)
                    {
                        continue;
                    }

                    var buffer = new char[length + 1];
                    var copied = NativeMethods.DragQueryFileW(medium.unionmember, index, buffer, (uint)buffer.Length);
                    if (copied > 0)
                    {
                        result.Add(new string(buffer, 0, (int)copied));
                    }
                }

                paths = result;
                return result.Count > 0;
            }
            finally
            {
                NativeMethods.ReleaseStgMedium(ref medium);
            }
        }
        catch (Exception ex) when (ex is ExternalException or InvalidCastException or ArgumentException)
        {
            paths = Array.Empty<string>();
            return false;
        }
    }
}

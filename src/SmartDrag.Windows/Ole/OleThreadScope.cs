using System.Runtime.InteropServices;
using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Ole;

public sealed class OleThreadScope : IDisposable
{
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private bool _initialized;

    public OleThreadScope()
    {
        var hr = NativeMethods.OleInitialize(nint.Zero);
        if (hr is not (S_OK or S_FALSE))
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        _initialized = true;
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        _initialized = false;
        NativeMethods.OleUninitialize();
    }
}

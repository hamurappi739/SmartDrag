using System.ComponentModel;
using System.Runtime.InteropServices;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Primitives;
using SmartDrag.Windows.Interop;
using SmartDrag.Windows.Diagnostics;

namespace SmartDrag.Windows.Probe;

internal sealed class ProbeWindow : IDisposable
{
    private const string WindowClassName = "SmartDrag.Windows.Probe.Overlay";
    private const int LogicalWidth = 320;
    private const int LogicalPadding = 8;
    private const int LogicalRowHeight = 44;
    private const int LogicalRowGap = 4;
    private const nuint TimerId = 1;

    private readonly NativeMethods.WndProc _wndProc;
    private readonly nint _instance;
    private readonly TimelineLogger _logger;
    private ProbeAction? _hoveredAction;
    private bool _disposed;

    public ProbeWindow(TimelineLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wndProc = WindowProc;
        _instance = NativeMethods.GetModuleHandleW(null);

        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = _instance,
            lpszClassName = WindowClassName
        };

        var atom = NativeMethods.RegisterClassExW(ref wndClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            const int ERROR_CLASS_ALREADY_EXISTS = 1410;
            if (error != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw new Win32Exception(error, "RegisterClassExW failed.");
            }
        }

        Handle = NativeMethods.CreateWindowExW(
            NativeConstants.WS_EX_TOPMOST | NativeConstants.WS_EX_TOOLWINDOW | NativeConstants.WS_EX_NOACTIVATE,
            WindowClassName,
            "SmartDrag Windows Probe",
            NativeConstants.WS_POPUP,
            0,
            0,
            LogicalWidth,
            CalculateLogicalHeight(),
            nint.Zero,
            nint.Zero,
            _instance,
            nint.Zero);

        if (Handle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW failed.");
        }

        if (NativeMethods.SetTimer(Handle, TimerId, 33, nint.Zero) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetTimer failed.");
        }
    }

    public nint Handle { get; }
    public bool IsVisible { get; private set; }

    public event EventHandler? Tick;

    public void ShowNearCursor(POINT cursor)
    {
        var monitor = NativeMethods.MonitorFromPoint(cursor, NativeConstants.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        var work = new RECT { Left = 0, Top = 0, Right = int.MaxValue, Bottom = int.MaxValue };
        if (monitor != nint.Zero && NativeMethods.GetMonitorInfoW(monitor, ref monitorInfo))
        {
            work = monitorInfo.rcWork;
        }

        // The hidden HWND may still belong to the monitor used by the previous drag (or 0,0 at startup).
        // Move it invisibly to the target monitor first, then query GetDpiForWindow. This keeps the probe
        // on documented per-monitor window DPI semantics without presenting a wrongly scaled first frame.
        if (!NativeMethods.SetWindowPos(
            Handle,
            nint.Zero,
            cursor.X,
            cursor.Y,
            0,
            0,
            NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Hidden DPI anchor SetWindowPos failed.");
        }

        var dpi = NativeMethods.GetDpiForWindow(Handle);
        if (dpi == 0)
        {
            dpi = 96;
        }

        var scale = dpi / 96.0;
        var width = (int)Math.Round(LogicalWidth * scale);
        var height = (int)Math.Round(CalculateLogicalHeight() * scale);
        var offset = (int)Math.Round(28 * scale);

        var placement = OverlayPlacementEngine.Calculate(
            new PointD(cursor.X, cursor.Y),
            new RectD(work.Left, work.Top, work.Right, work.Bottom),
            new SizeD(width, height),
            offset);

        if (!NativeMethods.SetWindowPos(
            Handle,
            NativeConstants.HWND_TOPMOST,
            (int)Math.Round(placement.X),
            (int)Math.Round(placement.Y),
            (int)Math.Round(placement.Width),
            (int)Math.Round(placement.Height),
            NativeConstants.SWP_NOACTIVATE | NativeConstants.SWP_SHOWWINDOW))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowPos failed.");
        }

        NativeMethods.ShowWindow(Handle, NativeConstants.SW_SHOWNOACTIVATE);
        IsVisible = true;
        NativeMethods.InvalidateRect(Handle, nint.Zero, true);
    }

    public void Hide()
    {
        if (!IsVisible)
        {
            return;
        }

        _hoveredAction = null;
        NativeMethods.ShowWindow(Handle, NativeConstants.SW_HIDE);
        IsVisible = false;
    }

    public ProbeAction? HitTestScreenPoint(POINTL screenPoint)
    {
        var point = new POINT { X = screenPoint.X, Y = screenPoint.Y };
        if (!NativeMethods.ScreenToClient(Handle, ref point))
        {
            return null;
        }

        var dpi = NativeMethods.GetDpiForWindow(Handle);
        if (dpi == 0)
        {
            dpi = 96;
        }

        var scale = dpi / 96.0;
        var padding = (int)Math.Round(LogicalPadding * scale);
        var rowHeight = (int)Math.Round(LogicalRowHeight * scale);
        var rowGap = (int)Math.Round(LogicalRowGap * scale);

        for (var index = 0; index < ProbeAction.All.Count; index++)
        {
            var top = padding + (index * (rowHeight + rowGap));
            var bottom = top + rowHeight;
            if (point.Y >= top && point.Y < bottom)
            {
                return ProbeAction.All[index];
            }
        }

        return null;
    }

    public void SetHoveredAction(ProbeAction? action)
    {
        if (Equals(_hoveredAction, action))
        {
            return;
        }

        _hoveredAction = action;
        NativeMethods.InvalidateRect(Handle, nint.Zero, false);
    }

    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case NativeConstants.WM_TIMER:
                    if (wParam == TimerId)
                    {
                        Tick?.Invoke(this, EventArgs.Empty);
                        return nint.Zero;
                    }
                    break;

                case NativeConstants.WM_PAINT:
                    Paint();
                    return nint.Zero;

                case NativeConstants.WM_DPICHANGED:
                    var suggested = Marshal.PtrToStructure<RECT>(lParam);
                    NativeMethods.SetWindowPos(
                        hwnd,
                        NativeConstants.HWND_TOPMOST,
                        suggested.Left,
                        suggested.Top,
                        suggested.Width,
                        suggested.Height,
                        NativeConstants.SWP_NOACTIVATE);
                    return nint.Zero;

                case NativeConstants.WM_DESTROY:
                    NativeMethods.PostQuitMessage(0);
                    return nint.Zero;
            }
        }
        catch (Exception ex)
        {
            // WndProc is another unmanaged boundary. Future UI/tick observers must not be able
            // to throw a managed exception back through user32.dll.
            _logger.Write("window.wndproc-exception", new
            {
                message,
                exceptionType = ex.GetType().FullName,
                hresult = ex.HResult
            });
            return nint.Zero;
        }

        return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private void Paint()
    {
        var hdc = NativeMethods.BeginPaint(Handle, out var paint);
        if (hdc == nint.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.GetClientRect(Handle, out var client);
            using var background = new NativeBrush(Rgb(31, 31, 35));
            NativeMethods.FillRect(hdc, ref client, background.Handle);
            NativeMethods.SetBkMode(hdc, NativeConstants.TRANSPARENT);
            NativeMethods.SetTextColor(hdc, Rgb(245, 245, 247));

            var dpi = NativeMethods.GetDpiForWindow(Handle);
            if (dpi == 0)
            {
                dpi = 96;
            }

            var scale = dpi / 96.0;
            var padding = (int)Math.Round(LogicalPadding * scale);
            var rowHeight = (int)Math.Round(LogicalRowHeight * scale);
            var rowGap = (int)Math.Round(LogicalRowGap * scale);
            var textInset = (int)Math.Round(12 * scale);

            for (var index = 0; index < ProbeAction.All.Count; index++)
            {
                var action = ProbeAction.All[index];
                var top = padding + (index * (rowHeight + rowGap));
                var rect = new RECT
                {
                    Left = padding,
                    Top = top,
                    Right = client.Right - padding,
                    Bottom = top + rowHeight
                };

                using var brush = new NativeBrush(Equals(_hoveredAction, action)
                    ? Rgb(67, 67, 73)
                    : Rgb(45, 45, 50));
                NativeMethods.FillRect(hdc, ref rect, brush.Handle);

                rect.Left += textInset;
                NativeMethods.DrawTextW(
                    hdc,
                    action.Label,
                    action.Label.Length,
                    ref rect,
                    NativeConstants.DT_LEFT | NativeConstants.DT_VCENTER | NativeConstants.DT_SINGLELINE | NativeConstants.DT_NOPREFIX);
            }
        }
        finally
        {
            NativeMethods.EndPaint(Handle, ref paint);
        }
    }

    private static int CalculateLogicalHeight() =>
        (LogicalPadding * 2) + (ProbeAction.All.Count * LogicalRowHeight) + ((ProbeAction.All.Count - 1) * LogicalRowGap);

    private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Handle != nint.Zero)
        {
            NativeMethods.KillTimer(Handle, TimerId);
            NativeMethods.DestroyWindow(Handle);
        }
    }

    private sealed class NativeBrush : IDisposable
    {
        public NativeBrush(uint color)
        {
            Handle = NativeMethods.CreateSolidBrush(color);
            if (Handle == nint.Zero)
            {
                throw new InvalidOperationException("CreateSolidBrush failed.");
            }
        }

        public nint Handle { get; }
        public void Dispose() => NativeMethods.DeleteObject(Handle);
    }
}

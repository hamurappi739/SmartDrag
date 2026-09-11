namespace SmartDrag.Windows.Interop;

public static class NativeConstants
{
    public const uint EVENT_OBJECT_DRAGSTART = 0x8021;
    public const uint EVENT_OBJECT_DRAGCANCEL = 0x8022;
    public const uint EVENT_OBJECT_DRAGCOMPLETE = 0x8023;

    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const uint WS_POPUP = 0x80000000;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_NOACTIVATE = 0x08000000;

    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public static readonly nint HWND_TOPMOST = new(-1);

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_DPICHANGED = 0x02E0;

    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    public const uint GA_ROOT = 2;

    public const int TRANSPARENT = 1;
    public const uint DT_LEFT = 0x00000000;
    public const uint DT_VCENTER = 0x00000004;
    public const uint DT_SINGLELINE = 0x00000020;
    public const uint DT_NOPREFIX = 0x00000800;

    public const short CF_HDROP = 15;

    public const uint DROPEFFECT_NONE = 0x00000000;
    public const uint DROPEFFECT_COPY = 0x00000001;
    public const uint DROPEFFECT_MOVE = 0x00000002;
    public const uint DROPEFFECT_LINK = 0x00000004;

    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);
}

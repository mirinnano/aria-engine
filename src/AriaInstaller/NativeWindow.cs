using System.Runtime.InteropServices;
using System.ComponentModel;

namespace AriaInstaller;

internal sealed class NativeInstallerWindow : IDisposable
{
    private const int WS_OVERLAPPEDWINDOW = 0xCF0000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CHILD = 0x40000000;
    private const int WM_DESTROY = 2;
    private const int SW_SHOW = 5;

    private nint _hwnd;
    private bool _disposed;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);
    private static readonly WndProcDelegate _wndProc = WndProc;
    private static readonly GCHandle _gcHandle = GCHandle.Alloc(_wndProc);

    public NativeInstallerWindow(string title, int width, int height)
    {
        nint hInst = GetModuleHandleW(nint.Zero);
        nint clsName = Marshal.StringToHGlobalUni("AriaInstWnd");
        nint wndTitle = Marshal.StringToHGlobalUni(title);

        try
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = hInst,
                hCursor = LoadCursorW(nint.Zero, 32512),
                hbrBackground = (nint)16, // COLOR_WINDOW + 1 = white brush
                lpszClassName = clsName
            };

            if (RegisterClassExW(ref wc) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassExW");

            _hwnd = CreateWindowExW(0, "AriaInstWnd", title, WS_OVERLAPPEDWINDOW,
                100, 100, width, height, 0, 0, hInst, 0);

            if (_hwnd == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW");
        }
        finally
        {
            Marshal.FreeHGlobal(clsName);
            Marshal.FreeHGlobal(wndTitle);
        }
    }

    public void Run()
    {
        ShowWindow(_hwnd, SW_SHOW);
        UpdateWindow(_hwnd);

        while (GetMessageW(out MSG msg, 0, 0, 0) != 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_DESTROY) { PostQuitMessage(0); return 0; }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose() { if (!_disposed && _hwnd != 0) { DestroyWindow(_hwnd); _hwnd = 0; _disposed = true; } }

    [DllImport("user32.dll", SetLastError = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hWnd);
    [DllImport("kernel32.dll")] private static extern nint GetModuleHandleW(nint lpModuleName);
    [DllImport("user32.dll")] private static extern nint LoadCursorW(nint hInstance, int lpCursorName);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEXW
    {
        public int cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        public nint lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam; public nint lParam; public uint time; public int pt_x; public int pt_y; }
}

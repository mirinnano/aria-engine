using System.Runtime.InteropServices;
using System.ComponentModel;

namespace AriaInstaller;

/// <summary>
/// Minimal Win32 native window for the installer.
/// No WinForms/WPF dependency - pure P/Invoke.
/// </summary>
internal sealed class NativeInstallerWindow : IDisposable
{
    // Win32 constants
    private const int WS_OVERLAPPEDWINDOW = 0xCF0000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CHILD = 0x40000000;
    private const int WS_TABSTOP = 0x10000;
    private const int BS_PUSHBUTTON = 0;
    private const int BS_DEFPUSHBUTTON = 1;
    private const int SS_LEFT = 0;
    private const int SS_CENTER = 1;
    private const int PBS_SMOOTH = 1;
    private const int WM_CREATE = 1;
    private const int WM_DESTROY = 2;
    private const int WM_COMMAND = 0x111;
    private const int WM_CLOSE = 0x10;
    private const int WM_CTLCOLORSTATIC = 0x138;
    private const int WM_SETFONT = 0x30;
    private const int SW_SHOW = 5;
    private const int CW_USEDEFAULT = unchecked((int)0x80000000);
    private const int COLOR_WINDOW = 5;
    private const int IDC_PROGRESS = 101;
    private const int IDC_BUTTON = 102;
    private const int IDC_STATUS = 103;
    private const int IDC_MODE = 104;
    private const int IDC_TARGET = 105;
    private const int WM_TIMER = 0x113;
    private const int TIMER_ANIM = 1;

    private nint _hwnd;
    private nint _hProgress;
    private nint _hButton;
    private nint _hStatus;
    private nint _hMode;
    private nint _hTarget;
    private nint _hAnimDC;
    private string _buttonText = "インストール";
    private string _currentFile = "";
    private int _animFrame;
    private bool _animating;
    private readonly Action? _onButtonClick;
    private bool _disposed;

    public string StatusText { set { if (_hStatus != 0) SetWindowText(_hStatus, value); } }
    public string ModeText { set { if (_hMode != 0) SetWindowText(_hMode, value); } }
    public string TargetText { set { if (_hTarget != 0) SetWindowText(_hTarget, value); } }
    public string ButtonText { set { _buttonText = value; if (_hButton != 0) SetWindowText(_hButton, value); } }
    public bool ButtonEnabled { set { if (_hButton != 0) EnableWindow(_hButton, value); } }
    public int ProgressValue { set { if (_hProgress != 0) SendMessage(_hProgress, 0x402, value, 0); } }
    public int ProgressMax { set { if (_hProgress != 0) SendMessage(_hProgress, 0x403, value, 0); } }
    public string CurrentFile { set { _currentFile = value; if (!_animating) { _animating = true; SetTimer(_hwnd, TIMER_ANIM, 40, 0); } } }
    public void StopAnimation() { _animating = false; KillTimer(_hwnd, TIMER_ANIM); InvalidateRect(_hwnd, 0, true); }
    public Action? ButtonClickAction { get; set; }

    public NativeInstallerWindow(string title, int width, int height, Action? onButtonClick = null)
    {
        _onButtonClick = onButtonClick;

        // Initialize common controls (required for progress bar)
        var icc = new INITCOMMONCONTROLSEX { dwSize = 8, dwICC = 0x20 };
        InitCommonControlsEx(ref icc);

        // Allocate class name in native memory to prevent GC issues
        nint classNamePtr = Marshal.StringToHGlobalUni("AriaInstallerWindow");
        nint titlePtr = Marshal.StringToHGlobalUni(title);

        var wc = new WNDCLASSEXW();
        wc.cbSize = Marshal.SizeOf<WNDCLASSEXW>();
        wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc);
        wc.hInstance = GetModuleHandleW(nint.Zero);
        wc.hCursor = LoadCursorW(nint.Zero, 32512);
        wc.hbrBackground = (nint)(COLOR_WINDOW + 1);
        wc.lpszClassName = classNamePtr;

        ushort atom = RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int err = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(classNamePtr);
            Marshal.FreeHGlobal(titlePtr);
            throw new Win32Exception(err, $"RegisterClassExW failed (error {err})");
        }

        var gch = GCHandle.Alloc(this);
        _hwnd = CreateWindowExW(0, "AriaInstallerWindow", title, WS_OVERLAPPEDWINDOW,
            100, 100, width, height, 0, 0, wc.hInstance, GCHandle.ToIntPtr(gch));

        Marshal.FreeHGlobal(classNamePtr);
        Marshal.FreeHGlobal(titlePtr);

        if (_hwnd == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
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

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_CREATE)
        {
            CreateControls(hwnd);
            return 0;
        }

        if (msg == WM_COMMAND && (wParam & 0xFFFF) == IDC_BUTTON)
        {
            _onButtonClick?.Invoke();
            return 0;
        }

        if (msg == WM_CTLCOLORSTATIC)
        {
            SetBkMode((nint)wParam, 1); // TRANSPARENT
            return (nint)GetStockObject(5); // WHITE_BRUSH
        }

        if (msg == WM_DESTROY)
        {
            KillTimer(hwnd, TIMER_ANIM);
            PostQuitMessage(0);
            return 0;
        }

        if (msg == WM_TIMER && wParam == TIMER_ANIM && _animating)
        {
            _animFrame++;
            DrawFileAnimation(hwnd);
            return 0;
        }

        if (msg == 0x000F) // WM_PAINT
        {
            if (_animating) DrawFileAnimation(hwnd);
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void CreateControls(nint parent)
    {
        nint hFont = GetStockObject(17); // DEFAULT_GUI_FONT
        int y = 20;

        _hMode = CreateWindowExW(0, "STATIC", "", WS_CHILD | WS_VISIBLE | SS_LEFT,
            20, y, 440, 20, parent, IDC_MODE, 0, 0);
        SendMessage(_hMode, WM_SETFONT, hFont, 0);
        y += 24;

        _hTarget = CreateWindowExW(0, "STATIC", "", WS_CHILD | WS_VISIBLE | SS_LEFT,
            20, y, 440, 20, parent, IDC_TARGET, 0, 0);
        SendMessage(_hTarget, WM_SETFONT, hFont, 0);
        y += 36;

        _hProgress = CreateWindowExW(0, "msctls_progress32", "", WS_CHILD | WS_VISIBLE | PBS_SMOOTH,
            20, y, 440, 24, parent, IDC_PROGRESS, 0, 0);
        y += 40;

        _hStatus = CreateWindowExW(0, "STATIC", "", WS_CHILD | WS_VISIBLE | SS_LEFT,
            20, y, 440, 40, parent, IDC_STATUS, 0, 0);
        SendMessage(_hStatus, WM_SETFONT, hFont, 0);
        y += 54;

        _hButton = CreateWindowExW(0, "BUTTON", _buttonText,
            WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON | WS_TABSTOP,
            340, y, 120, 32, parent, IDC_BUTTON, 0, 0);
        SendMessage(_hButton, WM_SETFONT, hFont, 0);
    }

    public void Dispose()
    {
        if (!_disposed && _hwnd != 0)
        {
            KillTimer(_hwnd, TIMER_ANIM);
            DestroyWindow(_hwnd);
            _hwnd = 0;
            _disposed = true;
        }
    }

    // === GDI Animation Drawing ===
    private void DrawFileAnimation(nint hwnd)
    {
        nint hdc = GetDC(hwnd);
        if (hdc == 0) return;

        RECT rect;
        GetClientRect(hwnd, out rect);

        int animY = 80, centerX = rect.right / 2, folderW = 40, folderH = 30;
        int leftFolderX = 60, rightFolderX = rect.right - 100;

        nint darkPen = CreatePen(0, 1, 0x004080);
        nint oldPen = SelectObject(hdc, darkPen);

        // Left folder
        Rectangle(hdc, leftFolderX, animY, leftFolderX + folderW, animY + folderH);
        Rectangle(hdc, leftFolderX, animY - 4, leftFolderX + 18, animY);
        MoveToEx(hdc, leftFolderX + 18, animY - 4, 0);
        LineTo(hdc, leftFolderX + 28, animY);
        SetTextColor(hdc, 0x004080);
        TextOutW(hdc, leftFolderX + 46, animY + 4, "\u30BD\u30FC\u30B9", 4);

        // Right folder
        Rectangle(hdc, rightFolderX, animY, rightFolderX + folderW, animY + folderH);
        Rectangle(hdc, rightFolderX, animY - 4, rightFolderX + 18, animY);
        MoveToEx(hdc, rightFolderX + 18, animY - 4, 0);
        LineTo(hdc, rightFolderX + 28, animY);
        TextOutW(hdc, rightFolderX + 44, animY + 4, "\u30A4\u30F3\u30B9\u30C8\u30FC\u30EB\u5148", 8);

        // Flying file
        float t = (_animFrame % 60) / 59f;
        int fileX = (int)(leftFolderX + folderW + 5 + t * (rightFolderX - leftFolderX - folderW - 15));
        int fileY = animY + (int)(Math.Sin(t * Math.PI) * 20);
        nint fileBrush = CreateSolidBrush(0x00C0FF);
        SelectObject(hdc, fileBrush);
        nint nullPen = GetStockObject(8); // NULL_PEN
        SelectObject(hdc, nullPen);
        Rectangle(hdc, fileX, fileY, fileX + 10, fileY + 14);
        DeleteObject(fileBrush);

        // Dotted path
        for (int i = 0; i < 16; i++)
        {
            float tt = i / 15f;
            int dx = (int)(leftFolderX + folderW + 5 + tt * (rightFolderX - leftFolderX - folderW - 15));
            int dy = animY + 7 + (int)(Math.Sin(tt * Math.PI) * 20);
            SetPixel(hdc, dx, dy, 0xA0A0A0);
        }

        // File name
        SetTextColor(hdc, 0x000000);
        string displayFile = _currentFile.Length > 55 ? "..." + _currentFile[^52..] : _currentFile;
        TextOutW(hdc, centerX - 180, animY + 40, displayFile, displayFile.Length);

        SelectObject(hdc, oldPen);
        DeleteObject(darkPen);
        ReleaseDC(hwnd, hdc);
    }

    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool SetWindowText(nint hWnd, string lpString);
    [DllImport("user32.dll")] private static extern bool EnableWindow(nint hWnd, bool bEnable);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);
    [DllImport("kernel32.dll")] private static extern nint GetModuleHandleW(nint lpModuleName);
    [DllImport("user32.dll")] private static extern nint LoadCursorW(nint hInstance, int lpCursorName);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int fnObject);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint hdc, int mode);
    [DllImport("gdi32.dll")] private static extern nint CreatePen(int fnPenStyle, int nWidth, int crColor);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(int crColor);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint hdc, nint h);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint h);
    [DllImport("gdi32.dll")] private static extern bool Rectangle(nint hdc, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool MoveToEx(nint hdc, int x, int y, nint lppt);
    [DllImport("gdi32.dll")] private static extern bool LineTo(nint hdc, int x, int y);
    [DllImport("gdi32.dll")] private static extern int SetTextColor(nint hdc, int crColor);
    [DllImport("gdi32.dll")] private static extern bool TextOutW(nint hdc, int x, int y, string lpString, int c);
    [DllImport("gdi32.dll")] private static extern int SetPixel(nint hdc, int x, int y, int crColor);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hWnd);
    [DllImport("user32.dll")] private static extern bool ReleaseDC(nint hWnd, nint hDC);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern nint SetTimer(nint hWnd, nint nIDEvent, uint uElapse, nint lpTimerFunc);
    [DllImport("user32.dll")] private static extern bool KillTimer(nint hWnd, nint uIDEvent);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(nint hWnd, nint lpRect, bool bErase);

    // WndProc delegate - MUST be rooted to prevent GC, MUST use StdCall
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);
    private static readonly WndProcDelegate _wndProc = StaticWndProc;
    private static readonly GCHandle _wndProcHandle = GCHandle.Alloc(_wndProc);

    private static nint StaticWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_CREATE)
        {
            var cs = Marshal.PtrToStructure<CREATESTRUCT>(lParam);
            if (cs.lpCreateParams != 0)
            {
                SetWindowLongPtrW(hwnd, -21, cs.lpCreateParams); // GWLP_USERDATA
            }
            return 0;
        }

        var ptr = GetWindowLongPtrW(hwnd, -21);
        if (ptr != 0)
        {
            var gch = GCHandle.FromIntPtr(ptr);
            if (gch.Target is NativeInstallerWindow w)
                return w.WndProc(hwnd, msg, wParam, lParam);
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CREATESTRUCT
    {
        public nint lpCreateParams;
        public nint hInstance;
        public nint hMenu;
        public nint hwndParent;
        public int cy;
        public int cx;
        public int y;
        public int x;
        public int style;
        public nint lpszName;
        public nint lpszClass;
        public uint dwExStyle;
    }

    [DllImport("user32.dll")] private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [DllImport("comctl32.dll")] private static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX picc);

    [StructLayout(LayoutKind.Sequential)]
    private struct INITCOMMONCONTROLSEX
    {
        public int dwSize;
        public uint dwICC;
    }
}

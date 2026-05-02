#![windows_subsystem = "windows"]

use std::ffi::OsString;
use std::os::windows::ffi::OsStrExt;
use std::mem;
use std::path::PathBuf;

type HINSTANCE = isize; type HWND = isize; type HDC = isize; type HFONT = isize;
type HBRUSH = isize; type HPEN = isize; type HMENU = isize;
type WPARAM = usize; type LPARAM = isize; type LRESULT = isize;
type ATOM = u16; type BOOL = i32;

const WS_OVERLAPPEDWINDOW: u32 = 0xCF0000;
const WS_CHILD: u32 = 0x40000000;
const WS_VISIBLE: u32 = 0x10000000;
const WS_BORDER: u32 = 0x800000;
const WS_TABSTOP: u32 = 0x10000;
const BS_DEFPUSHBUTTON: u32 = 1;
const BS_PUSHBUTTON: u32 = 0;
const ES_LEFT: u32 = 0;
const ES_AUTOHSCROLL: u32 = 0x80;
const SS_LEFT: u32 = 0;
const SS_CENTER: u32 = 1;
const SS_SUNKEN: u32 = 0x1000;
const PBS_SMOOTH: u32 = 1;
const LBS_NOTIFY: u32 = 1;
const WS_VSCROLL: u32 = 0x200000;
const WM_DESTROY: u32 = 2; const WM_CREATE: u32 = 1;
const WM_COMMAND: u32 = 0x111; const WM_TIMER: u32 = 0x113;
const WM_PAINT: u32 = 0x0F; const WM_SETFONT: u32 = 0x30;
const WM_CTLCOLORSTATIC: u32 = 0x138; const WM_SIZE: u32 = 5;
const SW_SHOW: i32 = 5; const IDC_ARROW: usize = 32512;
const COLOR_WINDOW: i32 = 5; const COLOR_BTNFACE: i32 = 15;
const DEFAULT_GUI_FONT: i32 = 17;
const PS_SOLID: i32 = 0; const NULL_PEN: i32 = 8; const TRANSPARENT: i32 = 1;

// Control IDs
const IDC_INSTALL: usize = 201; const IDC_BROWSE: usize = 202;
const IDC_CANCEL: usize = 203; const IDC_PATH: usize = 204;
const IDC_LOG: usize = 205; const IDC_PROGRESS: usize = 206;
const IDC_TITLE: usize = 207; const IDC_SUBTITLE: usize = 208;
const IDC_INFO: usize = 209;
const IDC_DESKTOP_CB: usize = 210; const IDC_STARTMENU_CB: usize = 211;
const TIMER_ANIM: usize = 1;

const BS_AUTOCHECKBOX: u32 = 3;

// State
static mut HINST: HINSTANCE = 0; static mut HWND_MAIN: HWND = 0;
static mut HWND_INSTALL: HWND = 0; static mut HWND_BROWSE: HWND = 0;
static mut HWND_CANCEL: HWND = 0; static mut HWND_PATH: HWND = 0;
static mut HWND_LOG: HWND = 0; static mut HWND_PROGRESS: HWND = 0;
static mut HWND_TITLE: HWND = 0; static mut HWND_SUBTITLE: HWND = 0;
static mut HWND_INFO: HWND = 0;
static mut HWND_DESKTOP_CB: HWND = 0; static mut HWND_STARTMENU_CB: HWND = 0;
static mut HFONT_BIG: HFONT = 0; static mut HFONT_NORMAL: HFONT = 0;
static mut ANIM_FRAME: i32 = 0;
static mut CURRENT_FILE: String = String::new();
static mut INSTALLING: bool = false;
static mut INSTALL_PATH: String = String::new();
static mut SOURCE_DIR: String = String::new();
static mut TOTAL_FILES: usize = 0; static mut COPIED_FILES: usize = 0;
static mut LOG_LINES: Vec<String> = Vec::new();

// Product info
const PRODUCT: &str = "umikaze";
const PUBLISHER: &str = "Ponkotusoft";
const VERSION: &str = "1.0.0";
const DESCRIPTION: &str = "ビジュアルノベルエンジン";

unsafe fn to_wide(s: &str) -> Vec<u16> {
    OsString::from(s).encode_wide().chain(std::iter::once(0)).collect()
}

unsafe fn set_text(hwnd: HWND, text: &str) {
    let w = to_wide(text);
    SetWindowTextW(hwnd, w.as_ptr());
}

unsafe fn log_line(text: &str) {
    LOG_LINES.push(text.to_string());
    let w = to_wide(text);
    SendMessageW(HWND_LOG, 0x0180, 0, w.as_ptr() as _); // LB_ADDSTRING
    let count = SendMessageW(HWND_LOG, 0x018B, 0, 0); // LB_GETCOUNT
    SendMessageW(HWND_LOG, 0x0197, (count - 1) as usize, 0); // LB_SETCURSEL
}

#[repr(C)] struct WNDCLASSEXW { cbSize: u32, style: u32, lpfnWndProc: extern "system" fn(HWND, u32, WPARAM, LPARAM) -> LRESULT, cbClsExtra: i32, cbWndExtra: i32, hInstance: HINSTANCE, hIcon: isize, hCursor: isize, hbrBackground: isize, lpszMenuName: *const u16, lpszClassName: *const u16, hIconSm: isize }
#[repr(C)] struct MSG { hwnd: HWND, message: u32, wParam: WPARAM, lParam: LPARAM, time: u32, pt_x: i32, pt_y: i32 }
#[repr(C)] struct PAINTSTRUCT { hdc: HDC, fErase: BOOL, rc_left: i32, rc_top: i32, rc_right: i32, rc_bottom: i32, fRestore: BOOL, fIncUpdate: BOOL, rgbReserved: [u8; 32] }
#[repr(C)] struct RECT { left: i32, top: i32, right: i32, bottom: i32 }
#[repr(C)] struct INITCOMMONCONTROLSEX { dwSize: u32, dwICC: u32 }
#[repr(C)] struct BROWSEINFOW { hwndOwner: HWND, pidlRoot: isize, pszDisplayName: *mut u16, lpszTitle: *const u16, ulFlags: u32, lpfn: isize, lParam: isize, iImage: i32 }

#[link(name="user32")] extern "system" {
    fn RegisterClassExW(lpwc: *const WNDCLASSEXW) -> ATOM;
    fn CreateWindowExW(dwExStyle: u32, lpClassName: *const u16, lpWindowName: *const u16, dwStyle: u32, x: i32, y: i32, w: i32, h: i32, hWndParent: HWND, hMenu: HMENU, hInstance: HINSTANCE, lpParam: isize) -> HWND;
    fn ShowWindow(hWnd: HWND, nCmdShow: i32) -> BOOL;
    fn GetMessageW(lpMsg: *mut MSG, hWnd: HWND, wMsgFilterMin: u32, wMsgFilterMax: u32) -> BOOL;
    fn TranslateMessage(lpMsg: *const MSG) -> BOOL;
    fn DispatchMessageW(lpMsg: *const MSG) -> LRESULT;
    fn DefWindowProcW(hWnd: HWND, Msg: u32, wParam: WPARAM, lParam: LPARAM) -> LRESULT;
    fn PostQuitMessage(nExitCode: i32);
    fn SetWindowTextW(hWnd: HWND, lpString: *const u16) -> BOOL;
    fn EnableWindow(hWnd: HWND, bEnable: BOOL) -> BOOL;
    fn SendMessageW(hWnd: HWND, Msg: u32, wParam: WPARAM, lParam: LPARAM) -> LRESULT;
    fn SetTimer(hWnd: HWND, nIDEvent: usize, uElapse: u32, lpTimerFunc: isize) -> usize;
    fn KillTimer(hWnd: HWND, uIDEvent: usize) -> BOOL;
    fn InvalidateRect(hWnd: HWND, lpRect: *const RECT, bErase: BOOL) -> BOOL;
    fn BeginPaint(hWnd: HWND, lpPaint: *mut PAINTSTRUCT) -> HDC;
    fn EndPaint(hWnd: HWND, lpPaint: *const PAINTSTRUCT) -> BOOL;
    fn GetClientRect(hWnd: HWND, lpRect: *mut RECT) -> BOOL;
    fn LoadCursorW(hInstance: HINSTANCE, lpCursorName: usize) -> isize;
    fn GetSysColorBrush(nIndex: i32) -> isize;
    fn SetFocus(hWnd: HWND) -> HWND;
    fn MessageBoxW(hWnd: HWND, lpText: *const u16, lpCaption: *const u16, uType: u32) -> i32;
}

#[link(name="kernel32")] extern "system" { fn GetModuleHandleW(lpModuleName: *const u16) -> HINSTANCE; }

#[link(name="shell32")] extern "system" {
    fn SHBrowseForFolderW(lpbi: *const BROWSEINFOW) -> isize;
    fn SHGetPathFromIDListW(pidl: isize, pszPath: *mut u16) -> BOOL;
}

#[link(name="gdi32")] extern "system" {
    fn GetStockObject(fnObject: i32) -> isize;
    fn CreatePen(fnPenStyle: i32, nWidth: i32, crColor: u32) -> isize;
    fn CreateSolidBrush(crColor: u32) -> isize;
    fn DeleteObject(ho: isize) -> BOOL;
    fn SelectObject(hdc: HDC, h: isize) -> isize;
    fn Rectangle(hdc: HDC, left: i32, top: i32, right: i32, bottom: i32) -> BOOL;
    fn MoveToEx(hdc: HDC, x: i32, y: i32, lppt: isize) -> BOOL;
    fn LineTo(hdc: HDC, x: i32, y: i32) -> BOOL;
    fn SetTextColor(hdc: HDC, crColor: u32) -> u32;
    fn TextOutW(hdc: HDC, x: i32, y: i32, lpString: *const u16, c: i32) -> BOOL;
    fn SetPixel(hdc: HDC, x: i32, y: i32, crColor: u32) -> u32;
    fn SetBkMode(hdc: HDC, mode: i32) -> i32;
    fn CreateFontW(cHeight: i32, cWidth: i32, cEscapement: i32, cOrientation: i32, cWeight: i32, bItalic: u32, bUnderline: u32, bStrikeOut: u32, iCharSet: u32, iOutPrecision: u32, iClipPrecision: u32, iQuality: u32, iPitchAndFamily: u32, pszFaceName: *const u16) -> HFONT;
}

#[link(name="comctl32")] extern "system" { fn InitCommonControlsEx(picc: *const INITCOMMONCONTROLSEX) -> BOOL; }

fn rgb(r: u8, g: u8, b: u8) -> u32 { (r as u32) | ((g as u32) << 8) | ((b as u32) << 16) }

fn main() {
    unsafe {
        HINST = GetModuleHandleW(std::ptr::null());
        let cn = to_wide("AriaInstWnd");

        let wc = WNDCLASSEXW { cbSize: mem::size_of::<WNDCLASSEXW>() as u32, style: 0, lpfnWndProc: wndproc, cbClsExtra: 0, cbWndExtra: 0, hInstance: HINST, hIcon: 0, hCursor: LoadCursorW(0, IDC_ARROW), hbrBackground: GetSysColorBrush(COLOR_BTNFACE), lpszMenuName: std::ptr::null(), lpszClassName: cn.as_ptr(), hIconSm: 0 };
        RegisterClassExW(&wc);

        let title = to_wide(&format!("{} {} セットアップ", PRODUCT, VERSION));
        HWND_MAIN = CreateWindowExW(0, cn.as_ptr(), title.as_ptr(), WS_OVERLAPPEDWINDOW, 200, 150, 620, 460, 0, 0, HINST, 0);

        let icc = INITCOMMONCONTROLSEX { dwSize: 8, dwICC: 0x20 | 0x40 }; // progress + listview
        InitCommonControlsEx(&icc);

        // Detect source (look for engine/ subdirectory or use current directory)
        if let Ok(exe) = std::env::current_exe() {
            if let Some(dir) = exe.parent() {
                let engine_dir = dir.join("engine");
                SOURCE_DIR = if engine_dir.exists() {
                    engine_dir.to_string_lossy().to_string()
                } else {
                    dir.to_string_lossy().to_string()
                };
            }
        }

        // Default install path
        INSTALL_PATH = format!("C:\\Program Files\\{}\\{}", PUBLISHER, PRODUCT);

        ShowWindow(HWND_MAIN, SW_SHOW);
        let mut msg: MSG = mem::zeroed();
        while GetMessageW(&mut msg, 0, 0, 0) != 0 { TranslateMessage(&msg); DispatchMessageW(&msg); }
    }
}

extern "system" fn wndproc(hwnd: HWND, msg: u32, wp: WPARAM, lp: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            WM_CREATE => { create_controls(hwnd); 0 }

            WM_COMMAND => {
                match wp {
                    x if x == IDC_BROWSE as usize => {
                        let mut path_buf = vec![0u16; 260];
                        let title = to_wide("インストール先を選択");
                        let bi = BROWSEINFOW { hwndOwner: hwnd, pidlRoot: 0, pszDisplayName: path_buf.as_mut_ptr(), lpszTitle: title.as_ptr(), ulFlags: 1, lpfn: 0, lParam: 0, iImage: 0 };
                        let pidl = SHBrowseForFolderW(&bi);
                        if pidl != 0 && SHGetPathFromIDListW(pidl, path_buf.as_mut_ptr()) != 0 {
                            let end = path_buf.iter().position(|&c| c == 0).unwrap_or(260);
                            INSTALL_PATH = String::from_utf16_lossy(&path_buf[..end]);
                            set_text(HWND_PATH, &INSTALL_PATH);
                        }
                        0
                    }
                    x if x == IDC_INSTALL as usize && !INSTALLING => {
                        if INSTALL_PATH.is_empty() { return 0; }
                        INSTALLING = true;
                        EnableWindow(HWND_INSTALL, 0);
                        EnableWindow(HWND_BROWSE, 0);
                        set_text(HWND_CANCEL, "キャンセル");
                        start_install();
                        0
                    }
                    x if x == IDC_CANCEL as usize => {
                        if INSTALLING { INSTALLING = false; }
                        else { PostQuitMessage(0); }
                        0
                    }
                    _ => 0
                }
            }

            WM_TIMER => { if wp == TIMER_ANIM { ANIM_FRAME += 1; InvalidateRect(hwnd, std::ptr::null(), 1); } 0 }

            WM_PAINT => { let mut ps: PAINTSTRUCT = mem::zeroed(); let hdc = BeginPaint(hwnd, &mut ps); draw_animation(hdc); EndPaint(hwnd, &ps); 0 }

            WM_CTLCOLORSTATIC => {
                if lp == HWND_TITLE || lp == HWND_INFO {
                    SetBkMode(lp as HDC, TRANSPARENT);
                    SetTextColor(lp as HDC, rgb(0, 64, 128));
                    GetSysColorBrush(COLOR_BTNFACE) as LRESULT
                } else { DefWindowProcW(hwnd, msg, wp, lp) }
            }

            WM_DESTROY => { KillTimer(hwnd, TIMER_ANIM); PostQuitMessage(0); 0 }
            _ => DefWindowProcW(hwnd, msg, wp, lp)
        }
    }
}

unsafe fn create_controls(hwnd: HWND) {
    HFONT_BIG = CreateFontW(24, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 0, 0, std::ptr::null()); // bold
    HFONT_NORMAL = GetStockObject(DEFAULT_GUI_FONT);

    let sc = to_wide("STATIC"); let bc = to_wide("BUTTON"); let ec = to_wide("EDIT");
    let lc = to_wide("LISTBOX"); let pc = to_wide("msctls_progress32"); let e = to_wide("");

    // Header - white background area
    let pname = to_wide(&format!("{} セットアップ", PRODUCT));
    HWND_TITLE = CreateWindowExW(0, sc.as_ptr(), pname.as_ptr(), WS_CHILD | WS_VISIBLE, 20, 12, 560, 32, hwnd, IDC_TITLE as _, HINST, 0);
    SendMessageW(HWND_TITLE, WM_SETFONT, HFONT_BIG as _, 0);

    let desc = to_wide(&format!("{} v{} - {}", DESCRIPTION, VERSION, PUBLISHER));
    HWND_SUBTITLE = CreateWindowExW(0, sc.as_ptr(), desc.as_ptr(), WS_CHILD | WS_VISIBLE, 20, 44, 560, 18, hwnd, 0, HINST, 0);
    SendMessageW(HWND_SUBTITLE, WM_SETFONT, HFONT_NORMAL as _, 0);

    // Info text
    let info = to_wide(&format!("このウィザードは {} をコンピュータにインストールします。\n続行するには[インストール]をクリックしてください。", PRODUCT));
    HWND_INFO = CreateWindowExW(0, sc.as_ptr(), info.as_ptr(), WS_CHILD | WS_VISIBLE, 20, 72, 560, 36, hwnd, IDC_INFO as _, HINST, 0);
    SendMessageW(HWND_INFO, WM_SETFONT, HFONT_NORMAL as _, 0);

    // Install path label + field
    let path_label = to_wide("インストール先:");
    let _ = CreateWindowExW(0, sc.as_ptr(), path_label.as_ptr(), WS_CHILD | WS_VISIBLE, 20, 120, 80, 20, hwnd, 0, HINST, 0);
    let path_val = to_wide(&INSTALL_PATH);
    HWND_PATH = CreateWindowExW(WS_BORDER, ec.as_ptr(), path_val.as_ptr(), WS_CHILD | WS_VISIBLE | ES_LEFT | ES_AUTOHSCROLL, 100, 118, 380, 22, hwnd, IDC_PATH as _, HINST, 0);
    SendMessageW(HWND_PATH, WM_SETFONT, HFONT_NORMAL as _, 0);
    let browse_txt = to_wide("参照...");
    HWND_BROWSE = CreateWindowExW(0, bc.as_ptr(), browse_txt.as_ptr(), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, 490, 117, 80, 24, hwnd, IDC_BROWSE as _, HINST, 0);
    SendMessageW(HWND_BROWSE, WM_SETFONT, HFONT_NORMAL as _, 0);

    // Shortcut options
    let desktop_txt = to_wide("デスクトップにショートカットを作成する");
    HWND_DESKTOP_CB = CreateWindowExW(0, bc.as_ptr(), desktop_txt.as_ptr(),
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 100, 145, 300, 20, hwnd, IDC_DESKTOP_CB as _, HINST, 0);
    SendMessageW(HWND_DESKTOP_CB, WM_SETFONT, HFONT_NORMAL as _, 0);
    SendMessageW(HWND_DESKTOP_CB, 0x00F1, 1, 0); // BM_SETCHECK - checked by default

    let sm_txt = to_wide("スタートメニューに登録する");
    HWND_STARTMENU_CB = CreateWindowExW(0, bc.as_ptr(), sm_txt.as_ptr(),
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 100, 168, 300, 20, hwnd, IDC_STARTMENU_CB as _, HINST, 0);
    SendMessageW(HWND_STARTMENU_CB, WM_SETFONT, HFONT_NORMAL as _, 0);
    SendMessageW(HWND_STARTMENU_CB, 0x00F1, 1, 0); // BM_SETCHECK

    // Progress bar
    HWND_PROGRESS = CreateWindowExW(0, pc.as_ptr(), e.as_ptr(),
        WS_CHILD | WS_VISIBLE | PBS_SMOOTH as u32, 20, 196, 560, 22, hwnd, IDC_PROGRESS as _, HINST, 0);
    SendMessageW(HWND_PROGRESS, 0x403, 0, 100); // PBM_SETRANGE32 

    // Log listbox
    HWND_LOG = CreateWindowExW(0, lc.as_ptr(), e.as_ptr(),
        WS_CHILD | WS_VISIBLE | LBS_NOTIFY | WS_VSCROLL | WS_BORDER,
        20, 228, 560, 146, hwnd, IDC_LOG as _, HINST, 0);
    SendMessageW(HWND_LOG, WM_SETFONT, HFONT_NORMAL as _, 0);

    // Buttons
    let install_txt = to_wide("インストール");
    HWND_INSTALL = CreateWindowExW(0, bc.as_ptr(), install_txt.as_ptr(),
        WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON | WS_TABSTOP,
        410, 386, 100, 28, hwnd, IDC_INSTALL as _, HINST, 0);
    SendMessageW(HWND_INSTALL, WM_SETFONT, HFONT_NORMAL as _, 0);

    let cancel_txt = to_wide("キャンセル");
    HWND_CANCEL = CreateWindowExW(0, bc.as_ptr(), cancel_txt.as_ptr(), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON | WS_TABSTOP, 520, 386, 70, 28, hwnd, IDC_CANCEL as _, HINST, 0);
    SendMessageW(HWND_CANCEL, WM_SETFONT, HFONT_NORMAL as _, 0);

    SetFocus(HWND_INSTALL);
}

unsafe fn start_install() {
    let source = PathBuf::from(&SOURCE_DIR);
    let dest = PathBuf::from(&INSTALL_PATH);
    std::fs::create_dir_all(&dest).ok();

    let files: Vec<_> = walkdir::WalkDir::new(&source).into_iter()
        .filter_map(|e| e.ok()).filter(|e| e.file_type().is_file())
        .filter(|e| !e.file_name().to_string_lossy().contains("aria-installer"))
        .collect();

    TOTAL_FILES = files.len();
    COPIED_FILES = 0;
    LOG_LINES.clear();
    SendMessageW(HWND_LOG, 0x0184, 0, 0); // LB_RESETCONTENT

    log_line(&format!("インストール先: {}", INSTALL_PATH));
    log_line(&format!("コピー元: {}", SOURCE_DIR));
    log_line(&format!("ファイル数: {}", TOTAL_FILES));
    log_line("");

    SetTimer(HWND_MAIN, TIMER_ANIM, 40, 0);
    SendMessageW(HWND_PROGRESS, 0x406, 0, TOTAL_FILES as isize); // PBM_SETRANGE32

    for (i, entry) in files.iter().enumerate() {
        if !INSTALLING { break; }

        let rel = entry.path().strip_prefix(&source).unwrap();
        let dp = dest.join(rel);
        if let Some(p) = dp.parent() { std::fs::create_dir_all(p).ok(); }
        std::fs::copy(entry.path(), &dp).ok();

        CURRENT_FILE = rel.to_string_lossy().to_string();
        COPIED_FILES = i + 1;
        SendMessageW(HWND_PROGRESS, 0x402, COPIED_FILES, 0); // PBM_SETPOS
        log_line(&CURRENT_FILE);
    }

    KillTimer(HWND_MAIN, TIMER_ANIM);

    if INSTALLING {
        log_line("");
        log_line("インストールが完了しました。");

        // Create shortcuts
        let engine_exe = dest.join("engine").join("AriaEngine.exe");
        let engine_exe_str = engine_exe.to_string_lossy().to_string();
        let work_dir = dest.to_string_lossy().to_string();

        let create_desktop = SendMessageW(HWND_DESKTOP_CB, 0x00F0, 0, 0) != 0; // BM_GETCHECK
        let create_startmenu = SendMessageW(HWND_STARTMENU_CB, 0x00F0, 0, 0) != 0; // BM_GETCHECK

        if create_desktop {
            log_line("デスクトップショートカットを作成中...");
            let desktop = std::env::var("USERPROFILE").map(|p| format!("{}\\Desktop\\umikaze.lnk", p)).unwrap_or_default();
            create_shortcut(&desktop, &engine_exe_str, &work_dir);
        }
        if create_startmenu {
            log_line("スタートメニューに登録中...");
            let sm = std::env::var("APPDATA").map(|p| format!("{}\\Microsoft\\Windows\\Start Menu\\Programs\\{}", p, PUBLISHER)).unwrap_or_default();
            std::fs::create_dir_all(&sm).ok();
            let sm_lnk = format!("{}\\{}.lnk", sm, PRODUCT);
            create_shortcut(&sm_lnk, &engine_exe_str, &work_dir);
        }

        set_text(HWND_TITLE, &format!("{} - インストール完了", PRODUCT));
        set_text(HWND_INFO, &format!("{} のインストールが完了しました。\nデスクトップまたはスタートメニューから起動できます。", PRODUCT));
        set_text(HWND_INSTALL, "完了");
        set_text(HWND_CANCEL, "閉じる");
    } else {
        log_line("インストールが中断されました。");
        set_text(HWND_INSTALL, "再試行");
    }
    EnableWindow(HWND_INSTALL, 1);
    INSTALLING = false;
}

unsafe fn draw_animation(hdc: HDC) {
    if !INSTALLING { return; }

    let mut rect = RECT { left:0,top:0,right:0,bottom:0 };
    GetClientRect(HWND_MAIN, &mut rect);
    let sw = rect.right; let anim_y = 140; let lx = 30; let rx = sw - 130; let fw = 36; let fh = 26;

    let pen = CreatePen(PS_SOLID, 1, rgb(0, 64, 128));
    let old = SelectObject(hdc, pen);
    SetBkMode(hdc, TRANSPARENT);

    // Folders
    for &(x, label) in &[(lx, "ソース"), (rx, "インストール先")] {
        Rectangle(hdc, x, anim_y, x+fw, anim_y+fh);
        Rectangle(hdc, x, anim_y-4, x+16, anim_y);
        MoveToEx(hdc, x+16, anim_y-4, 0); LineTo(hdc, x+24, anim_y);
        SetTextColor(hdc, rgb(0,64,128));
        let w = to_wide(label);
        TextOutW(hdc, x+fw+6, anim_y+2, w.as_ptr(), label.len() as i32);
    }

    // Flying file
    let t = (ANIM_FRAME % 60) as f32 / 59.0;
    let fx = lx+fw+5 + (t*(rx-lx-fw-15) as f32) as i32;
    let fy = anim_y + (t*3.14159).sin() as i32 * 18;
    let br = CreateSolidBrush(rgb(255,200,0));
    SelectObject(hdc, GetStockObject(NULL_PEN)); SelectObject(hdc, br);
    Rectangle(hdc, fx, fy, fx+8, fy+12);
    DeleteObject(br);

    // Dotted path
    SelectObject(hdc, pen);
    for i in 0..14 {
        let tt = i as f32/13.0;
        let dx = lx+fw+5+(tt*(rx-lx-fw-13) as f32) as i32;
        let dy = anim_y+1+((tt*3.14159).sin()*18.0) as i32;
        SetPixel(hdc, dx, dy, rgb(160,160,160));
    }

    // File name
    SetTextColor(hdc, rgb(0,0,0));
    let d = if CURRENT_FILE.len()>45 { format!("...{}",&CURRENT_FILE[CURRENT_FILE.len()-42..]) } else { CURRENT_FILE.clone() };
    let w = to_wide(&d);
    TextOutW(hdc, sw/2-180, anim_y+30, w.as_ptr(), d.len() as i32);

    SelectObject(hdc, old); DeleteObject(pen);
}

fn create_shortcut(link_path: &str, target: &str, working_dir: &str) {
    let ps = format!(
        "$ws=New-Object -ComObject WScript.Shell;$s=$ws.CreateShortcut('{}');$s.TargetPath='{}';$s.WorkingDirectory='{}';$s.Save()",
        link_path.replace("'", "''"), target.replace("'", "''"), working_dir.replace("'", "''")
    );
    std::process::Command::new("powershell")
        .args(&["-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-Command", &ps])
        .output().ok();
}

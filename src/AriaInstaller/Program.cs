using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace AriaInstaller;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (!CheckDotNetRuntime())
        {
            MessageBoxW(0,
                ".NET 8.0 デスクトップ ランタイムが見つかりません。\n\n" +
                "OKを押すとダウンロードページを開きます。\n" +
                "インストール後、再度 AriaInstaller.exe を実行してください。\n\n" +
                "ダウンロード先:\n" +
                "https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0/runtime/desktop/x64",
                "Aria Installer - .NET ランタイムが必要です",
                0x40); // MB_ICONINFORMATION + MB_OK

            OpenUrl("https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0/runtime/desktop/x64");
            return;
        }

        string? installDir = ParseInstallDir(args);
        string sourceDir = AppDomain.CurrentDomain.BaseDirectory;

        var window = new NativeInstallerWindow("umikaze Installer", 500, 300, null);
        window.ModeText = "インストール準備中...";
        window.TargetText = $"インストール先: {installDir ?? "デフォルト"}";
        window.ProgressMax = 100;
        window.StatusText = "インストールを開始するにはボタンを押してください。";
        window.ButtonText = "インストール";

        Task.Run(() => RunInstallation(window, sourceDir, installDir));
        window.Run();
    }

    private static async Task RunInstallation(NativeInstallerWindow window, string sourceDir, string? installDir)
    {
        try
        {
            installDir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "umikaze");

            window.ButtonEnabled = false;
            window.ButtonText = "インストール中...";

            // Step 1: Create directory
            window.ModeText = "インストール中...";
            window.StatusText = "インストール先を作成しています...";
            window.ProgressValue = 5;
            Directory.CreateDirectory(installDir);
            await Task.Delay(200);

            // Step 2: Copy engine files
            window.StatusText = "エンジンファイルをコピーしています...";
            window.ProgressValue = 10;
            await Task.Delay(100);

            int fileCount = 0;
            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("AriaInstaller") && !f.EndsWith(".pdb"))
                .ToList();

            for (int i = 0; i < files.Count; i++)
            {
                string relative = Path.GetRelativePath(sourceDir, files[i]);
                string dest = Path.Combine(installDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(files[i], dest, overwrite: true);
                fileCount++;

                if (i % 10 == 0)
                {
                    window.ProgressValue = 10 + (int)(80 * i / files.Count);
                    window.StatusText = $"コピー中... {fileCount}/{files.Count} ファイル";
                    window.CurrentFile = Path.GetFileName(files[i]);
                    await Task.Delay(1);
                }
            }

            // Step 3: Create shortcut
            window.ProgressValue = 90;
            window.StatusText = "ショートカットを作成しています...";
            await Task.Delay(200);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktop, "umikaze.lnk");
            CreateShortcut(shortcutPath, Path.Combine(installDir, "AriaEngine.exe"), installDir);

            // Step 4: Done
            window.ProgressValue = 100;
            window.ModeText = "インストール完了";
            window.StatusText = $"umikaze を {installDir} にインストールしました。";
            window.ButtonText = "起動";
            window.ButtonEnabled = true;

            // Store install dir for launch
            _installedDir = installDir;
            _launchOnClick = true;
        }
        catch (Exception ex)
        {
            window.ModeText = "エラー";
            window.StatusText = $"インストールに失敗しました: {ex.Message}";
            window.ButtonText = "閉じる";
            window.ButtonEnabled = true;
        }
    }

    private static string? _installedDir;
    private static bool _launchOnClick;

    private static bool CheckDotNetRuntime()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output.Contains("Microsoft.NETCore.App 8.");
        }
        catch { return false; }
    }

    private static string? ParseInstallDir(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--install-dir", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Save();
        }
        catch { /* Non-critical */ }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    // Win32 MessageBox
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}

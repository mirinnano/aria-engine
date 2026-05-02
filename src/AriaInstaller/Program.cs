using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AriaInstaller;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (!CheckDotNetRuntime())
            {
                MessageBoxW(0,
                    ".NET 8.0 デスクトップ ランタイムが見つかりません。\n\n" +
                    "ダウンロードページを開きます。\n" +
                    "https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0/runtime/desktop/x64",
                    "Aria Installer", 0x40);
                OpenUrl("https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0/runtime/desktop/x64");
                return;
            }

            string? installDir = ParseInstallDir(args);
            string sourceDir = AppDomain.CurrentDomain.BaseDirectory;

            // Default: install to LocalAppData (no admin needed)
            installDir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "umikaze");

            var window = new NativeInstallerWindow("umikaze Installer", 500, 300, null);
            window.ModeText = "インストール準備中...";
            window.TargetText = $"インストール先: {installDir}";
            window.ProgressMax = 100;
            window.StatusText = "インストールを開始するにはボタンを押してください。";
            window.ButtonText = "インストール";

            Task.Run(() => RunInstallation(window, sourceDir, installDir));
            window.Run();
        }
        catch (Exception ex)
        {
            MessageBoxW(0, $"インストーラエラー:\n{ex}", "Aria Installer", 0x10);
        }
    }

    private static async Task RunInstallation(NativeInstallerWindow window, string sourceDir, string? installDir)
    {
        try
        {
            installDir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "umikaze");

            window.ButtonEnabled = false;
            window.ButtonText = "インストール中...";
            window.ProgressValue = 5;
            window.StatusText = "インストール先を作成しています...";
            Directory.CreateDirectory(installDir);

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("AriaInstaller") && !f.EndsWith(".pdb")).ToList();

            window.StatusText = $"コピー中... 0/{files.Count} ファイル";
            window.ProgressMax = files.Count > 0 ? files.Count : 1;

            for (int i = 0; i < files.Count; i++)
            {
                string relative = Path.GetRelativePath(sourceDir, files[i]);
                string dest = Path.Combine(installDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(files[i], dest, overwrite: true);

                window.ProgressValue = i + 1;
                window.CurrentFile = Path.GetFileName(files[i]);
                window.StatusText = $"コピー中... {i + 1}/{files.Count} ファイル";
                await Task.Delay(1);
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktop, "umikaze.lnk"),
                Path.Combine(installDir, "AriaEngine.exe"), installDir);

            window.StopAnimation();
            window.ProgressValue = files.Count;
            window.ModeText = "インストール完了";
            window.StatusText = $"umikaze を {installDir} にインストールしました。\nデスクトップにショートカットを作成しました。";
            window.ButtonText = "完了";
            window.ButtonEnabled = true;
        }
        catch (Exception ex)
        {
            window.StopAnimation();
            window.ModeText = "エラー";
            window.StatusText = $"インストールに失敗しました:\n{ex.Message}";
            window.ButtonText = "閉じる";
            window.ButtonEnabled = true;
        }
    }

    private static bool CheckDotNetRuntime()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet", Arguments = "--list-runtimes",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
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
            if (string.Equals(args[i], "--install-dir", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic sc = shell.CreateShortcut(shortcutPath);
            sc.TargetPath = targetPath;
            sc.WorkingDirectory = workingDir;
            sc.Save();
        }
        catch { }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}

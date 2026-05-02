using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AriaInstaller;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm(ParseInstallDir(args)));
    }

    private static string? ParseInstallDir(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--install-dir", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal sealed class InstallerForm : Form
{
    private readonly Label _modeLabel = new();
    private readonly Label _targetLabel = new();
    private readonly Label _sourceLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _primaryButton = new();
    private readonly Button _launchButton = new();
    private readonly Button _browseButton = new();
    private readonly TextBox _log = new();
    private readonly string _defaultInstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "ponkotusoft",
        "umikaze");
    private readonly string _bundledPatch = Path.Combine(AppContext.BaseDirectory, "update.patch");
    private readonly AppManifest? _manifest;
    private string _installDir;

    public InstallerForm(string? initialInstallDir = null)
    {
        _manifest = AppManifest.Load(Path.Combine(AppContext.BaseDirectory, "app", "manifest.json"));
        _installDir = string.IsNullOrWhiteSpace(initialInstallDir) ? _defaultInstallDir : initialInstallDir;
        Text = File.Exists(_bundledPatch) ? $"umikaze Update {ManifestVersionLabel}" : $"umikaze Installer {ManifestVersionLabel}";
        Width = 600;
        Height = 460;
        MinimumSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        
        // Dark Theme / Flat Design Styling
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(230, 230, 230);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        _modeLabel.Dock = DockStyle.Fill;
        _modeLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        _modeLabel.Text = File.Exists(_bundledPatch) ? $"umikaze Update {ManifestVersionLabel}" : $"umikaze Installer {ManifestVersionLabel}";
        _modeLabel.Margin = new Padding(0, 0, 0, 16);
        root.Controls.Add(_modeLabel, 0, 0);

        _sourceLabel.Dock = DockStyle.Fill;
        _sourceLabel.Text = ResolveSourceText();
        _sourceLabel.ForeColor = Color.FromArgb(180, 180, 180);
        _sourceLabel.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(_sourceLabel, 0, 1);

        var targetPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 16) };
        targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        _targetLabel.Dock = DockStyle.Fill;
        _targetLabel.Text = $"Install target: {_installDir}";
        _targetLabel.TextAlign = ContentAlignment.MiddleLeft;
        
        _browseButton.Text = "Browse";
        _browseButton.Dock = DockStyle.Fill;
        _browseButton.Height = 32;
        StyleFlatButton(_browseButton, Color.FromArgb(60, 60, 60), Color.FromArgb(80, 80, 80));
        _browseButton.Click += (_, _) => BrowseInstallDir();
        
        targetPanel.Controls.Add(_targetLabel, 0, 0);
        targetPanel.Controls.Add(_browseButton, 1, 0);
        root.Controls.Add(targetPanel, 0, 2);

        _progress.Dock = DockStyle.Fill;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Height = 8;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(_progress, 0, 3);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Ready";
        _statusLabel.ForeColor = Color.FromArgb(180, 180, 180);
        _statusLabel.Margin = new Padding(0, 0, 0, 16);
        root.Controls.Add(_statusLabel, 0, 4);

        _primaryButton.Dock = DockStyle.Top;
        _primaryButton.Height = 44;
        _primaryButton.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _primaryButton.Text = File.Exists(_bundledPatch) ? "Apply Update" : "Install umikaze";
        _primaryButton.Margin = new Padding(0, 0, 0, 8);
        StyleFlatButton(_primaryButton, Color.FromArgb(0, 120, 215), Color.FromArgb(0, 140, 240));
        _primaryButton.Click += async (_, _) => await RunPrimaryActionAsync();
        root.Controls.Add(_primaryButton, 0, 5);

        _launchButton.Dock = DockStyle.Top;
        _launchButton.Height = 44;
        _launchButton.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _launchButton.Text = "Launch umikaze";
        _launchButton.Margin = new Padding(0, 0, 0, 16);
        _launchButton.Enabled = File.Exists(Path.Combine(_installDir, "AriaEngine.exe"));
        StyleFlatButton(_launchButton, Color.FromArgb(30, 180, 100), Color.FromArgb(40, 200, 120));
        _launchButton.Click += (_, _) => LaunchGame();
        root.Controls.Add(_launchButton, 0, 6);

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BackColor = Color.FromArgb(20, 20, 20);
        _log.ForeColor = Color.FromArgb(180, 180, 180);
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.Font = new Font("Consolas", 9F);
        root.Controls.Add(_log, 0, 7);

        if (File.Exists(_bundledPatch))
        {
            SetStatus("Ready to apply update");
            Log("Ready to apply bundled update.patch.");
        }
        else if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "app")))
        {
            SetStatus("Ready to install");
            Log("Ready to install bundled app files.");
        }
        else
        {
            SetStatus("Payload missing");
            Log("No bundled app folder or update.patch was found.");
        }
        Log("Installation target defaults to Program Files.");
        Log("Administrator permission will be requested only when required.");
        if (_manifest != null)
        {
            Log($"Package version: {_manifest.Version}");
            Log($"Package files: {_manifest.FileCount}");
            if (_manifest.SigningRequested && !_manifest.Signed)
            {
                Log("Package signature status: requested but not fully valid on this machine.");
            }
        }
    }

    private void StyleFlatButton(Button btn, Color defaultColor, Color hoverColor)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = defaultColor;
        btn.ForeColor = Color.White;
        btn.Cursor = Cursors.Hand;
        
        btn.MouseEnter += (s, e) => { if(btn.Enabled) btn.BackColor = hoverColor; };
        btn.MouseLeave += (s, e) => { if(btn.Enabled) btn.BackColor = defaultColor; };
        btn.EnabledChanged += (s, e) => {
            if (!btn.Enabled) btn.BackColor = Color.FromArgb(50, 50, 50);
            else btn.BackColor = defaultColor;
        };
    }

    private string ResolveSourceText()
    {
        string driveRoot = Path.GetPathRoot(AppContext.BaseDirectory) ?? "";
        var drive = string.IsNullOrWhiteSpace(driveRoot) ? null : new DriveInfo(driveRoot);
        string media = drive?.DriveType == DriveType.CDRom ? "DVD media" : "download package";
        return $"Source: {media} / {ManifestVersionLabel} ({AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)})";
    }

    private async Task RunPrimaryActionAsync()
    {
        if (RequiresElevation(_installDir) && !Program.IsAdministrator())
        {
            SetStatus("Requesting administrator permission");
            RelaunchElevated();
            return;
        }

        SetInProgress(true);
        try
        {
            if (File.Exists(_bundledPatch))
            {
                await Task.Run(ApplyPatch);
            }
            else
            {
                await Task.Run(InstallApp);
                await Task.Run(CreateShortcuts);
            }

            _progress.Value = 100;
            _launchButton.Enabled = File.Exists(Path.Combine(_installDir, "AriaEngine.exe"));
            SetStatus("Completed");
            Log("Completed.");
        }
        catch (Exception ex)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            SetStatus("Failed");
            Log($"Failed: {ex.Message}");
            if (ex is UnauthorizedAccessException)
            {
                Log("Permission denied. Try running the installer as Administrator if installing to Program Files.");
            }
        }
        finally
        {
            SetInProgress(false);
        }
    }

    private void BrowseInstallDir()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _installDir };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installDir = dialog.SelectedPath;
            _targetLabel.Text = $"Install target: {_installDir}";
            _launchButton.Enabled = File.Exists(Path.Combine(_installDir, "AriaEngine.exe"));
        }
    }

    private static bool RequiresElevation(string path)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string[] protectedRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        };

        foreach (string root in protectedRoots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RelaunchElevated()
    {
        try
        {
            Log("Requesting administrator permission...");
            string escapedInstallDir = _installDir.Replace("\"", "\\\"");
            Process.Start(new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = $"--install-dir \"{escapedInstallDir}\"",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
            Close();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Log("Administrator permission was canceled.");
        }
        catch (Exception ex)
        {
            Log($"Failed to request administrator permission: {ex.Message}");
        }
    }

    private void InstallApp()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "app");
        if (!Directory.Exists(source))
        {
            Log("app folder was not found beside installer.");
            throw new DirectoryNotFoundException("Bundled app folder was not found beside installer.");
        }

        ReportProgress(20, "Copying files...");
        CopyDirectory(source, _installDir, overwrite: true);
        ReportProgress(90, "Finalizing install...");
        WriteInstallReceipt();
        Log($"Installed to {_installDir}");
    }

    private void ApplyPatch()
    {
        string engine = Path.Combine(_installDir, "AriaEngine.exe");
        string dataPak = Path.Combine(_installDir, "data.pak");
        string patch = _bundledPatch;
        string updated = dataPak + ".updated";
        string backup = dataPak + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";

        if (!File.Exists(engine)) throw new FileNotFoundException("AriaEngine.exe was not found in install directory.", engine);
        if (!File.Exists(dataPak)) throw new FileNotFoundException("data.pak was not found in install directory.", dataPak);
        if (!File.Exists(patch)) throw new FileNotFoundException("Patch file was not found.", patch);

        ReportProgress(20, "Applying patch...");
        RunEngineTool(engine, $"aria-pack apply --base \"{dataPak}\" --patch \"{patch}\" --out \"{updated}\"");
        ReportProgress(70, "Replacing data.pak...");
        File.Copy(dataPak, backup, overwrite: false);
        File.Move(updated, dataPak, overwrite: true);
        ReportProgress(90, "Finalizing update...");
        WriteInstallReceipt();
        Log($"Aria Update applied. Backup: {backup}");
    }

    private void CreateShortcuts()
    {
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null)
            {
                Log("WScript.Shell not available. Cannot create shortcuts.");
                return;
            }

            dynamic shell = Activator.CreateInstance(t)!;
            
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            string programsPath = Path.Combine(startMenuPath, "Programs", "ponkotusoft");
            Directory.CreateDirectory(programsPath);
            
            string enginePath = Path.Combine(_installDir, "AriaEngine.exe");
            string workingDir = _installDir;
            string args = "--run-mode release --pak data.pak --compiled scripts/scripts.ariac";
            
            // Desktop
            string deskLink = Path.Combine(desktopPath, "umikaze.lnk");
            dynamic desktopShortcut = shell.CreateShortcut(deskLink);
            desktopShortcut.TargetPath = enginePath;
            desktopShortcut.WorkingDirectory = workingDir;
            desktopShortcut.Arguments = args;
            desktopShortcut.Save();
            
            // Start Menu
            string startLink = Path.Combine(programsPath, "umikaze.lnk");
            dynamic startMenuShortcut = shell.CreateShortcut(startLink);
            startMenuShortcut.TargetPath = enginePath;
            startMenuShortcut.WorkingDirectory = workingDir;
            startMenuShortcut.Arguments = args;
            startMenuShortcut.Save();
            
            Log("Created shortcuts on Desktop and Start Menu.");
        }
        catch (Exception ex)
        {
            Log($"Failed to create shortcuts: {ex.Message}");
        }
    }

    private void RunEngineTool(string enginePath, string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = enginePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Failed to start AriaEngine.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (!string.IsNullOrWhiteSpace(output)) Log(output.Trim());
        if (!string.IsNullOrWhiteSpace(error)) Log(error.Trim());
        if (process.ExitCode != 0) throw new InvalidOperationException($"AriaEngine exited with code {process.ExitCode}.");
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (string sourcePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, sourcePath);
            string destinationPath = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDir);
            File.Copy(sourcePath, destinationPath, overwrite);
        }
    }

    private void WriteInstallReceipt()
    {
        string receiptPath = Path.Combine(_installDir, "install-info.txt");
        File.WriteAllText(
            receiptPath,
            $"Product=umikaze{Environment.NewLine}Company=ponkotusoft{Environment.NewLine}Version={_manifest?.Version ?? "unknown"}{Environment.NewLine}InstalledAt={DateTime.Now:O}{Environment.NewLine}Source={AppContext.BaseDirectory}{Environment.NewLine}InstalledFiles={_manifest?.FileCount ?? 0}{Environment.NewLine}PakEncrypted={_manifest?.PakEncrypted.ToString() ?? "unknown"}{Environment.NewLine}Signed={_manifest?.Signed.ToString() ?? "unknown"}{Environment.NewLine}");
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message));
            return;
        }

        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(status));
            return;
        }

        _statusLabel.Text = status;
    }

    private void ReportProgress(int value, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ReportProgress(value, message));
            return;
        }

        _progress.Value = Math.Clamp(value, _progress.Minimum, _progress.Maximum);
        SetStatus(message);
        Log(message);
    }

    private void SetInProgress(bool inProgress)
    {
        _primaryButton.Enabled = !inProgress;
        _browseButton.Enabled = !inProgress;
        _launchButton.Enabled = !inProgress && File.Exists(Path.Combine(_installDir, "AriaEngine.exe"));
        _progress.Style = inProgress ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        if (inProgress)
        {
            _progress.Value = 0;
            _primaryButton.Text = "In Progress";
            SetStatus("In Progress");
            Log("In Progress...");
        }
        else
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _primaryButton.Text = File.Exists(_bundledPatch) ? "Apply Update" : "Install umikaze";
        }
    }

    private void LaunchGame()
    {
        string engine = Path.Combine(_installDir, "AriaEngine.exe");
        if (!File.Exists(engine))
        {
            Log("AriaEngine.exe was not found in install target.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = engine,
            Arguments = "--run-mode release --pak data.pak --compiled scripts/scripts.ariac",
            WorkingDirectory = _installDir,
            UseShellExecute = true
        });
        Log("Launched umikaze.");
    }

    private string ManifestVersionLabel => string.IsNullOrWhiteSpace(_manifest?.Version) ? "dev" : _manifest.Version;
}

internal sealed class AppManifest
{
    public string Version { get; init; } = "";
    public int FileCount { get; init; }
    public bool PakEncrypted { get; init; }
    public bool SigningRequested { get; init; }
    public bool Signed { get; init; }

    public static AppManifest? Load(string path)
    {
        if (!File.Exists(path)) return null;

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        return new AppManifest
        {
            Version = root.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "",
            FileCount = root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array ? files.GetArrayLength() : 0,
            PakEncrypted = root.TryGetProperty("packaging", out var packaging) &&
                packaging.TryGetProperty("pakEncrypted", out var pakEncrypted) &&
                pakEncrypted.ValueKind == JsonValueKind.True,
            SigningRequested = root.TryGetProperty("signing", out var signing) &&
                signing.TryGetProperty("requested", out var requested) &&
                requested.ValueKind == JsonValueKind.True,
            Signed = root.TryGetProperty("signing", out signing) &&
                signing.TryGetProperty("signed", out var signed) &&
                signed.ValueKind == JsonValueKind.True
        };
    }
}

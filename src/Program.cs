using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DualDriveExplorer
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 2 && string.Equals(args[0], "--copy-url", StringComparison.OrdinalIgnoreCase))
            {
                UrlCommand.Run(args[1]);
                return;
            }

            if (args.Length >= 1 && string.Equals(args[0], "--connect-google", StringComparison.OrdinalIgnoreCase))
            {
                GoogleAuth.ConnectInteractive(args.Length >= 2 ? args[1] : null);
                return;
            }

            if (args.Length >= 2 && string.Equals(args[0], "--diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                Diagnostics.Write(args[1]);
                return;
            }

            if (args.Length >= 2 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                SelfTests.Write(args[1]);
                return;
            }

            if (args.Length >= 3 && string.Equals(args[0], "--resolve-url", StringComparison.OrdinalIgnoreCase))
            {
                UrlCommand.WriteResolvedUrl(args[1], args[2]);
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, "Local\\DualDriveExplorer.Controller", out created))
            {
                if (!created)
                {
                    return;
                }
                Application.Run(new TrayContext());
            }
        }
    }

    internal sealed class AppSettings
    {
        public string LeftPath { get; set; }
        public string RightPath { get; set; }
        public string GoogleDriveRoot { get; set; }
        public double SplitRatio { get; set; }
        public string ClientIdProtected { get; set; }
        public string ClientSecretProtected { get; set; }
        public string TokenProtected { get; set; }

        public AppSettings()
        {
            GoogleDriveRoot = GoogleDriveLocator.FindBestRoot(null);
            LeftPath = GoogleDriveRoot;
            RightPath = @"C:\";
            SplitRatio = 0.5;
        }
    }

    internal static class SettingsStore
    {
        internal static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DualDriveExplorer");
        internal static readonly string SettingsPath = Path.Combine(DirectoryPath, "settings.json");
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        internal static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var value = Json.Deserialize<AppSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                    if (value != null)
                    {
                        if (string.IsNullOrWhiteSpace(value.GoogleDriveRoot))
                            value.GoogleDriveRoot = GoogleDriveLocator.FindBestRoot(null);
                        if (string.IsNullOrWhiteSpace(value.LeftPath)) value.LeftPath = value.GoogleDriveRoot;
                        if (string.IsNullOrWhiteSpace(value.RightPath)) value.RightPath = @"C:\";
                        value.SplitRatio = SplitterLayout.ClampRatio(value.SplitRatio);
                        return value;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        internal static void Save(AppSettings value)
        {
            Directory.CreateDirectory(DirectoryPath);
            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, Json.Serialize(value), new UTF8Encoding(false));
            if (File.Exists(SettingsPath))
            {
                File.Delete(SettingsPath);
            }
            File.Move(temp, SettingsPath);
        }

        internal static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            byte[] raw = Encoding.UTF8.GetBytes(value);
            byte[] protectedBytes = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        internal static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
            }
            catch { return null; }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon tray;
        private readonly ExplorerPair explorerPair;

        internal TrayContext()
        {
            explorerPair = new ExplorerPair(SettingsStore.Load());
            var menu = new ContextMenuStrip();
            menu.Items.Add(MenuItem("Open / arrange explorers", delegate { explorerPair.OpenAndArrange(); }));
            menu.Items.Add(MenuItem("Save current paths", delegate { explorerPair.CaptureAndSave(); ShowBalloon("Paths saved."); }));
            menu.Items.Add(MenuItem("Select Google Drive location...", delegate { explorerPair.SelectGoogleDriveRoot(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("Connect Google account...", delegate { GoogleAuth.ConnectInteractive(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("Privacy policy", delegate { OpenWebPage("https://kaiedu.center/download/privacy"); }));
            menu.Items.Add(MenuItem("Exit", delegate { ExitApp(); }));

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "Dual Drive Explorer";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { explorerPair.OpenAndArrange(); };

            explorerPair.StartTracking();
            Application.Idle += FirstIdle;
        }

        private ToolStripMenuItem MenuItem(string text, EventHandler action)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += action;
            return item;
        }

        private static void OpenWebPage(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void FirstIdle(object sender, EventArgs e)
        {
            Application.Idle -= FirstIdle;
            explorerPair.OpenAndArrange();
        }

        private void ShowBalloon(string message)
        {
            tray.BalloonTipTitle = "Dual Drive Explorer";
            tray.BalloonTipText = message;
            tray.ShowBalloonTip(2000);
        }

        private void ExitApp()
        {
            explorerPair.CaptureAndSave();
            explorerPair.Dispose();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }

    internal sealed class ExplorerPair : IDisposable
    {
        private readonly AppSettings settings;
        private readonly System.Windows.Forms.Timer timer;
        private readonly SplitterOverlay splitter;
        private IntPtr leftHandle;
        private IntPtr rightHandle;
        private int arrangeRetries;
        private int saveTick;

        internal ExplorerPair(AppSettings value)
        {
            settings = value;
            settings.SplitRatio = SplitterLayout.ClampRatio(settings.SplitRatio);
            splitter = new SplitterOverlay(SetDividerFromScreenX, ResetDivider, SaveDividerRatio);
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 500;
            timer.Tick += delegate
            {
                if (arrangeRetries > 0)
                {
                    Arrange();
                    arrangeRetries--;
                }
                UpdateSplitter();
                saveTick++;
                if (saveTick >= 4)
                {
                    saveTick = 0;
                    CaptureAndSave();
                }
            };
        }

        internal void StartTracking() { timer.Start(); }

        internal void OpenAndArrange()
        {
            string googleRoot = ResolveGoogleDriveRoot(true);
            if (string.IsNullOrEmpty(googleRoot)) return;

            string left = GoogleDriveLocator.IsPathInside(settings.LeftPath, googleRoot) && Directory.Exists(settings.LeftPath)
                ? settings.LeftPath
                : googleRoot;
            string right = ExistingOrDefault(settings.RightPath, @"C:\");
            leftHandle = OpenExplorerWindow(left, IntPtr.Zero);
            rightHandle = OpenExplorerWindow(right, leftHandle);
            arrangeRetries = 5;
            Arrange();
            CaptureAndSave();
        }

        internal void SelectGoogleDriveRoot()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the Google Drive streaming location (the folder that contains My Drive).";
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                if (!string.IsNullOrWhiteSpace(settings.GoogleDriveRoot) && Directory.Exists(settings.GoogleDriveRoot))
                    dialog.SelectedPath = settings.GoogleDriveRoot;
                if (dialog.ShowDialog() != DialogResult.OK) return;
                string selected = GoogleDriveLocator.NormalizeRoot(dialog.SelectedPath);
                if (!GoogleDriveLocator.IsGoogleDriveRoot(selected, false))
                {
                    MessageBox.Show(
                        "The selected location is not a recognized Google Drive for desktop streaming location. Select the folder or drive that contains My Drive.",
                        "KAIEDU Explorer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ApplyGoogleDriveRoot(selected);
                SettingsStore.Save(settings);
                OpenAndArrange();
            }
        }

        private string ResolveGoogleDriveRoot(bool showMessage)
        {
            string detected = GoogleDriveLocator.FindBestRoot(settings.GoogleDriveRoot);
            if (string.IsNullOrEmpty(detected))
            {
                if (showMessage)
                {
                    MessageBox.Show(
                        "Google Drive for desktop is not ready. Install or start Google Drive, sign in, and then choose 'Open / arrange explorers'. If Drive is streamed to a folder, use 'Select Google Drive location...'.",
                        "KAIEDU Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return null;
            }
            ApplyGoogleDriveRoot(detected);
            return detected;
        }

        private void ApplyGoogleDriveRoot(string newRoot)
        {
            string oldRoot = settings.GoogleDriveRoot;
            if (!string.IsNullOrWhiteSpace(oldRoot) &&
                !string.Equals(GoogleDriveLocator.NormalizeRoot(oldRoot), newRoot, StringComparison.OrdinalIgnoreCase) &&
                GoogleDriveLocator.IsPathInside(settings.LeftPath, oldRoot))
            {
                string relative = settings.LeftPath.Substring(GoogleDriveLocator.NormalizeRoot(oldRoot).Length).TrimStart('\\');
                string rebased = Path.Combine(newRoot, relative);
                settings.LeftPath = Directory.Exists(rebased) ? rebased : newRoot;
            }
            else if (!GoogleDriveLocator.IsPathInside(settings.LeftPath, newRoot) || !Directory.Exists(settings.LeftPath))
            {
                settings.LeftPath = newRoot;
            }
            settings.GoogleDriveRoot = newRoot;
            SettingsStore.Save(settings);
        }

        private static string ExistingOrDefault(string requested, string fallback)
        {
            try { if (Directory.Exists(requested)) return requested; } catch { }
            return Directory.Exists(fallback) ? fallback : @"C:\";
        }

        private IntPtr OpenExplorerWindow(string path, IntPtr excluded)
        {
            var before = new HashSet<long>(GetExplorerWindows().Select(x => x.Handle.ToInt64()));
            var info = new ProcessStartInfo("explorer.exe", "/n,\"" + path + "\"");
            info.UseShellExecute = true;
            Process.Start(info);

            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(200);
                var windows = GetExplorerWindows();
                var created = windows.LastOrDefault(x => !before.Contains(x.Handle.ToInt64()) &&
                    x.Handle != excluded && PathsEqual(x.Path, path));
                if (created != null) return created.Handle;
            }
            var fallback = GetExplorerWindows().LastOrDefault(x => x.Handle != excluded && PathsEqual(x.Path, path));
            if (fallback != null) return fallback.Handle;
            return IntPtr.Zero;
        }

        private void Arrange()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            SplitterBounds layout = SplitterLayout.Calculate(area, settings.SplitRatio);
            if (leftHandle != IntPtr.Zero) Native.ShowWindow(leftHandle, Native.SW_RESTORE);
            if (rightHandle != IntPtr.Zero) Native.ShowWindow(rightHandle, Native.SW_RESTORE);

            IntPtr positions = Native.BeginDeferWindowPos(2);
            if (positions != IntPtr.Zero && leftHandle != IntPtr.Zero)
                positions = Native.DeferWindowPos(positions, leftHandle, IntPtr.Zero,
                    area.Left, area.Top, layout.LeftWidth, area.Height, Native.SWP_NOZORDER | Native.SWP_SHOWWINDOW);
            if (positions != IntPtr.Zero && rightHandle != IntPtr.Zero)
                positions = Native.DeferWindowPos(positions, rightHandle, IntPtr.Zero,
                    layout.DividerX, area.Top, layout.RightWidth, area.Height, Native.SWP_NOZORDER | Native.SWP_SHOWWINDOW);
            if (positions != IntPtr.Zero) Native.EndDeferWindowPos(positions);
            UpdateSplitter();
        }

        private void SetDividerFromScreenX(int screenX)
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            settings.SplitRatio = SplitterLayout.ClampRatio((double)(screenX - area.Left) / Math.Max(1, area.Width));
            Arrange();
        }

        private void ResetDivider()
        {
            settings.SplitRatio = 0.5;
            Arrange();
            SettingsStore.Save(settings);
        }

        internal void SaveDividerRatio()
        {
            SettingsStore.Save(settings);
        }

        private void UpdateSplitter()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            SplitterBounds layout = SplitterLayout.Calculate(area, settings.SplitRatio);
            bool visible = ArePairedWindowsVisible(layout, area) && IsPairActive();
            splitter.UpdateDisplay(visible, layout.DividerX, area.Top, area.Height);
        }

        private bool ArePairedWindowsVisible(SplitterBounds layout, Rectangle area)
        {
            if (leftHandle == IntPtr.Zero || rightHandle == IntPtr.Zero ||
                !Native.IsWindow(leftHandle) || !Native.IsWindow(rightHandle) ||
                !Native.IsWindowVisible(leftHandle) || !Native.IsWindowVisible(rightHandle) ||
                Native.IsIconic(leftHandle) || Native.IsIconic(rightHandle)) return false;

            Native.RECT leftRect;
            Native.RECT rightRect;
            if (!Native.GetWindowRect(leftHandle, out leftRect) || !Native.GetWindowRect(rightHandle, out rightRect)) return false;
            const int tolerance = 12;
            return Math.Abs(leftRect.Left - area.Left) <= tolerance &&
                   Math.Abs(leftRect.Top - area.Top) <= tolerance &&
                   Math.Abs(leftRect.Right - layout.DividerX) <= tolerance &&
                   Math.Abs(rightRect.Left - layout.DividerX) <= tolerance &&
                   Math.Abs(rightRect.Right - area.Right) <= tolerance &&
                   Math.Abs(rightRect.Bottom - area.Bottom) <= tolerance;
        }

        private bool IsPairActive()
        {
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            if (foreground == leftHandle || foreground == rightHandle || foreground == splitter.Handle) return true;
            IntPtr owner = Native.GetAncestor(foreground, Native.GA_ROOTOWNER);
            return owner == leftHandle || owner == rightHandle;
        }

        internal void CaptureAndSave()
        {
            bool changed = false;
            var windows = GetExplorerWindows();
            var left = windows.FirstOrDefault(x => x.Handle == leftHandle);
            var right = windows.FirstOrDefault(x => x.Handle == rightHandle);
            if (left != null && Directory.Exists(left.Path) &&
                GoogleDriveLocator.IsPathInside(left.Path, settings.GoogleDriveRoot) &&
                !PathsEqual(left.Path, settings.LeftPath))
            {
                settings.LeftPath = left.Path;
                changed = true;
            }
            if (right != null && Directory.Exists(right.Path) && !PathsEqual(right.Path, settings.RightPath))
            {
                settings.RightPath = right.Path;
                changed = true;
            }
            if (changed) SettingsStore.Save(settings);
        }

        private static bool PathsEqual(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static List<ExplorerWindow> GetExplorerWindows()
        {
            var result = new List<ExplorerWindow>();
            object shell = null;
            object windows = null;
            try
            {
                Type type = Type.GetTypeFromProgID("Shell.Application");
                shell = Activator.CreateInstance(type);
                windows = type.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
                int count = (int)windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null);
                for (int i = 0; i < count; i++)
                {
                    object window = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
                    if (window == null) continue;
                    try
                    {
                        string fullName = Convert.ToString(window.GetType().InvokeMember("FullName", System.Reflection.BindingFlags.GetProperty, null, window, null));
                        if (!fullName.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        int hwnd = Convert.ToInt32(window.GetType().InvokeMember("HWND", System.Reflection.BindingFlags.GetProperty, null, window, null));
                        string url = Convert.ToString(window.GetType().InvokeMember("LocationURL", System.Reflection.BindingFlags.GetProperty, null, window, null));
                        string path = UrlToPath(url);
                        if (!string.IsNullOrEmpty(path)) result.Add(new ExplorerWindow(new IntPtr(hwnd), path));
                    }
                    catch { }
                    finally { if (window != null && Marshal.IsComObject(window)) Marshal.FinalReleaseComObject(window); }
                }
            }
            catch { }
            finally
            {
                if (windows != null && Marshal.IsComObject(windows)) Marshal.FinalReleaseComObject(windows);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
            return result;
        }

        private static string UrlToPath(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var uri = new Uri(url);
                if (uri.IsFile) return Uri.UnescapeDataString(uri.LocalPath);
            }
            catch { }
            return null;
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
            splitter.Dispose();
        }
    }

    internal struct SplitterBounds
    {
        internal int DividerX;
        internal int LeftWidth;
        internal int RightWidth;
    }

    internal static class SplitterLayout
    {
        internal const double MinimumRatio = 0.30;
        internal const double MaximumRatio = 0.70;

        internal static double ClampRatio(double ratio)
        {
            if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0) return 0.5;
            return Math.Max(MinimumRatio, Math.Min(MaximumRatio, ratio));
        }

        internal static SplitterBounds Calculate(Rectangle area, double ratio)
        {
            double safeRatio = ClampRatio(ratio);
            int leftWidth = (int)Math.Round(area.Width * safeRatio, MidpointRounding.AwayFromZero);
            leftWidth = Math.Max(1, Math.Min(area.Width - 1, leftWidth));
            return new SplitterBounds
            {
                DividerX = area.Left + leftWidth,
                LeftWidth = leftWidth,
                RightWidth = area.Width - leftWidth
            };
        }
    }

    internal sealed class SplitterOverlay : Form
    {
        private const int GripWidth = 8;
        private readonly Action<int> moveDivider;
        private readonly Action resetDivider;
        private readonly Action saveDivider;
        private bool dragging;

        internal SplitterOverlay(Action<int> move, Action reset, Action save)
        {
            moveDivider = move;
            resetDivider = reset;
            saveDivider = save;
            Text = "KAIEDU Explorer Divider";
            AccessibleName = "KAIEDU Explorer resize divider";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(232, 236, 241);
            Cursor = Cursors.VSplit;
            Width = GripWidth;
            DoubleBuffered = true;
            MouseDown += OnGripMouseDown;
            MouseMove += OnGripMouseMove;
            MouseUp += OnGripMouseUp;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return parameters;
            }
        }

        internal void UpdateDisplay(bool shouldShow, int dividerX, int top, int height)
        {
            if (!shouldShow && !dragging)
            {
                if (Visible) Hide();
                return;
            }
            SetBounds(dividerX - GripWidth / 2, top, GripWidth, height);
            if (!Visible) Show();
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, Left, Top, Width, Height,
                Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int center = Width / 2;
            using (var line = new Pen(Color.FromArgb(118, 126, 138)))
                e.Graphics.DrawLine(line, center, 0, center, Height);

            int handleY = Math.Max(12, Height / 2 - 22);
            using (var brush = new SolidBrush(Color.FromArgb(76, 84, 96)))
            using (var font = new Font("Segoe UI Symbol", 11f, FontStyle.Bold, GraphicsUnit.Point))
            {
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("↔", font, brush, new RectangleF(0, handleY, Width, 44), format);
            }
        }

        private void OnGripMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks >= 2)
            {
                dragging = false;
                Capture = false;
                resetDivider();
                return;
            }
            dragging = true;
            Capture = true;
            moveDivider(Control.MousePosition.X);
        }

        private void OnGripMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging) moveDivider(Control.MousePosition.X);
        }

        private void OnGripMouseUp(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            Capture = false;
            saveDivider();
        }
    }

    internal sealed class ExplorerWindow
    {
        internal IntPtr Handle { get; private set; }
        internal string Path { get; private set; }
        internal ExplorerWindow(IntPtr handle, string path) { Handle = handle; Path = path; }
    }

    internal static class Native
    {
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const int SW_RESTORE = 9;
        internal const uint GA_ROOTOWNER = 3;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr BeginDeferWindowPos(int numberOfWindows);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr DeferWindowPos(IntPtr positionInfo, IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EndDeferWindowPos(IntPtr positionInfo);
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    }

    internal sealed class OAuthToken
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
        public string scope { get; set; }
        public long expires_at_utc { get; set; }
    }

    internal static class GoogleAuth
    {
        private const string Scope = "https://www.googleapis.com/auth/drive.metadata.readonly";
        private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        internal static bool IsConfigured(AppSettings settings)
        {
            return !string.IsNullOrEmpty(SettingsStore.Unprotect(settings.ClientIdProtected));
        }

        internal static void ConnectInteractive(string credentialPath = null)
        {
            try
            {
                string selectedPath = credentialPath;
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Title = "Select Google OAuth desktop client JSON";
                        dialog.Filter = "Google OAuth JSON (*.json)|*.json|All files (*.*)|*.*";
                        if (dialog.ShowDialog() != DialogResult.OK) return;
                        selectedPath = dialog.FileName;
                    }
                }

                var root = Json.DeserializeObject(File.ReadAllText(selectedPath, Encoding.UTF8)) as Dictionary<string, object>;
                Dictionary<string, object> installed = null;
                if (root != null && root.ContainsKey("installed")) installed = root["installed"] as Dictionary<string, object>;
                if (installed == null || !installed.ContainsKey("client_id"))
                    throw new InvalidOperationException("This is not a Google OAuth Desktop app credential JSON file.");

                string clientId = Convert.ToString(installed["client_id"]);
                string clientSecret = installed.ContainsKey("client_secret") ? Convert.ToString(installed["client_secret"]) : "";
                OAuthToken token = Authorize(clientId, clientSecret);
                var settings = SettingsStore.Load();
                settings.ClientIdProtected = SettingsStore.Protect(clientId);
                settings.ClientSecretProtected = SettingsStore.Protect(clientSecret);
                settings.TokenProtected = SettingsStore.Protect(Json.Serialize(token));
                SettingsStore.Save(settings);
                string errorLog = Path.Combine(SettingsStore.DirectoryPath, "oauth-error.log");
                if (File.Exists(errorLog)) File.Delete(errorLog);
                MessageBox.Show("Google Drive metadata access is connected.", "Dual Drive Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(SettingsStore.DirectoryPath);
                File.WriteAllText(Path.Combine(SettingsStore.DirectoryPath, "oauth-error.log"), ex.ToString(), new UTF8Encoding(false));
                MessageBox.Show("Google connection failed.\r\n\r\n" + ex.Message, "Dual Drive Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static OAuthToken Authorize(string clientId, string clientSecret)
        {
            int port;
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string redirect = "http://127.0.0.1:" + port + "/";
            string verifier = Base64Url(RandomBytes(48));
            string challenge;
            using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            string state = Base64Url(RandomBytes(24));

            string url = AuthEndpoint +
                "?client_id=" + Uri.EscapeDataString(clientId) +
                "&redirect_uri=" + Uri.EscapeDataString(redirect) +
                "&response_type=code" +
                "&scope=" + Uri.EscapeDataString(Scope) +
                "&access_type=offline&prompt=consent" +
                "&code_challenge=" + Uri.EscapeDataString(challenge) +
                "&code_challenge_method=S256" +
                "&state=" + Uri.EscapeDataString(state);

            var listener = new HttpListener();
            listener.Prefixes.Add(redirect);
            listener.Start();
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            var context = listener.GetContext();
            string returnedState = context.Request.QueryString["state"];
            string code = context.Request.QueryString["code"];
            string error = context.Request.QueryString["error"];
            byte[] response = Encoding.UTF8.GetBytes("<html><body><h2>Dual Drive Explorer</h2><p>You can close this window.</p></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.OutputStream.Write(response, 0, response.Length);
            context.Response.Close();
            listener.Stop();
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Google returned: " + error);
            if (returnedState != state || string.IsNullOrEmpty(code)) throw new InvalidOperationException("OAuth response validation failed.");

            var form = new Dictionary<string, string>();
            form["client_id"] = clientId;
            if (!string.IsNullOrEmpty(clientSecret)) form["client_secret"] = clientSecret;
            form["code"] = code;
            form["code_verifier"] = verifier;
            form["redirect_uri"] = redirect;
            form["grant_type"] = "authorization_code";
            using (var client = new HttpClient())
            {
                var httpResponse = client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form)).Result;
                string json = httpResponse.Content.ReadAsStringAsync().Result;
                if (!httpResponse.IsSuccessStatusCode) throw new InvalidOperationException("Token exchange failed: " + json);
                var token = Json.Deserialize<OAuthToken>(json);
                token.expires_at_utc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, token.expires_in - 120);
                return token;
            }
        }

        internal static string GetAccessToken(AppSettings settings)
        {
            string tokenJson = SettingsStore.Unprotect(settings.TokenProtected);
            string clientId = SettingsStore.Unprotect(settings.ClientIdProtected);
            string clientSecret = SettingsStore.Unprotect(settings.ClientSecretProtected) ?? "";
            if (string.IsNullOrEmpty(tokenJson) || string.IsNullOrEmpty(clientId)) return null;
            var token = Json.Deserialize<OAuthToken>(tokenJson);
            if (token != null && !string.IsNullOrEmpty(token.access_token) && token.expires_at_utc > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return token.access_token;
            if (token == null || string.IsNullOrEmpty(token.refresh_token)) return null;

            var form = new Dictionary<string, string>();
            form["client_id"] = clientId;
            if (!string.IsNullOrEmpty(clientSecret)) form["client_secret"] = clientSecret;
            form["refresh_token"] = token.refresh_token;
            form["grant_type"] = "refresh_token";
            using (var client = new HttpClient())
            {
                var response = client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form)).Result;
                string json = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode) return null;
                var refreshed = Json.Deserialize<OAuthToken>(json);
                refreshed.refresh_token = token.refresh_token;
                refreshed.expires_at_utc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, refreshed.expires_in - 120);
                settings.TokenProtected = SettingsStore.Protect(Json.Serialize(refreshed));
                SettingsStore.Save(settings);
                return refreshed.access_token;
            }
        }

        private static byte[] RandomBytes(int count) { var b = new byte[count]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b); return b; }
        private static string Base64Url(byte[] value) { return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    }

    internal sealed class DriveItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string mimeType { get; set; }
        public string webViewLink { get; set; }
        public string size { get; set; }
        public string md5Checksum { get; set; }
        public string modifiedTime { get; set; }
    }

    internal sealed class DriveList
    {
        public DriveItem[] files { get; set; }
    }

    internal sealed class GoogleDriveCandidate
    {
        internal string RootPath { get; set; }
        internal string VolumeLabel { get; set; }
        internal bool IsReady { get; set; }
        internal bool HasMyDrive { get; set; }
        internal bool HasDriveMarker { get; set; }
    }

    internal static class GoogleDriveLocator
    {
        private static readonly string[] MyDriveLabels = { "My Drive", "\uB0B4 \uB4DC\uB77C\uC774\uBE0C" };

        internal static string NormalizeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string full = Path.GetFullPath(path).TrimEnd('\\');
                return full.Length == 2 && full[1] == ':' ? full + "\\" : full;
            }
            catch { return null; }
        }

        internal static bool IsPathInside(string path, string root)
        {
            string fullPath = NormalizeRoot(path);
            string fullRoot = NormalizeRoot(root);
            if (fullPath == null || fullRoot == null) return false;
            if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = fullRoot.TrimEnd('\\') + "\\";
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsCandidateShape(GoogleDriveCandidate candidate, bool requireVolumeLabel)
        {
            if (candidate == null || !candidate.IsReady || !candidate.HasMyDrive || !candidate.HasDriveMarker) return false;
            return !requireVolumeLabel || string.Equals(candidate.VolumeLabel, "Google Drive", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGoogleDriveRoot(string root, bool requireVolumeLabel)
        {
            string normalized = NormalizeRoot(root);
            if (normalized == null || !Directory.Exists(normalized)) return false;
            var candidate = ReadCandidate(normalized);
            if (requireVolumeLabel)
            {
                try { candidate.VolumeLabel = new DriveInfo(Path.GetPathRoot(normalized)).VolumeLabel; }
                catch { candidate.VolumeLabel = null; }
            }
            return IsCandidateShape(candidate, requireVolumeLabel);
        }

        internal static List<string> FindRoots()
        {
            var result = new List<string>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    var candidate = ReadCandidate(drive.RootDirectory.FullName);
                    candidate.VolumeLabel = drive.VolumeLabel;
                    if (IsCandidateShape(candidate, true)) result.Add(NormalizeRoot(candidate.RootPath));
                }
                catch { }
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static string SelectBestRoot(IEnumerable<string> roots, string preferredRoot)
        {
            var normalized = roots.Select(NormalizeRoot).Where(x => x != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string preferred = NormalizeRoot(preferredRoot);
            if (preferred != null)
            {
                string match = normalized.FirstOrDefault(x => string.Equals(x, preferred, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            return normalized.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        internal static string FindBestRoot(string preferredRoot)
        {
            var roots = FindRoots();
            string preferred = NormalizeRoot(preferredRoot);
            if (preferred != null && IsGoogleDriveRoot(preferred, false) && !roots.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                roots.Add(preferred);
            return SelectBestRoot(roots, preferred);
        }

        internal static string FindRootForPath(string path, string preferredRoot)
        {
            var roots = FindRoots();
            string preferred = NormalizeRoot(preferredRoot);
            if (preferred != null && IsGoogleDriveRoot(preferred, false) && !roots.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                roots.Add(preferred);
            return roots.Where(root => IsPathInside(path, root)).OrderByDescending(root => root.Length).FirstOrDefault();
        }

        private static GoogleDriveCandidate ReadCandidate(string root)
        {
            string normalized = NormalizeRoot(root);
            bool hasMyDrive = MyDriveLabels.Any(label => Directory.Exists(Path.Combine(normalized, label)));
            bool hasMarker = Directory.Exists(Path.Combine(normalized, ".shortcut-targets-by-id")) ||
                             Directory.Exists(Path.Combine(normalized, ".Encrypted"));
            return new GoogleDriveCandidate
            {
                RootPath = normalized,
                IsReady = normalized != null && Directory.Exists(normalized),
                HasMyDrive = hasMyDrive,
                HasDriveMarker = hasMarker
            };
        }
    }

    internal static class DriveResolver
    {
        private const string FolderMime = "application/vnd.google-apps.folder";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        internal static DriveItem Resolve(string localPath, string accessToken, string configuredRoot)
        {
            string full = Path.GetFullPath(localPath).TrimEnd('\\');
            string root = GoogleDriveLocator.FindRootForPath(full, configuredRoot);
            if (string.IsNullOrEmpty(root))
                throw new InvalidOperationException("The selected item is not inside a recognized Google Drive for desktop location.");

            string relative = full.Substring(root.Length).Trim('\\');
            if (relative.Length == 0)
                return new DriveItem { id = "root", name = "Google Drive", mimeType = FolderMime, webViewLink = "https://drive.google.com/drive/my-drive" };

            string[] parts = relative.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int index = 0;
            string parent = "root";
            string driveId = null;
            if (parts.Length > 0 && IsMyDriveLabel(parts[0]))
            {
                index = 1;
            }
            else if (parts.Length > 0 && IsSharedDriveLabel(parts[0]))
            {
                if (parts.Length < 2) throw new InvalidOperationException("Select a folder or file inside a shared drive.");
                driveId = FindSharedDrive(parts[1], accessToken);
                parent = driveId;
                index = 2;
            }
            else
            {
                throw new InvalidOperationException("Only My Drive and Shared drives under the selected Google Drive location are supported.");
            }

            DriveItem current = null;
            for (int i = index; i < parts.Length; i++)
            {
                bool final = i == parts.Length - 1;
                var names = CandidateNames(parts[i]);
                var candidates = new List<DriveItem>();
                foreach (string name in names)
                {
                    candidates.AddRange(FindChildren(parent, name, driveId, accessToken));
                }
                if (!final) candidates = candidates.Where(x => x.mimeType == FolderMime).ToList();
                if (candidates.Count == 0) return null;
                if (candidates.Count > 1 && final && File.Exists(full))
                {
                    long length = new FileInfo(full).Length;
                    var sizeMatch = candidates.Where(x => x.size == length.ToString()).ToList();
                    if (sizeMatch.Count == 1) candidates = sizeMatch;
                }
                if (candidates.Count != 1)
                    throw new InvalidOperationException("More than one cloud item matches this path. The URL was not copied to avoid selecting the wrong file.");
                current = candidates[0];
                parent = current.id;
            }
            return current;
        }

        private static IEnumerable<string> CandidateNames(string localName)
        {
            yield return localName;
            string lower = localName.ToLowerInvariant();
            foreach (string ext in new[] { ".gdoc", ".gsheet", ".gslides", ".gdraw", ".gform" })
                if (lower.EndsWith(ext)) yield return localName.Substring(0, localName.Length - ext.Length);
        }

        private static List<DriveItem> FindChildren(string parent, string name, string driveId, string token)
        {
            string query = "'" + EscapeQuery(parent) + "' in parents and name = '" + EscapeQuery(name) + "' and trashed = false";
            var args = new List<string>();
            args.Add("q=" + Uri.EscapeDataString(query));
            args.Add("fields=" + Uri.EscapeDataString("files(id,name,mimeType,webViewLink,size,md5Checksum,modifiedTime)"));
            args.Add("pageSize=100");
            args.Add("supportsAllDrives=true");
            args.Add("includeItemsFromAllDrives=true");
            if (!string.IsNullOrEmpty(driveId))
            {
                args.Add("corpora=drive");
                args.Add("driveId=" + Uri.EscapeDataString(driveId));
            }
            string json = GetJson("https://www.googleapis.com/drive/v3/files?" + string.Join("&", args), token);
            var list = Json.Deserialize<DriveList>(json);
            return list != null && list.files != null ? list.files.ToList() : new List<DriveItem>();
        }

        private static string FindSharedDrive(string name, string token)
        {
            string json = GetJson("https://www.googleapis.com/drive/v3/drives?pageSize=100&fields=" + Uri.EscapeDataString("drives(id,name)"), token);
            var root = Json.DeserializeObject(json) as Dictionary<string, object>;
            var drives = root != null && root.ContainsKey("drives") ? root["drives"] as IEnumerable : null;
            var matches = new List<string>();
            if (drives != null)
            {
                foreach (object value in drives)
                {
                    var d = value as Dictionary<string, object>;
                    if (d != null && string.Equals(Convert.ToString(d["name"]), name, StringComparison.OrdinalIgnoreCase))
                        matches.Add(Convert.ToString(d["id"]));
                }
            }
            if (matches.Count != 1) throw new InvalidOperationException("The shared drive name could not be resolved uniquely.");
            return matches[0];
        }

        private static string GetJson(string url, string token)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = client.GetAsync(url).Result;
                string json = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google Drive API returned " + (int)response.StatusCode + ".");
                return json;
            }
        }

        private static string EscapeQuery(string value) { return value.Replace("\\", "\\\\").Replace("'", "\\'"); }
        private static bool IsMyDriveLabel(string value) { return value == "\uB0B4 \uB4DC\uB77C\uC774\uBE0C" || value.Equals("My Drive", StringComparison.OrdinalIgnoreCase); }
        private static bool IsSharedDriveLabel(string value) { return value == "\uACF5\uC720 \uB4DC\uB77C\uC774\uBE0C" || value.Equals("Shared drives", StringComparison.OrdinalIgnoreCase); }
    }

    internal static class UrlCommand
    {
        internal static void Run(string path)
        {
            try
            {
                Clipboard.SetText(ResolveUrl(path));
                MessageBox.Show("Google Drive URL copied to the clipboard.", "Dual Drive Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Copy Google Cloud URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        internal static void WriteResolvedUrl(string path, string outputPath)
        {
            try
            {
                File.WriteAllText(outputPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object>
                {
                    { "success", true }, { "url", ResolveUrl(path) }
                }), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                File.WriteAllText(outputPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object>
                {
                    { "success", false }, { "error", ex.Message }
                }), new UTF8Encoding(false));
                Environment.ExitCode = 1;
            }
        }

        private static string ResolveUrl(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("The selected item no longer exists.");
            var settings = SettingsStore.Load();
            if (!GoogleAuth.IsConfigured(settings))
                throw new InvalidOperationException("Google OAuth is not connected. Open Dual Drive Explorer from the tray and choose 'Connect Google account...'.");
            string token = GoogleAuth.GetAccessToken(settings);
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Google authorization expired. Reconnect the Google account from the tray menu.");

            DriveItem item = null;
            for (int attempt = 0; attempt < 6 && item == null; attempt++)
            {
                item = DriveResolver.Resolve(path, token, settings.GoogleDriveRoot);
                if (item == null) Thread.Sleep(2000);
            }
            if (item == null || string.IsNullOrEmpty(item.webViewLink))
                throw new InvalidOperationException("The item is not available in Google Drive yet. It may still be syncing.");
            return item.webViewLink;
        }
    }

    internal static class Diagnostics
    {
        internal static void Write(string outputPath)
        {
            try
            {
                var settings = SettingsStore.Load();
                var data = new Dictionary<string, object>();
                data["settingsPath"] = SettingsStore.SettingsPath;
                data["leftPath"] = settings.LeftPath;
                data["rightPath"] = settings.RightPath;
                data["splitRatio"] = settings.SplitRatio;
                data["googleConfigured"] = GoogleAuth.IsConfigured(settings);
                var googleRoots = GoogleDriveLocator.FindRoots();
                data["googleDriveRoots"] = googleRoots;
                data["googleDriveRoot"] = GoogleDriveLocator.SelectBestRoot(googleRoots, settings.GoogleDriveRoot);
                data["googleDriveReady"] = googleRoots.Count > 0 || GoogleDriveLocator.IsGoogleDriveRoot(settings.GoogleDriveRoot, false);
                data["contextFile"] = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\DualDriveExplorer.CopyGoogleUrl") != null;
                data["contextFolder"] = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\DualDriveExplorer.CopyGoogleUrl") != null;
                File.WriteAllText(outputPath, new JavaScriptSerializer().Serialize(data), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                File.WriteAllText(outputPath, "{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}");
            }
        }
    }

    internal static class SelfTests
    {
        internal static void Write(string outputPath)
        {
            var failures = new List<string>();
            Assert(!GoogleDriveLocator.IsCandidateShape(new GoogleDriveCandidate
            {
                RootPath = @"G:\", VolumeLabel = "DATA", IsReady = true, HasMyDrive = true, HasDriveMarker = true
            }, true), "A local G: volume must not be accepted merely because it has Drive-like folders.", failures);
            Assert(GoogleDriveLocator.IsCandidateShape(new GoogleDriveCandidate
            {
                RootPath = @"H:\", VolumeLabel = "Google Drive", IsReady = true, HasMyDrive = true, HasDriveMarker = true
            }, true), "A valid Google Drive volume on H: must be accepted.", failures);
            Assert(string.Equals(GoogleDriveLocator.SelectBestRoot(new[] { @"H:\" }, @"G:\"), @"H:\", StringComparison.OrdinalIgnoreCase),
                "When local G: conflicts, the detected H: Google Drive must be selected.", failures);
            Assert(string.Equals(GoogleDriveLocator.SelectBestRoot(new[] { @"H:\", @"I:\" }, @"I:\"), @"I:\", StringComparison.OrdinalIgnoreCase),
                "The saved preferred Google Drive must win when multiple roots exist.", failures);
            Assert(GoogleDriveLocator.SelectBestRoot(new string[0], null) == null,
                "No Google Drive installation must produce no selected root.", failures);
            Assert(GoogleDriveLocator.IsPathInside(@"H:\My Drive\file.txt", @"H:\"),
                "A file below the detected root must be recognized.", failures);
            Assert(!GoogleDriveLocator.IsPathInside(@"G:\local.txt", @"H:\"),
                "A local G: path must not be treated as an H: Google Drive path.", failures);

            var equal = SplitterLayout.Calculate(new Rectangle(0, 0, 1858, 1080), 0.5);
            Assert(equal.LeftWidth == 929 && equal.RightWidth == 929 && equal.DividerX == 929,
                "A 1858-pixel work area must split into two adjacent 929-pixel windows.", failures);
            Assert(Math.Abs(SplitterLayout.ClampRatio(0.1) - 0.30) < 0.0001 &&
                   Math.Abs(SplitterLayout.ClampRatio(0.9) - 0.70) < 0.0001,
                "The draggable divider must stay between 30% and 70%.", failures);
            var offset = SplitterLayout.Calculate(new Rectangle(100, 50, 1000, 700), 0.6);
            Assert(offset.DividerX == 700 && offset.LeftWidth == 600 && offset.RightWidth == 400,
                "The divider calculation must honor work-area offsets without a center gap.", failures);

            var result = new Dictionary<string, object>();
            result["passed"] = failures.Count == 0;
            result["failureCount"] = failures.Count;
            result["failures"] = failures;
            File.WriteAllText(outputPath, new JavaScriptSerializer().Serialize(result), new UTF8Encoding(false));
            if (failures.Count > 0) Environment.ExitCode = 1;
        }

        private static void Assert(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add(message);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class SetupLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool quiet = args != null && Array.Exists(args,
            value => string.Equals(value, "--quiet", StringComparison.OrdinalIgnoreCase));

        string packageRoot = AppDomain.CurrentDomain.BaseDirectory;
        string installer = Path.Combine(packageRoot, "installer", "Install-DualDriveExplorer.ps1");
        if (!File.Exists(installer))
        {
            if (!quiet)
                MessageBox.Show(
                    "설치 파일을 찾을 수 없습니다. ZIP의 전체 내용을 한 폴더에 압축 해제한 뒤 다시 실행해 주세요.\r\n\r\n" + installer,
                    "KAIEDU Explorer 설치", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        try
        {
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"System32\WindowsPowerShell\v1.0\powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = powershell,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + installer + "\"",
                WorkingDirectory = Path.GetDirectoryName(installer),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    if (!quiet)
                        MessageBox.Show(
                            "설치가 완료되었습니다.\r\n\r\n폴더가 현재 탐색기 창 안에서 열리도록 Windows 탐색기 옵션도 조정했습니다.\r\n작업표시줄 알림 영역에서 KAIEDU Explorer를 확인해 주세요.",
                            "KAIEDU Explorer 설치", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                string details = string.IsNullOrWhiteSpace(error) ? output : error;
                string logPath = WriteErrorLog(packageRoot, details);
                if (!quiet)
                    MessageBox.Show(
                        "설치를 완료하지 못했습니다.\r\n\r\n오류 기록: " + logPath,
                        "KAIEDU Explorer 설치", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return process.ExitCode == 0 ? 1 : process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            string logPath = WriteErrorLog(packageRoot, ex.ToString());
            if (!quiet)
                MessageBox.Show(
                    "설치 프로그램을 실행하지 못했습니다.\r\n\r\n오류 기록: " + logPath,
                    "KAIEDU Explorer 설치", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string WriteErrorLog(string packageRoot, string details)
    {
        string path = Path.Combine(packageRoot, "KAIEDU-Explorer-Setup-error.log");
        try
        {
            File.WriteAllText(path, details ?? "Unknown setup error", new UTF8Encoding(false));
        }
        catch
        {
            path = Path.Combine(Path.GetTempPath(), "KAIEDU-Explorer-Setup-error.log");
            File.WriteAllText(path, details ?? "Unknown setup error", new UTF8Encoding(false));
        }
        return path;
    }
}

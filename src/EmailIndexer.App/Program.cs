using System;
using System.IO;
using System.Windows.Forms;
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--scan-test")
                return ScanTest.Run(args[1]);
            if (args.Length == 2 && args[0] == "--index-test")
                return ScanTest.RunIndex(args[1]);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (_, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) ReportCrash(ex, fatal: true); };

            if (args.Length == 1 && args[0] == "--env")
            {
                Application.Run(new EnvCheckForm());
                return 0;
            }

            // exe에 폴더를 끌어다 놓거나 "EmailIndexer.exe 폴더"로 실행하면 그 폴더를 연다
            string? folder = args.Length == 1 && Directory.Exists(args[0]) ? args[0] : null;
            Application.Run(new MainForm(AppSettings.Load(), folder));
            return 0;
        }

        /// <summary>예상 못 한 오류를 %APPDATA%\EmailIndexer\error.log에 남기고 알린다.</summary>
        private static void ReportCrash(Exception ex, bool fatal = false)
        {
            var log = Path.Combine(Path.GetDirectoryName(AppSettings.DefaultPath)!, "error.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }
            if (fatal) return;
            try
            {
                MessageBox.Show($"예상하지 못한 오류가 발생했습니다.\n{ex.Message}\n\n" +
                                "방금 하던 작업을 다시 시도해 보고, 계속되면 프로그램을 껐다 다시 켜세요. 메일 파일에는 영향이 없습니다.\n" +
                                $"같은 문제가 반복되면 이 기록 파일을 담당자에게 보내 주세요:\n{log}", "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}

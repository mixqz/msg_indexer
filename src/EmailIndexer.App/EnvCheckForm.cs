using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using EmailIndexer.Core;
using Microsoft.Win32;

namespace EmailIndexer.App
{
    /// <summary>
    /// Stage 2 빌드 검증용 창: 회사 PC에서 무설치·일반 권한 실행 여부와
    /// 이후 기능에 필요한 환경(.NET 4.8, Outlook classic)을 한 화면에 보여준다.
    /// </summary>
    internal sealed class EnvCheckForm : Form
    {
        private readonly TextBox _report;

        public EnvCheckForm()
        {
            Text = $"{AppInfo.Name} {AppInfo.Version} — 실행 환경 점검";
            Size = new Size(720, 460);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Malgun Gothic", 10F);
            Icon = Ui.AppIcon ?? Icon;

            var title = new Label
            {
                Text = "✅ 앱이 정상 실행되었습니다. 아래 내용을 [정보 복사]로 전달해 주세요.",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };

            _report = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10F),
                Text = BuildReport(),
            };

            var copy = new Button { Text = "정보 복사", Width = 120, Height = 32 };
            copy.Click += (_, __) =>
            {
                Clipboard.SetText(_report.Text);
                copy.Text = "복사됨 ✓";
            };
            var close = new Button { Text = "닫기", Width = 90, Height = 32 };
            close.Click += (_, __) => Close();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6),
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(copy);

            Controls.Add(_report);
            Controls.Add(buttons);
            Controls.Add(title);
            Theme.Apply(this);
        }

        private static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"앱 버전      : {AppInfo.Version}");
            sb.AppendLine($"Windows      : {Safe(GetWindowsVersion)}");
            sb.AppendLine($"64비트 OS    : {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($".NET 런타임  : {Safe(GetNetFrameworkVersion)}");
            sb.AppendLine($"권한         : {Safe(() => IsAdmin() ? "관리자 (주의: Outlook 연동 시 문제 가능)" : "일반 사용자 ✓")}");
            sb.AppendLine($"Outlook      : {Safe(GetOutlookState)}");
            sb.AppendLine($"실행 위치    : {Application.ExecutablePath}");
            sb.AppendLine($"화면 배율    : {Safe(() => $"{DpiPercent()}%")}");
            return sb.ToString();
        }

        private static string Safe(Func<string> f)
        {
            try { return f(); }
            catch (Exception ex) { return "확인 실패: " + ex.Message; }
        }

        private static string GetWindowsVersion()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string ?? "?";
            var display = key?.GetValue("DisplayVersion") as string ?? "";
            var build = key?.GetValue("CurrentBuild") as string ?? "";
            // Windows 11도 ProductName이 "Windows 10"으로 남아 있어 빌드 번호로 보정
            if (int.TryParse(build, out var b) && b >= 22000)
                product = product.Replace("Windows 10", "Windows 11");
            return $"{product} {display} (빌드 {build})";
        }

        private static string GetNetFrameworkVersion()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            var release = key?.GetValue("Release") is int r ? r : 0;
            var ver = release >= 533320 ? "4.8.1" : release >= 528040 ? "4.8" : "4.8 미만 (지원 안 됨)";
            return $"{ver} (release {release})";
        }

        private static bool IsAdmin()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static string GetOutlookState()
        {
            bool classic = Process.GetProcessesByName("OUTLOOK").Any();
            bool newOutlook = Process.GetProcessesByName("olk").Any();
            if (classic) return "Outlook(classic) 실행 중 ✓";
            if (newOutlook) return "새 Outlook만 실행 중 (자동 백업 불가)";
            return "실행 중 아님";
        }

        private static int DpiPercent()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return (int)Math.Round(g.DpiX / 96f * 100);
        }
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using EmailIndexer.Core;
using EmailIndexer.Core.Text;
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
            Text = L.F("outlook.env.title", AppInfo.Name, AppInfo.Version);
            Size = new Size(720, 460);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font(Ui.Base.FontFamily, 10F);
            Icon = Ui.AppIcon ?? Icon;

            var title = new Label
            {
                Text = L.T("outlook.env.heading"),
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

            var copy = new Button { Text = L.T("outlook.env.copy"), Width = 120, Height = 32 };
            copy.Click += (_, __) =>
            {
                Clipboard.SetText(_report.Text);
                copy.Text = L.T("outlook.env.copied");
            };
            var close = new Button { Text = L.T("outlook.env.close"), Width = 90, Height = 32 };
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
            sb.AppendLine($"App version   : {AppInfo.Version}");
            sb.AppendLine($"Windows       : {Safe(GetWindowsVersion)}");
            sb.AppendLine($"64-bit OS     : {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($".NET runtime  : {Safe(GetNetFrameworkVersion)}");
            sb.AppendLine($"Permissions   : {Safe(() => IsAdmin() ? "Administrator (note: may cause problems with Outlook integration)" : "Standard user \u2713")}");
            sb.AppendLine($"Outlook       : {Safe(GetOutlookState)}");
            sb.AppendLine($"Run location  : {Application.ExecutablePath}");
            sb.AppendLine($"Display scale : {Safe(() => $"{DpiPercent()}%")}");
            return sb.ToString();
        }

        private static string Safe(Func<string> f)
        {
            try { return f(); }
            catch (Exception ex) { return "check failed: " + ex.Message; }
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
            return $"{product} {display} (build {build})";
        }

        private static string GetNetFrameworkVersion()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            var release = key?.GetValue("Release") is int r ? r : 0;
            var ver = release >= 533320 ? "4.8.1" : release >= 528040 ? "4.8" : "below 4.8 (not supported)";
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
            if (classic) return "Outlook (classic) running \u2713";
            if (newOutlook) return "Only new Outlook running (automatic backup unavailable)";
            return "Not running";
        }

        private static int DpiPercent()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return (int)Math.Round(g.DpiX / 96f * 100);
        }
    }
}

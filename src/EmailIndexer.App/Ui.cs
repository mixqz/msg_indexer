using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EmailIndexer.Core.Text;

namespace EmailIndexer.App
{
    /// <summary>화면 공통 도우미.</summary>
    internal static class Ui
    {
        public static readonly Font Base = CreateFont(9F);
        public static readonly Font Bold = new Font(Base, FontStyle.Bold);

        /// <summary>
        /// 언어별 기본 글꼴 (Windows 기본 탑재). 한국어 Malgun Gothic, 일본어 Yu Gothic UI,
        /// 중국어 Microsoft YaHei UI, 영어·스페인어·프랑스어 Segoe UI. 없으면 시스템 글꼴.
        /// </summary>
        private static Font CreateFont(float size)
        {
            string[] prefs;
            switch (EmailIndexer.Core.Text.L.Language)
            {
                case "ko": prefs = new[] { "Malgun Gothic" }; break;
                case "ja": prefs = new[] { "Yu Gothic UI", "Meiryo UI" }; break;
                case "zh": prefs = new[] { "Microsoft YaHei UI", "Microsoft YaHei" }; break;
                default: prefs = new[] { "Segoe UI" }; break;
            }
            // 한글 이름·제목이 섞인 메일도 많아, 서양 언어에서도 대체 글꼴로 Malgun Gothic을 둔다
            foreach (var name in prefs.Concat(new[] { "Malgun Gothic", "NanumGothic" }))
            {
                var f = new Font(name, size);
                if (f.Name == name) return f;
                f.Dispose();
            }
            return new Font(SystemFonts.MessageBoxFont.FontFamily, size);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>입력칸 안내 문구 (Windows 기본 기능, 실패해도 무시).</summary>
        public static void SetCue(TextBox box, string text)
        {
            try
            {
                if (box.IsHandleCreated) SendMessage(box.Handle, 0x1501, (IntPtr)1, text);
                else box.HandleCreated += (_, __) => SetCue(box, text);
            }
            catch { /* Windows가 아니면 무시 */ }
        }

        /// <summary>
        /// 모든 버튼 공통: 높이 28px로 통일, 글자가 길면 넓어지되 최소 너비 보장
        /// (고정 너비/자동 크기 버튼이 섞여 한 줄의 높이가 달라지던 문제 해결).
        /// </summary>
        public static Button Btn(string text, EventHandler onClick, int width = 0)
        {
            var b = new Button
            {
                Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(width > 0 ? width : 72, 28), Height = 28,
                Margin = new Padding(3, 2, 3, 2), Padding = new Padding(6, 0, 6, 0), UseVisualStyleBackColor = true,
            };
            b.Click += onClick;
            return b;
        }

        // Windows 11 기본 강조색 계열 (흰 글자 대비: 기본 6.3:1, 마우스 올림 5.2:1, 누름 8.0:1)
        private static readonly Color Accent = Color.FromArgb(0, 95, 184);
        private static readonly Color AccentHover = Color.FromArgb(26, 110, 191);
        private static readonly Color AccentPressed = Color.FromArgb(0, 76, 147);
        private static readonly Color AccentFocus = Color.FromArgb(0, 40, 80);

        /// <summary>
        /// 화면의 주 동작 버튼 1개에만 강조색 채우기. 키보드 포커스는 두꺼운 진한 테두리,
        /// 사용 불가 상태는 회색으로 바꿔 파란 바탕 위 흐린 글자가 되지 않게 한다.
        /// </summary>
        public static Button Primary(Button b)
        {
            b.Tag = Theme.PrimaryTag; // 테마가 보조 버튼 모양으로 덮어쓰지 않게
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.FlatStyle = FlatStyle.Flat;
            b.Font = Bold;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.FlatAppearance.MouseDownBackColor = AccentPressed;
            void Paint()
            {
                bool focused = b.Focused && b.Enabled;
                b.BackColor = b.Enabled ? Accent : SystemColors.ControlLight;
                b.ForeColor = b.Enabled ? Color.White : SystemColors.GrayText;
                b.FlatAppearance.BorderColor = !b.Enabled ? SystemColors.ControlDark : focused ? AccentFocus : Accent;
                b.FlatAppearance.BorderSize = focused ? 2 : 1;
            }
            b.GotFocus += (_, __) => Paint();
            b.LostFocus += (_, __) => Paint();
            b.EnabledChanged += (_, __) => Paint();
            Paint();
            return b;
        }

        /// <summary>exe에 들어 있는 앱 아이콘 (창 제목줄·작업 표시줄용). 못 읽으면 기본 아이콘.</summary>
        public static readonly Icon? AppIcon = LoadAppIcon();

        private static Icon? LoadAppIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }

        /// <summary>기본 연결 프로그램(Outlook)으로 열기.</summary>
        public static void OpenFile(IWin32Window owner, string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, L.F("dlg.ui.open.failedMessage", path, ex.Message),
                    L.T("dlg.ui.open.failedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static void ShowInFolder(string path)
        {
            try { Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        }

        public static void Error(IWin32Window owner, string title, Exception ex)
            => MessageBox.Show(owner, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        /// <summary>
        /// 되돌리기 어려운 작업 확인: [동작 이름] / [취소] 버튼 (예/아니요 대신 결과를 버튼에 적는다).
        /// 기본 선택은 [취소] — Enter를 실수로 눌러도 실행되지 않는다.
        /// </summary>
        public static bool Confirm(IWin32Window owner, string title, string message, string okText, bool destructive = false)
        {
            using var f = new Form
            {
                Text = title, Font = Base, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16, 14, 16, 10),
            };
            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
            var icon = new PictureBox
            {
                Image = (destructive ? SystemIcons.Warning : SystemIcons.Question).ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(0, 0, 12, 0),
            };
            var text = new Label { Text = message, AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(0, 4, 0, 14) };
            var bar = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0) };
            var cancel = new Button { Text = L.T("dlg.ui.confirm.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 28) };
            var ok = new Button { Text = okText, DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 28) };
            bar.Controls.Add(cancel);
            bar.Controls.Add(ok);
            layout.Controls.Add(icon, 0, 0);
            layout.Controls.Add(text, 1, 0);
            layout.Controls.Add(bar, 0, 1);
            layout.SetColumnSpan(bar, 2);
            f.Controls.Add(layout);
            f.CancelButton = cancel;
            Theme.Apply(f);
            f.Shown += (_, __) => cancel.Focus();
            return f.ShowDialog(owner) == DialogResult.OK;
        }
    }

    /// <summary>깜빡임 없는 ListView.</summary>
    internal sealed class BufferedListView : ListView
    {
        public BufferedListView() { DoubleBuffered = true; }
    }
}

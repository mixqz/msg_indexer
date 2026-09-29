using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    /// <summary>설정: '내 이메일 주소' (보낸 메일 판정용).</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly TextBox _addresses;

        public SettingsForm(AppSettings settings)
        {
            Text = "설정";
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;

            var help = new Label
            {
                Dock = DockStyle.Top, Height = 64, Padding = new Padding(10, 10, 10, 0),
                Text = "내 이메일 주소 (한 줄에 하나)\n발신자가 이 주소인 메일은 '보낸 메일'로 보고, 파일명·정렬에 발신 시각을 씁니다.\nOutlook 백업을 실행하면 계정 주소가 자동으로 추가됩니다.",
            };
            _addresses = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
                Text = string.Join(Environment.NewLine, settings.MyAddresses),
            };
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            body.Controls.Add(_addresses);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var cancel = Ui.Btn("취소", (_, __) => Close(), 90);
            cancel.DialogResult = DialogResult.Cancel;
            var ok = Ui.Btn("저장", (_, __) =>
            {
                settings.MyAddresses = _addresses.Lines.Select(l => l.Trim()).Where(l => l.Contains("@"))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                DialogResult = DialogResult.OK;
                Close();
            }, 90);
            var env = Ui.Btn("실행 환경 점검", (_, __) => { using var f = new EnvCheckForm(); f.ShowDialog(this); });
            bar.Controls.AddRange(new Control[] { cancel, ok, env });
            AcceptButton = null;
            CancelButton = cancel;

            Controls.Add(body);
            Controls.Add(bar);
            Controls.Add(help);
            Theme.Apply(this);
        }
    }
}

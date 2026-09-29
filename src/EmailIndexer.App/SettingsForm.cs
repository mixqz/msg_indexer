using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EmailIndexer.Core.Text;
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    /// <summary>설정: '내 이메일 주소' (보낸 메일 판정용).</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly TextBox _addresses;
        private readonly ComboBox _language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, AccessibleName = "Language" };

        /// <summary>저장 시 언어가 바뀌었는지 (다시 시작 안내용).</summary>
        public bool LanguageChanged { get; private set; }

        public SettingsForm(AppSettings settings)
        {
            Text = L.T("dlg.settings.title");
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(520, 460);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;

            var help = new Label
            {
                Dock = DockStyle.Top, Height = 64, Padding = new Padding(10, 10, 10, 0),
                Text = L.T("dlg.settings.help"),
            };
            _addresses = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
                Text = string.Join(Environment.NewLine, settings.MyAddresses),
            };
            // 언어: 각 언어를 그 언어의 이름으로 표시 (현재 언어를 못 읽어도 찾을 수 있게)
            foreach (var (code, name) in L.Languages) _language.Items.Add(new LangItem(code, name));
            _language.SelectedIndex = Math.Max(0, L.Languages.ToList().FindIndex(l => l.Code == L.Normalize(settings.Language)));
            var langRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 12, 10, 0), WrapContents = false };
            langRow.Controls.Add(new Label { Text = "Language / 언어", AutoSize = true, Padding = new Padding(0, 5, 8, 0) });
            langRow.Controls.Add(_language);
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            body.Controls.Add(_addresses);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var cancel = Ui.Btn(L.T("dlg.settings.cancel"), (_, __) => Close(), 90);
            cancel.DialogResult = DialogResult.Cancel;
            var ok = Ui.Btn(L.T("dlg.settings.save"), (_, __) =>
            {
                settings.MyAddresses = _addresses.Lines.Select(l => l.Trim()).Where(l => l.Contains("@"))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var lang = ((LangItem)_language.SelectedItem).Code;
                LanguageChanged = lang != L.Normalize(settings.Language);
                settings.Language = lang;
                DialogResult = DialogResult.OK;
                Close();
            }, 90);
            var env = Ui.Btn(L.T("dlg.settings.envCheck"), (_, __) => { using var f = new EnvCheckForm(); f.ShowDialog(this); });
            bar.Controls.AddRange(new Control[] { cancel, ok, env });
            AcceptButton = null;
            CancelButton = cancel;

            Controls.Add(body);
            Controls.Add(bar);
            Controls.Add(help);
            Controls.Add(langRow);
            Theme.Apply(this);
        }

        private sealed class LangItem
        {
            public string Code { get; }
            private readonly string _name;
            public LangItem(string code, string name) { Code = code; _name = name; }
            public override string ToString() => _name;
        }
    }
}

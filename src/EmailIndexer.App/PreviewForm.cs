using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using EmailIndexer.Core.Text;
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    /// <summary>미리보기 창 (명세 8장): 서식 보기 ↔ 텍스트 보기, 이전/다음, 본문 복사, Outlook으로 열기.</summary>
    internal sealed class PreviewForm : Form
    {
        private readonly IList<MailRow> _rows;
        private readonly AppSettings _settings;
        private int _index;
        private PreviewContent? _content;

        // 긴 제목도 끝까지 보이도록 줄바꿈 (잘라내지 않음), 드래그로 복사 가능
        private readonly TextBox _subject = new TextBox
        {
            Dock = DockStyle.Top, Font = new Font(Ui.Base.FontFamily, 12F, FontStyle.Bold), ReadOnly = true, Multiline = true,
            WordWrap = true, BorderStyle = BorderStyle.None, BackColor = Theme.AppBg, TabStop = false,
            AccessibleName = L.T("dlg.preview.subjectAccessible"),
        };
        private readonly TableLayoutPanel _meta = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10, 4, 12, 10) };
        private readonly Panel _bodyHost = new Panel { Dock = DockStyle.Fill };
        private readonly TextBox _text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window, BorderStyle = BorderStyle.None };
        private WebBrowser? _web;
        private readonly Button _toggle;
        private readonly Button _prev, _next;
        private readonly Label _path = new Label { AutoSize = false, Width = 360, Height = 28, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = SystemColors.GrayText };

        public PreviewForm(IList<MailRow> rows, int index, AppSettings settings)
        {
            _rows = rows; _index = index; _settings = settings;
            Text = L.T("dlg.preview.title");
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(960, 760);
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            ShowInTaskbar = false;
            MinimumSize = new Size(560, 420);

            _text.Font = new Font(Ui.Base.FontFamily, 10F);
            try
            {
                _web = new WebBrowser
                {
                    Dock = DockStyle.Fill, ScriptErrorsSuppressed = true, AllowWebBrowserDrop = false,
                    IsWebBrowserContextMenuEnabled = true, WebBrowserShortcutsEnabled = true,
                };
                _web.Navigating += OnNavigating;
            }
            catch { _web = null; } // 서식 보기를 쓸 수 없는 환경 → 텍스트만

            _toggle = Ui.Btn(L.T("dlg.preview.viewText"), (_, __) => { _settings.PreviewAsText = !_settings.PreviewAsText; ShowBody(); }, 110);
            _prev = Ui.Btn(L.T("dlg.preview.prev"), (_, __) => Move(-1), 80);
            _next = Ui.Btn(L.T("dlg.preview.next"), (_, __) => Move(+1), 80);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 6, 6, 2), WrapContents = true };
            bar.Controls.AddRange(new Control[]
            {
                _prev, _next, _toggle,
                Ui.Btn(L.T("dlg.preview.copyBody"), (_, __) => { if (_content != null) Clipboard.SetText(_content.Text.Length > 0 ? _content.Text : " "); }),
                Ui.Btn(L.T("dlg.preview.openInOutlook"), (_, __) => Ui.OpenFile(this, Current.Mail.FilePath)),
                Ui.Btn(L.T("dlg.preview.showInFolder"), (_, __) => Ui.ShowInFolder(Current.Mail.FilePath)),
                Ui.Btn(L.T("dlg.preview.close"), (_, __) => Close(), 70),
                _path,
            });

            var sep = new Label { Dock = DockStyle.Top, Height = 1, BackColor = SystemColors.ControlDark };
            Controls.Add(_bodyHost);
            Controls.Add(sep);
            Controls.Add(_meta);
            // 제목은 창 가장자리에 붙지 않게 여백 있는 영역에 둔다 (본문 정보와 같은 왼쪽 선)
            var subjectHost = new Panel { Dock = DockStyle.Top, Padding = new Padding(12, 12, 12, 2), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            subjectHost.Controls.Add(_subject);
            Controls.Add(subjectHost);
            Controls.Add(bar);

            Theme.Apply(this);
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.Alt && e.KeyCode == Keys.Left || e.KeyCode == Keys.PageUp && e.Control) { Move(-1); e.Handled = true; }
                else if (e.Alt && e.KeyCode == Keys.Right || e.KeyCode == Keys.PageDown && e.Control) { Move(+1); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.O) Ui.OpenFile(this, Current.Mail.FilePath);
            };
            Load += (_, __) => ShowCurrent();
            Resize += (_, __) => FitSubject();
        }

        private MailRow Current => _rows[_index];

        private new void Move(int delta)
        {
            var i = _index + delta;
            if (i < 0 || i >= _rows.Count) return;
            _index = i;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            var r = Current;
            var m = r.Mail;
            Text = L.F("dlg.preview.titleWithFile", r.FileName);
            _subject.Text = string.IsNullOrEmpty(m.Subject) ? L.T("dlg.preview.noSubject") : m.Subject;
            FitSubject();
            var meeting = Display.Meeting(m.Meeting);
            var rows = new List<(string, string)>
            {
                (L.T("dlg.preview.hdr.from"), m.SenderName + (m.SenderEmail.Length > 0 && m.SenderEmail != m.SenderName ? $" <{m.SenderEmail}>" : "")),
                (L.T("dlg.preview.hdr.to"), Short(string.Join("; ", m.To))),
            };
            if (m.Cc.Count > 0) rows.Add((L.T("dlg.preview.hdr.cc"), Short(string.Join("; ", m.Cc))));
            rows.Add((L.T("dlg.preview.hdr.date"), $"{r.LocalTime.ToString("yyyy-MM-dd (ddd) HH:mm:ss", L.Culture)}  ·  {Display.Direction(r.Direction)}{(meeting.Length > 0 ? "  ·  " + L.F("dlg.preview.meetingSuffix", meeting) : "")}"));
            rows.Add((L.T("dlg.preview.hdr.attachments"), m.HasAttachments ? L.P("dlg.preview.attachmentCount", m.AttachmentNames.Count, Short(string.Join(", ", m.AttachmentNames))) : L.T("dlg.preview.attachmentNone")));
            _meta.SuspendLayout();
            _meta.Controls.Clear();
            _meta.RowStyles.Clear();
            _meta.RowCount = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                _meta.Controls.Add(new Label { Text = rows[i].Item1, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(2, 2, 12, 2) }, 0, i);
                _meta.Controls.Add(new Label { Text = rows[i].Item2, AutoSize = true, MaximumSize = new Size(Math.Max(300, ClientSize.Width - 120), 0), Margin = new Padding(2) }, 1, i);
            }
            _meta.ResumeLayout();
            _path.Text = m.FilePath;
            _prev.Enabled = _index > 0;
            _next.Enabled = _index < _rows.Count - 1;

            Cursor = Cursors.WaitCursor;
            try { _content = Preview.Load(m); }
            finally { Cursor = Cursors.Default; }
            ShowBody();
        }

        /// <summary>제목 줄 수에 맞춰 높이 조절 (최대 4줄, 넘치면 스크롤 대신 전체가 보이도록 창 너비 기준).</summary>
        private void FitSubject()
        {
            if (_subject.Text.Length == 0) return;
            var width = Math.Max(200, ClientSize.Width - 40);
            var size = TextRenderer.MeasureText(_subject.Text, _subject.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak);
            _subject.Height = Math.Min(size.Height, _subject.Font.Height * 4) + 12;
            _subject.ScrollBars = size.Height > _subject.Font.Height * 4 ? ScrollBars.Vertical : ScrollBars.None;
        }

        private static string Short(string s) => s.Length > 300 ? s.Substring(0, 300) + " …" : s;

        private bool _allowNav;

        private void ShowBody()
        {
            if (_content == null) return;
            bool asText = _settings.PreviewAsText || _web == null;
            _toggle.Text = asText ? L.T("dlg.preview.viewFormatted") : L.T("dlg.preview.viewText");
            _toggle.Enabled = _web != null;
            _bodyHost.Controls.Clear();
            if (asText)
            {
                _text.Text = (_content.Error != null ? "[" + _content.Error + "]" + Environment.NewLine + Environment.NewLine : "")
                             + _content.Text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                _text.SelectionStart = 0;
                _bodyHost.Controls.Add(_text);
            }
            else
            {
                _allowNav = true;
                _web!.DocumentText = _content.Html;
                _bodyHost.Controls.Add(_web);
            }
        }

        /// <summary>본문 표시 외 이동은 막고, 사용자가 누른 링크는 기본 브라우저로 연다.</summary>
        private void OnNavigating(object? sender, WebBrowserNavigatingEventArgs e)
        {
            if (_allowNav && e.Url.ToString() == "about:blank") { _allowNav = false; return; }
            e.Cancel = true;
            var u = e.Url;
            if (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeMailto)
            {
                var ok = Ui.Confirm(this, L.T("dlg.preview.link.title"), L.F("dlg.preview.link.message", u), L.T("dlg.preview.link.open"));
                if (ok) try { Process.Start(new ProcessStartInfo(u.ToString()) { UseShellExecute = true }); } catch { }
            }
        }
    }
}

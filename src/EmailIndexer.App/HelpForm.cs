using System.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EmailIndexer.Core;
using EmailIndexer.Core.Text;

namespace EmailIndexer.App
{
    /// <summary>Help (F1): topic list on the left, description on the right. Arrow keys move between topics; Esc closes.</summary>
    internal sealed class HelpForm : Form
    {
        private sealed class Topic
        {
            public string Id = ""; public string Title = ""; public string Body = "";
            public override string ToString() => Title;
        }

        // Stable topic ids, in display order. Titles and bodies come from help.<id>.title / help.<id>.body.
        private static readonly string[] TopicIds =
        {
            "start", "list", "search", "filters", "preview", "rename",
            "movedelete", "duplicates", "outlook", "settings", "shortcuts", "troubleshooting",
        };

        private readonly List<Topic> _topicsList = new List<Topic>();

        private readonly ListBox _topics = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
        private readonly TextBox _body = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
            BorderStyle = BorderStyle.None, BackColor = SystemColors.Window,
        };
        private readonly Label _heading = new Label { Dock = DockStyle.Top, Height = 40, Padding = new Padding(14, 12, 14, 0), AutoEllipsis = true };

        public HelpForm(string? topic = null)
        {
            foreach (var id in TopicIds)
                _topicsList.Add(new Topic { Id = id, Title = L.T($"help.{id}.title"), Body = L.T($"help.{id}.body") });

            _topics.AccessibleName = L.T("help.acc.topics");
            _body.AccessibleName = L.T("help.acc.body");

            Text = L.F("help.window.title", AppInfo.Name, AppInfo.Version);
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(860, 600);
            MinimumSize = new Size(560, 400);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;

            _heading.Font = new Font(Ui.Base.FontFamily, 13F, FontStyle.Bold);
            _body.Font = new Font(Ui.Base.FontFamily, 10F);
            _topics.Font = new Font(Ui.Base.FontFamily, 10F);
            _topics.ItemHeight = _topics.Font.Height + 10;
            _topics.DrawMode = DrawMode.OwnerDrawFixed;
            _topics.DrawItem += DrawTopic;
            foreach (var t in _topicsList) _topics.Items.Add(t);
            _topics.SelectedIndexChanged += (_, __) => ShowTopic();

            // 주제 목록 너비: 가장 긴 번역 제목이 한 줄에 들어가게 (190~300px)
            var titleW = _topicsList.Max(t => TextRenderer.MeasureText(t.Title, new Font(_topics.Font, FontStyle.Bold)).Width);
            var left = new Panel { Dock = DockStyle.Left, Width = Math.Min(300, Math.Max(190, titleW + 36)), Padding = new Padding(8, 10, 0, 10), BackColor = Theme.AppBg };
            left.Controls.Add(_topics);
            var divider = new Label { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Border };
            var bodyHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 6, 12, 8), BackColor = SystemColors.Window };
            bodyHost.Controls.Add(_body);
            var right = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Window };
            right.Controls.Add(bodyHost);
            right.Controls.Add(_heading);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 6) };
            var close = Ui.Btn(L.T("help.close"), (_, __) => Close(), 80);
            bar.Controls.Add(close);
            bar.Controls.Add(new Label
            {
                AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 7, 12, 0),
                Text = L.T("help.footer.hint"),
            });
            CancelButton = close;

            Controls.Add(right);
            Controls.Add(divider);
            Controls.Add(left);
            Controls.Add(bar);

            Theme.Apply(this);
            var start = topic == null ? 0 : Math.Max(0, _topicsList.FindIndex(t => t.Id == topic));
            Load += (_, __) => { _topics.SelectedIndex = start; _topics.Focus(); };
        }

        private void ShowTopic()
        {
            if (!(_topics.SelectedItem is Topic t)) return;
            _heading.Text = t.Title;
            _body.Text = t.Body.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            _body.SelectionStart = 0;
            _body.SelectionLength = 0;
        }

        /// <summary>The selected topic gets an accent bar and bold text (not distinguished by color alone).</summary>
        private void DrawTopic(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(selected ? Theme.Surface : Theme.AppBg))
                e.Graphics.FillRectangle(bg, e.Bounds);
            if (selected)
                using (var bar = new SolidBrush(Color.FromArgb(0, 95, 184)))
                    e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top + 4, 3, e.Bounds.Height - 8);
            var font = selected ? new Font(_topics.Font, FontStyle.Bold) : _topics.Font;
            TextRenderer.DrawText(e.Graphics, _topicsList[e.Index].Title, font,
                new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height),
                SystemColors.ControlText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (selected) font.Dispose();
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EmailIndexer.Core.Text;

namespace EmailIndexer.App
{
    /// <summary>실행 전 확인 표 (정규화 '변경 전 → 후', 중복 정리 대상 등).</summary>
    internal sealed class PlanDialog : Form
    {
        public sealed class Line
        {
            public string[] Cells = new string[0];
            public Color? Color;
        }

        private readonly Button _ok;

        public PlanDialog(string title, string summary, string[] columns, int[] widths, IList<Line> lines,
            string okText, bool okEnabled, string? extraText = null, Action? extraAction = null)
        {
            Text = title;
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(1100, 640);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;

            var head = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 52, Padding = new Padding(10, 8, 10, 0), Text = summary };
            var list = new BufferedListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, VirtualMode = true,
                VirtualListSize = lines.Count, BorderStyle = BorderStyle.FixedSingle,
            };
            for (int i = 0; i < columns.Length; i++) list.Columns.Add(columns[i], widths[i]);
            list.RetrieveVirtualItem += (_, e) =>
            {
                var l = lines[e.ItemIndex];
                var it = new ListViewItem(l.Cells.Length > 0 ? l.Cells[0] : "");
                for (int i = 1; i < columns.Length; i++) it.SubItems.Add(i < l.Cells.Length ? l.Cells[i] : "");
                if (l.Color.HasValue) it.ForeColor = l.Color.Value;
                e.Item = it;
            };
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0) };
            body.Controls.Add(list);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 8, 8, 0) };
            var cancel = Ui.Btn(L.T("dlg.plan.cancel"), (_, __) => Close(), 90);
            cancel.DialogResult = DialogResult.Cancel;
            _ok = Ui.Btn(okText, (_, __) => { DialogResult = DialogResult.OK; Close(); });
            _ok.Enabled = okEnabled;
            bar.Controls.Add(cancel);
            bar.Controls.Add(_ok);
            if (extraText != null && extraAction != null)
            {
                var extra = Ui.Btn(extraText, (_, __) => { DialogResult = DialogResult.Retry; Close(); });
                bar.Controls.Add(new Label { Width = 40 });
                bar.Controls.Add(extra);
            }
            CancelButton = cancel;

            Controls.Add(body);
            Controls.Add(head);
            Controls.Add(bar);
            Theme.Apply(this);
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EmailIndexer.App
{
    /// <summary>
    /// Windows 11 스타일의 밝고 평평한 테마. 모든 창은 마지막에 <see cref="Apply"/>를 호출한다.
    /// 색 대비(흰 배경 기준): Text 16.9:1, Subtle 6.2:1, 앱 배경(F3F3F3) 위 Subtle 5.6:1.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color AppBg = Color.FromArgb(243, 243, 243);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(229, 229, 229);
        public static readonly Color ControlBorder = Color.FromArgb(209, 209, 209);
        public static readonly Color Hover = Color.FromArgb(246, 246, 246);
        public static readonly Color Pressed = Color.FromArgb(235, 235, 235);
        public static readonly Color Text = Color.FromArgb(26, 26, 26);
        public static readonly Color Subtle = Color.FromArgb(96, 96, 96);
        public static readonly Color Accent = Color.FromArgb(0, 95, 184);
        public static readonly Color AccentLight = Color.FromArgb(224, 238, 250); // 위 Accent 글자 대비 5.4:1
        public static readonly Color HeaderBg = Color.FromArgb(250, 250, 250);

        public const string PrimaryTag = "primary";

        /// <summary>창 전체에 테마 적용 (이미 스타일을 정한 컨트롤은 건드리지 않음).</summary>
        public static void Apply(Form form)
        {
            form.BackColor = AppBg;
            form.ForeColor = Text;
            Walk(form);
        }

        private static void Walk(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                switch (c)
                {
                    case Button b when !Equals(b.Tag, PrimaryTag):
                        Secondary(b);
                        break;
                    case RadioButton rb when rb.Appearance == Appearance.Button:
                        Segment(rb);
                        break;
                    case StatusStrip ss:
                        ss.BackColor = AppBg;
                        ss.SizingGrip = false;
                        ss.Renderer = new FlatStripRenderer();
                        break;
                    case GroupBox g:
                        g.ForeColor = Subtle;
                        break;
                }
                Walk(c);
            }
        }

        /// <summary>보조 버튼: 흰 바탕 + 얇은 회색 테두리, 포커스는 강조색 2px 테두리.</summary>
        public static void Secondary(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Surface;
            b.ForeColor = Text;
            b.FlatAppearance.MouseOverBackColor = Hover;
            b.FlatAppearance.MouseDownBackColor = Pressed;
            b.Cursor = Cursors.Hand;
            void Paint()
            {
                bool f = b.Focused && b.Enabled;
                b.FlatAppearance.BorderColor = f ? Accent : ControlBorder;
                b.FlatAppearance.BorderSize = f ? 2 : 1;
                b.ForeColor = b.Enabled ? Text : SystemColors.GrayText;
            }
            b.GotFocus += (_, __) => Paint();
            b.LostFocus += (_, __) => Paint();
            b.EnabledChanged += (_, __) => Paint();
            Paint();
        }

        /// <summary>검색 범위 같은 세그먼트 버튼: 선택된 칸은 연한 강조색 바탕 + 강조색 굵은 글씨.</summary>
        public static void Segment(RadioButton rb)
        {
            rb.FlatStyle = FlatStyle.Flat;
            rb.BackColor = Surface;
            rb.FlatAppearance.BorderColor = ControlBorder;
            rb.FlatAppearance.CheckedBackColor = AccentLight;
            rb.FlatAppearance.MouseOverBackColor = Hover;
            rb.MinimumSize = new Size(52, 28);
            rb.Padding = new Padding(8, 0, 8, 0);
            rb.Cursor = Cursors.Hand;
            void Paint()
            {
                rb.ForeColor = rb.Checked ? Accent : Text;
                rb.Font = rb.Checked ? Ui.Bold : Ui.Base;
            }
            rb.CheckedChanged += (_, __) => Paint();
            Paint();
        }

        /// <summary>흰 카드: 내용을 감싸고 1px 테두리를 그린다.</summary>
        public static Panel Card(Padding padding)
        {
            var p = new Panel { BackColor = Surface, Padding = padding };
            p.Paint += (_, e) =>
            {
                using var pen = new Pen(Border);
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };
            p.Resize += (_, __) => p.Invalidate();
            return p;
        }

        /// <summary>필터 등 구역 제목: 작은 굵은 회색 글씨 (테두리 상자 대신 여백으로 묶음).</summary>
        public static Label SectionTitle(string text)
            => new Label
            {
                Text = text, AutoSize = true, ForeColor = Subtle, Font = Ui.Bold,
                Margin = new Padding(0, 0, 0, 4),
            };

        /// <summary>
        /// 목록: 행 높이 28px로 여유 있게, 머리글은 평평한 회색 + 아래 구분선.
        /// </summary>
        public static void StyleList(ListView lv)
        {
            lv.BackColor = Surface;
            lv.ForeColor = Text;
            lv.BorderStyle = BorderStyle.None;
            lv.SmallImageList = new ImageList { ImageSize = new Size(1, 28) }; // 행 높이 확보 (표준 방법)
            lv.OwnerDraw = true;
            lv.DrawItem += (_, e) => e.DrawDefault = true;
            lv.DrawSubItem += (_, e) => e.DrawDefault = true;
            lv.DrawColumnHeader += (_, e) =>
            {
                using (var bg = new SolidBrush(HeaderBg)) e.Graphics.FillRectangle(bg, e.Bounds);
                using (var pen = new Pen(Border))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                    e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 6, e.Bounds.Right - 1, e.Bounds.Bottom - 6);
                }
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine
                            | (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right
                               : e.Header.TextAlign == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
                var r = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 16, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, Ui.Bold, r, Subtle, flags);
            };
        }

        /// <summary>상태줄: 그라데이션·테두리 없는 평평한 모양.</summary>
        private sealed class FlatStripRenderer : ToolStripProfessionalRenderer
        {
            public FlatStripRenderer() : base(new FlatColors()) { RoundedEdges = false; }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(Border);
                e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }
        }

        private sealed class FlatColors : ProfessionalColorTable
        {
            public override Color StatusStripGradientBegin => AppBg;
            public override Color StatusStripGradientEnd => AppBg;
            public override Color ToolStripBorder => Border;
            public override Color MenuItemSelected => AccentLight;
            public override Color MenuItemBorder => AccentLight;
            public override Color MenuBorder => ControlBorder;
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color SeparatorDark => Border;
        }

        /// <summary>오른쪽 클릭 메뉴도 같은 평평한 모양.</summary>
        public static void StyleMenu(ContextMenuStrip m)
        {
            m.Renderer = new FlatStripRenderer();
            m.BackColor = Surface;
            m.Font = Ui.Base;
            m.Padding = new Padding(2, 4, 2, 4);
            foreach (ToolStripItem it in m.Items) it.Padding = new Padding(4, 3, 4, 3);
        }
    }
}

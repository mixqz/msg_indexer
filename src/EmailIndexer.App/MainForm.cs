using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EmailIndexer.Core;
using EmailIndexer.Core.Files;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Text;
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    /// <summary>메인 화면 (명세 7·8·11장).</summary>
    internal sealed class MainForm : Form
    {
        private sealed class Col
        {
            // Id: 저장·정렬용 언어 중립 식별자 (ColumnWidths 키). NameKey: 화면 표시용 번역 키.
            public string Id = ""; public string NameKey = ""; public SortColumn Sort; public int Width; public HorizontalAlignment Align;
            public Func<MailRow, string> Get = _ => "";
            public string Name => L.T(NameKey);
        }

        private static readonly Col[] Columns =
        {
            new Col { Id = "time", NameKey = "main.col.time", Sort = SortColumn.Time, Width = 128, Get = r => r.LocalTime.ToString("yyyy-MM-dd HH:mm") },
            new Col { Id = "dir", NameKey = "main.col.dir", Sort = SortColumn.Direction, Width = 44, Get = r => Display.Direction(r.Direction) },
            new Col { Id = "sender", NameKey = "main.col.sender", Sort = SortColumn.Sender, Width = 220, Get = r => r.Mail.SenderName },
            new Col { Id = "subject", NameKey = "main.col.subject", Sort = SortColumn.Subject, Width = 420, Get = r => r.Mail.IsError ? "[" + Display.Error(r.Mail) + "]" : r.Mail.Subject },
            new Col { Id = "attach", NameKey = "main.col.attach", Sort = SortColumn.Attach, Width = 48, Align = HorizontalAlignment.Center, Get = r => r.Mail.HasAttachments ? L.P("main.cell.attachCount", r.Mail.AttachmentNames.Count) : "" },
            new Col { Id = "meeting", NameKey = "main.col.meeting", Sort = SortColumn.Meeting, Width = 48, Align = HorizontalAlignment.Center, Get = r => Display.Meeting(r.Mail.Meeting) },
            new Col { Id = "size", NameKey = "main.col.size", Sort = SortColumn.Size, Width = 72, Align = HorizontalAlignment.Right, Get = r => Display.Size(r.Mail.FileSize) },
            new Col { Id = "status", NameKey = "main.col.status", Sort = SortColumn.Status, Width = 80, Get = Display.Status },
            new Col { Id = "folder", NameKey = "main.col.folder", Sort = SortColumn.Folder, Width = 160, Get = r => r.Folder },
        };

        private readonly AppSettings _settings;
        private readonly string? _startFolder;
        private List<MailRow> _all = new List<MailRow>();
        private List<MailRow> _view = new List<MailRow>();
        private readonly MailFilter _filter = new MailFilter();
        private SortColumn _sortCol = SortColumn.Time;
        private bool _sortAsc;
        private CancellationTokenSource? _scanCts;
        private bool _suppress;

        // 상단
        private readonly TextBox _folderBox = new TextBox { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, BackColor = SystemColors.Window };
        private readonly Button _btnScan;
        // 검색
        private readonly TextBox _search = new TextBox { Width = 380 };
        private readonly List<RadioButton> _scopeButtons = new List<RadioButton>();
        private readonly Label _applied = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(2, 6, 0, 0) };
        private readonly System.Windows.Forms.Timer _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        // 필터
        private readonly ComboBox _period = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
        private readonly DateTimePicker _from = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yy-MM-dd", Width = 88, Enabled = false };
        private readonly DateTimePicker _to = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yy-MM-dd", Width = 88, Enabled = false };
        private readonly CheckBox _dirIn = Chk("main.chk.dirIn"), _dirOut = Chk("main.chk.dirOut");
        private readonly CheckBox _attYes = Chk("main.chk.attYes"), _attNo = Chk("main.chk.attNo");
        private readonly Dictionary<MeetingKind, CheckBox> _meet = new Dictionary<MeetingKind, CheckBox>
        {
            [MeetingKind.Request] = Chk("main.chk.meetRequest"), [MeetingKind.Accepted] = Chk("main.chk.meetAccepted"), [MeetingKind.Declined] = Chk("main.chk.meetDeclined"),
            [MeetingKind.Tentative] = Chk("main.chk.meetTentative"), [MeetingKind.Canceled] = Chk("main.chk.meetCanceled"),
        };
        private readonly Dictionary<StatusFlag, CheckBox> _status = new Dictionary<StatusFlag, CheckBox>
        {
            [StatusFlag.Duplicate] = Chk("main.chk.statusDuplicate"), [StatusFlag.Similar] = Chk("main.chk.statusSimilar"),
            [StatusFlag.Error] = Chk("main.chk.statusError"), [StatusFlag.NotNormalized] = Chk("main.chk.statusNotNormalized"),
        };
        private readonly CheckBox _fmtMsg = Chk("main.chk.fmtMsg"), _fmtEml = Chk("main.chk.fmtEml");
        private readonly TreeView _tree = new TreeView { Width = 196, Height = 130, HideSelection = false, BorderStyle = BorderStyle.FixedSingle };
        private readonly ListBox _senders = new ListBox { Width = 196, Height = 190, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        // 목록
        private readonly BufferedListView _list = new BufferedListView
        {
            Dock = DockStyle.Fill, View = System.Windows.Forms.View.Details, FullRowSelect = true, VirtualMode = true,
            HideSelection = false, MultiSelect = true, BorderStyle = BorderStyle.None,
        };
        private readonly Label _emptyHint = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText, Cursor = Cursors.Hand,
        };
        // 상태줄
        private readonly ToolStripStatusLabel _statusLeft = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripProgressBar _progress = new ToolStripProgressBar { Visible = false, Width = 160 };
        private readonly ToolStripStatusLabel _counts = new ToolStripStatusLabel();

        private static CheckBox Chk(string key) => new CheckBox { Text = L.T(key), AutoSize = true, Margin = new Padding(3, 1, 3, 1) };

        /// <summary>'유사' 행 글자색: 흰 배경 대비 5.7:1 (기존 DarkOrange는 2.3:1로 읽기 어려움).</summary>
        private static readonly Color SimilarText = Color.FromArgb(166, 77, 0);

        public MainForm(AppSettings settings, string? startFolder)
        {
            _settings = settings;
            _startFolder = startFolder;
            Text = AppInfo.Name;
            Font = Ui.Base;
            KeyPreview = true;
            AllowDrop = true;
            MinimumSize = new Size(900, 560);
            RestoreWindow();

            _btnScan = Ui.Btn(L.T("main.btn.scan"), (_, __) => StartScan(), 96); // '스캔 취소'로 바뀌어도 너비가 흔들리지 않게
            Icon = Ui.AppIcon ?? Icon;
            _emptyHint.Text = L.T("main.empty.chooseFolder");

            var body = BuildBody();
            var top = BuildTop();
            Controls.Add(body);
            Controls.Add(top);
            Controls.Add(BuildStatus());
            top.TabIndex = 0; body.TabIndex = 1; // 위쪽 폴더·버튼 → 본문

            WireEvents();
            BuildColumns();
            Theme.Apply(this);
            UpdateCounts();
        }

        // ---------------- 화면 구성 ----------------

        private Control BuildTop()
        {
            // 번역이 길어 한 줄에 다 안 들어가면 버튼 묶음을 둘째 줄로 내린다 (잘림 방지)
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5, RowCount = 2, Padding = new Padding(14, 10, 16, 4), BackColor = Theme.AppBg };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 아이콘
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 앱 이름
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // '백업 폴더'
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 버튼 묶음
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right };
            var logo = new PictureBox
            {
                Size = new Size(22, 22), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0),
                Image = (Ui.AppIcon != null ? new Icon(Ui.AppIcon, 32, 32).ToBitmap() : null),
            };
            t.Controls.Add(logo, 0, 0);
            t.Controls.Add(new Label
            {
                Text = L.T("main.top.appName"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 24, 0),
                Font = new Font(Ui.Base.FontFamily, 11F, FontStyle.Bold),
            }, 1, 0);
            t.Controls.Add(new Label { Text = L.T("main.top.backupFolder"), AutoSize = true, ForeColor = Theme.Subtle, Anchor = AnchorStyles.Left }, 2, 0);
            _folderBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _folderBox.Margin = new Padding(6, 3, 8, 3);
            _folderBox.BackColor = Theme.Surface;
            t.Controls.Add(_folderBox, 3, 0);
            buttons.Controls.Add(Ui.Btn(L.T("main.btn.changeFolder"), (_, __) => ChooseFolder()));
            buttons.Controls.Add(_btnScan);
            // 이 화면의 주 동작 1개만 강조색
            var outlook = Ui.Primary(Ui.Btn(L.T("main.btn.outlookBackup"), (_, __) => OpenOutlookBackup()));
            new ToolTip().SetToolTip(outlook, L.T("main.tip.outlookBackup"));
            buttons.Controls.Add(outlook);
            buttons.Controls.Add(Ui.Btn(L.T("main.btn.settings"), (_, __) => OpenSettings()));
            var help = Ui.Btn(L.T("main.btn.help"), (_, __) => OpenHelp());
            new ToolTip().SetToolTip(help, L.T("main.tip.help"));
            buttons.Controls.Add(help);
            t.Controls.Add(buttons, 4, 0);
            const int minFolderBox = 220;
            void Reflow()
            {
                var fixedW = t.Padding.Horizontal + Enumerable.Range(0, 3).Select(ci => t.GetControlFromPosition(ci, 0))
                    .Where(c => c != null).Sum(c => c!.PreferredSize.Width + c.Margin.Horizontal); // 아이콘+앱 이름+'백업 폴더' 
                var twoRows = t.ClientSize.Width < fixedW + minFolderBox + buttons.PreferredSize.Width;
                var inRow2 = t.GetRow(buttons) == 1;
                if (twoRows == inRow2) return;
                t.SuspendLayout();
                if (twoRows) { t.SetCellPosition(buttons, new TableLayoutPanelCellPosition(0, 1)); t.SetColumnSpan(buttons, 5); buttons.Anchor = AnchorStyles.Left; buttons.Margin = new Padding(0, 6, 0, 0); }
                else { t.SetColumnSpan(buttons, 1); t.SetCellPosition(buttons, new TableLayoutPanelCellPosition(4, 0)); buttons.Anchor = AnchorStyles.Right; buttons.Margin = new Padding(0); }
                t.ResumeLayout();
            }
            t.Resize += (_, __) => Reflow();
            return t;
        }

        private void OpenHelp(string? topic = null)
        {
            using var f = new HelpForm(topic);
            f.ShowDialog(this);
        }

        private Control BuildBody()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 1, BackColor = Theme.Border };
            split.Panel1.BackColor = Theme.AppBg;
            split.Panel2.BackColor = Theme.AppBg;
            split.HandleCreated += (_, __) => { try { split.SplitterDistance = 236; } catch { } };
            split.Panel1.Controls.Add(BuildFilterPanel());
            split.Panel2.Controls.Add(BuildListArea());
            return split;
        }

        private Control BuildFilterPanel()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                Padding = new Padding(16, 12, 8, 12), BackColor = Theme.AppBg,
            };
            var head = new TableLayoutPanel { ColumnCount = 2, Width = 200, Height = 30, Margin = new Padding(0, 0, 0, 10) };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.Controls.Add(new Label { Text = L.T("main.filter.title"), Font = new Font(Ui.Base.FontFamily, 11F, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            var reset = new LinkLabel
            {
                Text = L.T("main.filter.reset"), AutoSize = true, Anchor = AnchorStyles.Right, LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent,
            };
            reset.LinkClicked += (_, __) => ResetFilters();
            head.Controls.Add(reset, 1, 0);
            flow.Controls.Add(head);

            _period.Items.AddRange(new object[] { L.T("main.period.all"), L.T("main.period.today"), L.T("main.period.last7"), L.T("main.period.last30"), L.T("main.period.thisYear"), L.T("main.period.custom") });
            _period.SelectedIndex = 0;
            _period.FlatStyle = FlatStyle.Flat;
            _period.Width = 200;
            var dates = Row(_from, new Label { Text = "~", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, _to);
            flow.Controls.Add(Group(L.T("main.filter.period"), _period, dates));
            flow.Controls.Add(Group(L.T("main.filter.dirInOut"), Row(_dirIn, _dirOut)));
            flow.Controls.Add(Group(L.T("main.filter.attach"), Row(_attYes, _attNo)));
            flow.Controls.Add(Group(L.T("main.filter.meeting"), Row(_meet[MeetingKind.Request], _meet[MeetingKind.Accepted], _meet[MeetingKind.Declined]),
                Row(_meet[MeetingKind.Tentative], _meet[MeetingKind.Canceled])));
            flow.Controls.Add(Group(L.T("main.filter.status"), Row(_status[StatusFlag.Duplicate], _status[StatusFlag.Similar], _status[StatusFlag.Error]),
                Row(_status[StatusFlag.NotNormalized])));
            flow.Controls.Add(Group(L.T("main.filter.format"), Row(_fmtMsg, _fmtEml)));
            foreach (var box in new Control[] { _tree, _senders })
            {
                box.BackColor = Theme.AppBg;
                box.Width = 200;
            }
            _tree.BorderStyle = BorderStyle.None;
            _tree.ShowLines = false;
            _tree.ItemHeight = 24;
            _tree.FullRowSelect = true;
            _senders.BorderStyle = BorderStyle.None;
            _senders.ItemHeight = 22;
            flow.Controls.Add(Group(L.T("main.filter.folder"), _tree));
            flow.Controls.Add(Group(L.T("main.filter.topSenders"), _senders));
            return flow;
        }

        private static FlowLayoutPanel Row(params Control[] cs)
        {
            // 긴 번역(예: 프랑스어)으로 한 줄(필터 칸 200px)에 안 들어가면 세로로 쌓는다
            var width = cs.Sum(c => c.PreferredSize.Width + c.Margin.Horizontal);
            var p = new FlowLayoutPanel
            {
                AutoSize = true, WrapContents = false, Margin = new Padding(0),
                FlowDirection = width > 212 ? FlowDirection.TopDown : FlowDirection.LeftToRight,
            };
            p.Controls.AddRange(cs);
            return p;
        }

        /// <summary>필터 구역: 테두리 상자 대신 작은 제목 + 여백으로 묶는다 (구역 사이 18px, 구역 안 4px).</summary>
        private static Control Group(string title, params Control[] cs)
        {
            var g = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                Margin = new Padding(0, 0, 0, 18), Padding = new Padding(0),
            };
            g.Controls.Add(Theme.SectionTitle(title));
            g.Controls.AddRange(cs);
            return g;
        }

        private Control BuildListArea()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 16, 4), BackColor = Theme.AppBg };

            var searchRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, 6) };
            // 검색창: 흰 카드 안에 테두리 없는 입력칸 (둥근 느낌의 넉넉한 여백)
            var searchCard = Theme.Card(new Padding(10, 7, 10, 4));
            searchCard.Size = new Size(460, 32);
            searchCard.Margin = new Padding(0, 0, 10, 0);
            _search.BorderStyle = BorderStyle.None;
            _search.Dock = DockStyle.Fill;
            _search.BackColor = Theme.Surface;
            searchCard.Controls.Add(_search);
            searchCard.Click += (_, __) => _search.Focus();
            Ui.SetCue(_search, L.T("main.search.cue"));
            searchRow.Controls.Add(new Label { Text = L.T("main.search.label"), AutoSize = true, Font = Ui.Bold, ForeColor = Theme.Subtle, Padding = new Padding(0, 8, 4, 0) });
            searchRow.Controls.Add(searchCard);
            foreach (SearchScope s in Enum.GetValues(typeof(SearchScope)))
            {
                var rb = new RadioButton
                {
                    Text = MailFilter.ScopeName(s), Tag = s, Appearance = Appearance.Button, AutoSize = true,
                    Checked = s == SearchScope.All, Margin = new Padding(0, 2, 0, 2), TextAlign = ContentAlignment.MiddleCenter,
                };
                rb.CheckedChanged += (_, __) => { if (rb.Checked) ApplyFilter(); };
                _scopeButtons.Add(rb);
                searchRow.Controls.Add(rb);
            }

            var appliedRow = new Panel { Dock = DockStyle.Top, Height = 26, Padding = new Padding(2, 4, 4, 0) };
            _applied.Dock = DockStyle.Fill;
            _applied.ForeColor = Theme.Subtle;
            _applied.Padding = new Padding(0);
            appliedRow.Controls.Add(_applied);

            // 목록: 흰 카드
            var listHost = Theme.Card(new Padding(1));
            listHost.Dock = DockStyle.Fill;
            Theme.StyleList(_list);
            _emptyHint.BackColor = Theme.Surface;
            _emptyHint.ForeColor = Theme.Subtle;
            listHost.Controls.Add(_list);
            listHost.Controls.Add(_emptyHint);
            _emptyHint.BringToFront();

            var actions = new FlowLayoutPanel
            {
                // 창이 좁거나 화면 배율이 커도 버튼이 잘리지 않도록 줄바꿈
                Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, Padding = new Padding(0, 8, 0, 4),
            };
            actions.Controls.Add(Ui.Btn(L.T("main.action.preview"), (_, __) => OpenPreview()));
            actions.Controls.Add(Ui.Btn(L.T("main.action.open"), (_, __) => OpenSelected()));
            actions.Controls.Add(Ui.Btn(L.T("main.action.showInFolder"), (_, __) => { var r = FocusedRow(); if (r != null) Ui.ShowInFolder(r.Mail.FilePath); }));
            actions.Controls.Add(new Label { Width = 16 });
            actions.Controls.Add(Ui.Btn(L.T("main.action.normalize"), (_, __) => DoNormalize()));
            actions.Controls.Add(Ui.Btn(L.T("main.action.move"), (_, __) => DoMove()));
            actions.Controls.Add(Ui.Btn(L.T("main.action.delete"), (_, __) => DoDelete()));
            actions.Controls.Add(Ui.Btn(L.T("main.action.dedupe"), (_, __) => DoDedupe()));

            panel.Controls.Add(listHost);
            panel.Controls.Add(appliedRow);
            panel.Controls.Add(searchRow);
            panel.Controls.Add(actions);
            // Tab 순서: 검색 → 목록 → 동작 버튼 (화면에서 읽히는 순서)
            searchRow.TabIndex = 0; listHost.TabIndex = 1; actions.TabIndex = 2; appliedRow.TabIndex = 3;
            _search.AccessibleName = L.T("main.acc.search");
            _list.AccessibleName = L.T("main.acc.list");
            _list.AccessibleDescription = L.T("main.acc.listDesc");
            _folderBox.AccessibleName = L.T("main.acc.folderBox");
            _tree.AccessibleName = L.T("main.acc.tree");
            _senders.AccessibleName = L.T("main.acc.senders");
            _period.AccessibleName = L.T("main.acc.period");
            _from.AccessibleName = L.T("main.acc.from");
            _to.AccessibleName = L.T("main.acc.to");
            foreach (var rb in _scopeButtons) rb.AccessibleName = L.F("main.acc.scope", rb.Text);
            return panel;
        }

        private Control BuildStatus()
        {
            var s = new StatusStrip { SizingGrip = true, ShowItemToolTips = true };
            _statusLeft.TextChanged += (_, __) => _statusLeft.ToolTipText = _statusLeft.Text; // 잘려도 마우스를 올리면 전체 문구
            s.Items.AddRange(new ToolStripItem[] { _statusLeft, _progress, _counts });
            return s;
        }

        /// <summary>짧은 값 열(구분·일정·상태·첨부)은 번역된 값·제목이 다 보이는 너비로.</summary>
        private int FitWidth(Col c)
        {
            IEnumerable<string> values;
            switch (c.Id)
            {
                case "dir": values = new[] { Display.Direction(MailDirection.Received), Display.Direction(MailDirection.Sent) }; break;
                case "meeting": values = Enum.GetValues(typeof(MeetingKind)).Cast<MeetingKind>().Select(Display.Meeting); break;
                case "status": values = new[] { "status.error", "status.duplicate", "status.keeper", "status.similar", "status.normalized" }.Select(L.T); break;
                case "attach": values = new[] { L.P("main.cell.attachCount", 99) }; break;
                default: return 0;
            }
            var font = _list.Font ?? Font;
            return values.Concat(new[] { c.Name + " ▼" }).Max(v => TextRenderer.MeasureText(v ?? "", font).Width) + 18;
        }

        private void BuildColumns()
        {
            _list.Columns.Clear();
            foreach (var c in Columns)
            {
                var w = _settings.ColumnWidths.TryGetValue(c.Id, out var saved) && saved > 20 ? saved : Math.Max(c.Width, FitWidth(c));
                _list.Columns.Add(c.Name, w, c.Align);
            }
            UpdateSortHeader();

            var menu = new ContextMenuStrip();
            menu.Items.Add(L.T("main.menu.preview"), null, (_, __) => OpenPreview());
            menu.Items.Add(L.T("main.menu.open"), null, (_, __) => OpenSelected());
            menu.Items.Add(L.T("main.menu.showInFolder"), null, (_, __) => { var r = FocusedRow(); if (r != null) Ui.ShowInFolder(r.Mail.FilePath); });
            menu.Items.Add(L.T("main.menu.copyPath"), null, (_, __) =>
            {
                var paths = SelectedRows().Select(r => r.Mail.FilePath).ToList();
                if (paths.Count > 0) Clipboard.SetText(string.Join(Environment.NewLine, paths));
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("main.menu.normalize"), null, (_, __) => DoNormalize());
            menu.Items.Add(L.T("main.menu.move"), null, (_, __) => DoMove());
            menu.Items.Add(L.T("main.menu.trash"), null, (_, __) => DoDelete());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("main.menu.thisSenderOnly"), null, (_, __) =>
            {
                var r = FocusedRow();
                if (r == null) return;
                _filter.SenderEmail = MailFilter.Sender(r.Mail);
                _suppress = true; _senders.ClearSelected(); _suppress = false;
                ApplyFilter();
            });
            Theme.StyleMenu(menu);
            _list.ContextMenuStrip = menu;
        }

        // ---------------- 이벤트 ----------------

        private void WireEvents()
        {
            _list.RetrieveVirtualItem += OnRetrieveItem;
            _list.ColumnClick += (_, e) =>
            {
                var col = Columns[e.Column].Sort;
                if (_sortCol == col) _sortAsc = !_sortAsc;
                else { _sortCol = col; _sortAsc = col != SortColumn.Time && col != SortColumn.Size && col != SortColumn.Attach; }
                UpdateSortHeader();
                SortView();
            };
            _list.DoubleClick += (_, __) => OpenPreview();
            _list.SelectedIndexChanged += (_, __) => UpdateCounts();
            _list.VirtualItemsSelectionRangeChanged += (_, __) => UpdateCounts();
            _list.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { OpenPreview(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DoDelete(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.A) { SelectAll(); e.Handled = true; }
            };

            _searchTimer.Tick += (_, __) => { _searchTimer.Stop(); ApplyFilter(); };
            _search.TextChanged += (_, __) => { _searchTimer.Stop(); _searchTimer.Start(); };
            _search.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) { _search.Clear(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { _searchTimer.Stop(); ApplyFilter(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down && _view.Count > 0) { _list.Focus(); SelectIndex(0); e.Handled = true; }
            };

            _period.SelectedIndexChanged += (_, __) =>
            {
                _from.Enabled = _to.Enabled = _period.SelectedIndex == 5;
                ApplyFilter();
            };
            _from.ValueChanged += (_, __) => ApplyFilter();
            _to.ValueChanged += (_, __) => ApplyFilter();
            foreach (var c in new[] { _dirIn, _dirOut, _attYes, _attNo, _fmtMsg, _fmtEml }
                         .Concat(_meet.Values).Concat(_status.Values))
                c.CheckedChanged += (_, __) => ApplyFilter();
            _tree.AfterSelect += (_, __) => { _filter.Folder = _tree.SelectedNode?.Tag as string; ApplyFilter(); };
            _senders.SelectedIndexChanged += (_, __) =>
            {
                if (_suppress) return;
                _filter.SenderEmail = (_senders.SelectedItem as SenderItem)?.Key;
                ApplyFilter();
            };

            _emptyHint.Click += (_, __) => _hintAction?.Invoke();
            // 폴더 끌어다 놓기: 창 전체(목록·필터·입력칸 위 어디든)에서 받는다
            foreach (var c in AllControls(this)) c.AllowDrop = true;
            foreach (var c in AllControls(this).Concat(new Control[] { this }))
            {
                c.DragEnter += (_, e) => e.Effect = DroppedFolder(e) != null ? DragDropEffects.Link : DragDropEffects.None;
                c.DragDrop += (_, e) => { var d = DroppedFolder(e); if (d != null) SetRoot(d); };
            }

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.F5) { StartScan(); e.Handled = true; }
                else if (e.KeyCode == Keys.F1) { OpenHelp(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.F) { _search.Focus(); _search.SelectAll(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.O) { OpenSelected(); e.Handled = true; }
            };

            Shown += async (_, __) =>
            {
                ActiveControl = _search; // 바로 입력해 검색, ↓로 목록 이동
                await OnStartAsync();
            };
            FormClosing += (_, __) => { _scanCts?.Cancel(); SaveWindow(); };
        }

        private static IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in AllControls(c)) yield return d;
            }
        }

        private static string? DroppedFolder(DragEventArgs e)
        {
            if (!(e.Data?.GetData(DataFormats.FileDrop) is string[] paths) || paths.Length == 0) return null;
            return Directory.Exists(paths[0]) ? paths[0] : null;
        }

        private void OnRetrieveItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _view.Count) { e.Item = new ListViewItem(""); return; }
            var r = _view[e.ItemIndex];
            var item = new ListViewItem(Columns[0].Get(r));
            for (int i = 1; i < Columns.Length; i++) item.SubItems.Add(Columns[i].Get(r));
            if (r.Mail.IsError) item.ForeColor = Color.Firebrick;
            else if (r.Entry.Dup == DupStatus.Duplicate) item.ForeColor = SystemColors.GrayText;
            else if (r.Entry.Dup == DupStatus.Similar) item.ForeColor = SimilarText;
            e.Item = item;
        }

        // ---------------- 데이터 ----------------

        private async Task OnStartAsync()
        {
            var root = _startFolder ?? _settings.BackupRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                _statusLeft.Text = L.T("main.status.chooseFolder");
                return;
            }
            await SetRootAsync(root, loadCacheFirst: true);
        }

        private void SetRoot(string root) => _ = SetRootAsync(root, loadCacheFirst: true);

        private async Task SetRootAsync(string root, bool loadCacheFirst)
        {
            _scanCts?.Cancel();
            root = Path.GetFullPath(root);
            _settings.BackupRoot = root;
            TrySaveSettings();
            _folderBox.Text = root;
            if (loadCacheFirst)
            {
                _statusLeft.Text = L.T("main.status.loadingCache");
                _loading = true;
                UpdateEmptyHint();
                var loaded = await Task.Run(() => IndexStore.Load(root));
                _loading = false;
                if (_settings.BackupRoot != root) return;
                if (loaded.Existed)
                {
                    Duplicates.Recompute(loaded.Entries);
                    SetEntries(loaded.Entries);
                }
                else SetEntries(new List<IndexEntry>());
            }
            StartScan();
        }

        private void ChooseFolder()
        {
            using var dlg = new FolderBrowserDialog { Description = L.T("main.browse.description"), ShowNewFolderButton = false };
            if (Directory.Exists(_settings.BackupRoot)) dlg.SelectedPath = _settings.BackupRoot;
            if (dlg.ShowDialog(this) == DialogResult.OK) SetRoot(dlg.SelectedPath);
        }

        private async void StartScan()
        {
            if (_scanCts != null) { _scanCts.Cancel(); return; } // 두 번째 누름 = 취소
            var root = _settings.BackupRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) { ChooseFolder(); return; }

            var cts = _scanCts = new CancellationTokenSource();
            _btnScan.Text = L.T("main.btn.scanCancel");
            UpdateEmptyHint();
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            var progress = new Progress<ScanProgress>(p =>
            {
                if (cts.IsCancellationRequested) return;
                _statusLeft.Text = p.Total > 0 ? L.F("main.scan.progress", p.Phase, p.Done, p.Total) : L.F("main.scan.progressIndeterminate", p.Phase);
                if (p.Total > 0)
                {
                    _progress.Style = ProgressBarStyle.Continuous;
                    _progress.Maximum = p.Total;
                    _progress.Value = Math.Min(p.Done, p.Total);
                }
            });
            try
            {
                var result = await Task.Run(() => new Scanner(new WindowsTrash()).Scan(root, progress, cts.Token));
                if (_settings.BackupRoot != root) return;
                SetEntries(result.Entries);
                _statusLeft.Text = Summary(result);
                if (result.AutoTrashed.Count > 0)
                {
                    var list = string.Join("\n", result.AutoTrashed.Take(10).Select(kv => "· " + Path.GetFileName(kv.Key))) +
                        (result.AutoTrashed.Count > 10 ? L.F("main.autoTrash.more", result.AutoTrashed.Count - 10) : "");
                    MessageBox.Show(this,
                        L.P("main.autoTrash.body", result.AutoTrashed.Count, list),
                        L.T("main.autoTrash.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                if (result.TrashFailures.Count > 0)
                    MessageBox.Show(this, L.F("main.trashFail.body", string.Join("\n", result.TrashFailures.Take(10))),
                        L.T("main.trashFail.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (OperationCanceledException) { _statusLeft.Text = L.T("main.status.scanCanceled"); }
            catch (Exception ex)
            {
                _statusLeft.Text = L.T("main.status.scanFailed");
                MessageBox.Show(this,
                    L.F("main.scanFail.body", root, ex.Message),
                    L.T("main.scanFail.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (ReferenceEquals(_scanCts, cts)) _scanCts = null;
                cts.Dispose();
                _btnScan.Text = L.T("main.btn.scan");
                _progress.Visible = false;
                UpdateEmptyHint();
            }
        }

        private static string Summary(ScanResult r)
        {
            var parts = new List<string> { L.F("main.summary.done", r.Elapsed.TotalSeconds) };
            if (r.FirstScan) parts.Add(L.T("main.summary.firstScan"));
            if (r.CacheWasCorrupt) parts.Add(L.T("main.summary.cacheRebuilt"));
            parts.Add(L.F("main.summary.added", r.Added));
            if (r.Changed > 0) parts.Add(L.F("main.summary.changed", r.Changed));
            if (r.Moved > 0) parts.Add(L.F("main.summary.moved", r.Moved));
            if (r.Removed > 0) parts.Add(L.F("main.summary.removed", r.Removed));
            if (r.AutoTrashed.Count > 0) parts.Add(L.P("main.summary.autoTrashed", r.AutoTrashed.Count));
            if (r.Errors > 0) parts.Add(L.F("main.summary.errors", r.Errors));
            return string.Join(" · ", parts);
        }

        private void SetEntries(List<IndexEntry> entries)
        {
            var focusedPath = FocusedRow()?.Entry.RelPath;
            _all = entries.Select(e => new MailRow(e, _settings.MyAddresses)).ToList();
            RebuildTree();
            RebuildSenders();
            ApplyFilter(focusedPath);
        }

        private bool _loading;
        private Action? _hintAction;

        /// <summary>목록이 비었을 때 상황에 맞는 안내와 다음 동작 (명세 11장 빈 상태).</summary>
        private void UpdateEmptyHint()
        {
            string? text = null;
            _hintAction = null;
            if (string.IsNullOrEmpty(_settings.BackupRoot))
            {
                text = L.T("main.empty.chooseFolder");
                _hintAction = ChooseFolder;
            }
            else if (_all.Count == 0)
            {
                if (_loading || _scanCts != null)
                    text = L.T("main.empty.building");
                else
                {
                    text = L.T("main.empty.noFiles");
                    _hintAction = ChooseFolder;
                }
            }
            else if (_view.Count == 0)
            {
                text = string.IsNullOrWhiteSpace(_search.Text)
                    ? L.T("main.empty.noMatches")
                    : L.F("main.empty.noMatchesWithSearch", _search.Text.Trim());
                _hintAction = ResetFilters;
            }
            _emptyHint.Visible = text != null;
            if (text != null) _emptyHint.Text = text;
            _emptyHint.Cursor = _hintAction != null ? Cursors.Hand : Cursors.Default;
        }

        private void RebuildTree()
        {
            var selected = _filter.Folder;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            var root = _tree.Nodes.Add(L.F("main.tree.all", _all.Count));
            root.Tag = null;
            var map = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase) { [""] = root };
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _all)
            {
                var path = "";
                foreach (var seg in r.Folder.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    path = path.Length == 0 ? seg : path + "\\" + seg;
                    counts[path] = counts.TryGetValue(path, out var c) ? c + 1 : 1;
                }
            }
            foreach (var p in counts.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase))
            {
                var cut = p.LastIndexOf('\\');
                var parent = cut < 0 ? root : map[p.Substring(0, cut)];
                var node = parent.Nodes.Add(L.F("main.tree.node", cut < 0 ? p : p.Substring(cut + 1), counts[p]));
                node.Tag = p;
                map[p] = node;
            }
            root.Expand();
            _suppress = true;
            _tree.SelectedNode = selected != null && map.TryGetValue(selected, out var sel) ? sel : null;
            if (_tree.SelectedNode == null) _filter.Folder = null;
            _suppress = false;
            _tree.EndUpdate();
        }

        private sealed class SenderItem
        {
            public string? Key; public string Label = "";
            public override string ToString() => Label;
        }

        private void RebuildSenders()
        {
            _suppress = true;
            _senders.BeginUpdate();
            _senders.Items.Clear();
            _senders.Items.Add(new SenderItem { Key = null, Label = L.T("main.sender.all") });
            foreach (var (key, name, count) in MailSort.TopSenders(_all, 30))
                _senders.Items.Add(new SenderItem { Key = key, Label = L.F("main.sender.item", string.IsNullOrEmpty(name) ? key : name, count) });
            _senders.EndUpdate();
            _suppress = false;
        }

        private void ReadFilterControls()
        {
            var today = DateTime.Today;
            _filter.DateFrom = _filter.DateTo = null;
            switch (_period.SelectedIndex)
            {
                case 1: _filter.DateFrom = today; break;
                case 2: _filter.DateFrom = today.AddDays(-6); break;
                case 3: _filter.DateFrom = today.AddDays(-29); break;
                case 4: _filter.DateFrom = new DateTime(today.Year, 1, 1); break;
                case 5: _filter.DateFrom = _from.Value.Date; _filter.DateTo = _to.Value.Date; break;
            }
            _filter.Directions.Clear();
            if (_dirIn.Checked) _filter.Directions.Add(MailDirection.Received);
            if (_dirOut.Checked) _filter.Directions.Add(MailDirection.Sent);
            _filter.HasAttachments = _attYes.Checked == _attNo.Checked ? (bool?)null : _attYes.Checked;
            _filter.Meetings.Clear();
            foreach (var kv in _meet) if (kv.Value.Checked) _filter.Meetings.Add(kv.Key);
            _filter.Statuses.Clear();
            foreach (var kv in _status) if (kv.Value.Checked) _filter.Statuses.Add(kv.Key);
            _filter.Formats.Clear();
            if (_fmtMsg.Checked) _filter.Formats.Add(MailFormat.Msg);
            if (_fmtEml.Checked) _filter.Formats.Add(MailFormat.Eml);
            _filter.Search = _search.Text;
            _filter.Scope = (SearchScope)(_scopeButtons.FirstOrDefault(b => b.Checked)?.Tag ?? SearchScope.All);
        }

        private void ApplyFilter(string? keepFocusRel = null)
        {
            if (_suppress) return;
            keepFocusRel ??= FocusedRow()?.Entry.RelPath;
            ReadFilterControls();
            _view = _filter.Apply(_all);
            _view.Sort(MailSort.By(_sortCol, _sortAsc));
            RefreshList(keepFocusRel);
            var d = _filter.Describe();
            _applied.Text = d.Count == 0 ? "" : L.F("main.applied", string.Join("  ·  ", d));
            UpdateEmptyHint();
        }

        private void SortView()
        {
            var keep = FocusedRow()?.Entry.RelPath;
            _view.Sort(MailSort.By(_sortCol, _sortAsc));
            RefreshList(keep);
        }

        private void RefreshList(string? keepFocusRel)
        {
            _list.BeginUpdate();
            _list.SelectedIndices.Clear();
            _list.VirtualListSize = _view.Count;
            _list.EndUpdate();
            _list.Invalidate();
            if (keepFocusRel != null)
            {
                var i = _view.FindIndex(r => r.Entry.RelPath == keepFocusRel);
                if (i >= 0) SelectIndex(i);
            }
            UpdateCounts();
        }

        private void SelectIndex(int i)
        {
            if (i < 0 || i >= _view.Count) return;
            _list.SelectedIndices.Clear();
            _list.SelectedIndices.Add(i);
            _list.FocusedItem = _list.Items[i];
            _list.EnsureVisible(i);
        }

        private void SelectAll()
        {
            if (_view.Count == 0) return;
            _list.VirtualListSize = _view.Count;
            for (int i = 0; i < _view.Count; i++) _list.SelectedIndices.Add(i);
            UpdateCounts();
        }

        private void UpdateSortHeader()
        {
            for (int i = 0; i < Columns.Length; i++)
                _list.Columns[i].Text = Columns[i].Sort == _sortCol
                    ? L.F(_sortAsc ? "main.sort.asc" : "main.sort.desc", Columns[i].Name)
                    : Columns[i].Name;
        }

        private void UpdateCounts()
            => _counts.Text = L.F("main.counts", _all.Count, _view.Count, _list.SelectedIndices.Count);

        private void ResetFilters()
        {
            _suppress = true;
            _period.SelectedIndex = 0;
            foreach (var c in new[] { _dirIn, _dirOut, _attYes, _attNo, _fmtMsg, _fmtEml }.Concat(_meet.Values).Concat(_status.Values))
                c.Checked = false;
            _search.Clear();
            _scopeButtons[0].Checked = true;
            _tree.SelectedNode = null;
            _senders.ClearSelected();
            _filter.Folder = null;
            _filter.SenderEmail = null;
            _suppress = false;
            ApplyFilter();
        }

        // ---------------- 선택·동작 ----------------

        private MailRow? FocusedRow()
        {
            if (_view.Count == 0) return null;
            var i = _list.FocusedItem?.Index ?? -1;
            if (i < 0 && _list.SelectedIndices.Count > 0) i = _list.SelectedIndices[0];
            return i >= 0 && i < _view.Count ? _view[i] : null;
        }

        private List<MailRow> SelectedRows()
        {
            var list = new List<MailRow>();
            foreach (int i in _list.SelectedIndices) if (i < _view.Count) list.Add(_view[i]);
            return list;
        }

        private void OpenPreview()
        {
            var r = FocusedRow();
            if (r == null) return;
            using var f = new PreviewForm(_view.ToList(), _view.IndexOf(r), _settings);
            f.ShowDialog(this);
            TrySaveSettings();
        }

        private void OpenSelected()
        {
            var rows = SelectedRows();
            if (rows.Count == 0) { var r = FocusedRow(); if (r != null) rows.Add(r); }
            if (rows.Count > 5 && !Ui.Confirm(this, L.T("main.open.title"), L.F("main.open.message", rows.Count), L.F("main.open.ok", rows.Count))) return;
            foreach (var r in rows) Ui.OpenFile(this, r.Mail.FilePath);
        }

        private void OpenSettings()
        {
            using var f = new SettingsForm(_settings);
            if (f.ShowDialog(this) != DialogResult.OK) return;
            TrySaveSettings();
            foreach (var r in _all) r.Recalc(_settings.MyAddresses);
            ApplyFilter();
            if (f.LanguageChanged)
                MessageBox.Show(this, L.TIn(_settings.Language, "settings.restartToApply"), "Email Archive Indexer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------- 파일 관리 (명세 4·6·9장) ----------------

        /// <summary>파일 작업 전 공통 점검: 폴더 선택됨 + 스캔 중 아님.</summary>
        private bool ReadyForFileOps()
        {
            if (string.IsNullOrEmpty(_settings.BackupRoot) || _all.Count == 0) return false;
            if (_scanCts != null)
            {
                MessageBox.Show(this, L.T("main.busy.scanning"), L.T("main.busy.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        /// <summary>선택한 항목, 없으면 현재 표시 중인 전체.</summary>
        private (List<MailRow> rows, string scope) TargetRows()
        {
            var sel = SelectedRows();
            return sel.Count > 0 ? (sel, L.P("main.target.selected", sel.Count)) : (_view.ToList(), L.P("main.target.viewAll", _view.Count));
        }

        private void AfterFileOp(string title, OpResult res)
        {
            _statusLeft.Text = L.F("main.status.fileOp", title, res);
            if (res.Failures.Count > 0)
                MessageBox.Show(this, $"{res}\n\n" + string.Join("\n", res.Failures.Take(15)) + (res.Failures.Count > 15 ? "\n…" : ""),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            StartScan(); // 바뀐 파일만 다시 읽어 캐시 갱신 ('이동'으로 인식)
        }

        private void DoNormalize()
        {
            if (!ReadyForFileOps()) return;
            var (rows, scope) = TargetRows();
            if (rows.Count == 0) return;
            List<RenamePlan> plans;
            Cursor = Cursors.WaitCursor;
            try { plans = FileOps.PlanRenames(rows); }
            finally { Cursor = Cursors.Default; }

            int n = plans.Count(p => p.Status == PlanStatus.Rename);
            int skip = plans.Count(p => p.Status == PlanStatus.Skip);
            int err = plans.Count(p => p.Status == PlanStatus.Error);
            var lines = plans.OrderBy(p => p.Status).Select(p => new PlanDialog.Line
            {
                Cells = new[] { p.Status == PlanStatus.Rename ? L.T("main.normalize.status.rename") : p.Status == PlanStatus.Skip ? L.T("main.normalize.status.skip") : L.T("main.normalize.status.error"), p.OldName, p.NewName, p.Note },
                Color = p.Status == PlanStatus.Rename ? (Color?)null : p.Status == PlanStatus.Skip ? SystemColors.GrayText : Color.Firebrick,
            }).ToList();
            var undoFile = FileOps.LatestUndo(_settings.BackupRoot);

            using var dlg = new PlanDialog(L.T("main.normalize.title"),
                L.F("main.normalize.summary", scope, n, skip, err),
                new[] { L.T("main.normalize.col.action"), L.T("main.normalize.col.oldName"), L.T("main.normalize.col.newName"), L.T("main.normalize.col.note") }, new[] { 70, 420, 460, 140 }, lines,
                L.P("main.normalize.ok", n), n > 0,
                undoFile != null ? L.P("main.normalize.undo", FileOps.UndoCount(undoFile)) : null,
                undoFile != null ? () => DoUndoRename(undoFile) : (Action?)null);
            var choice = dlg.ShowDialog(this);
            if (choice == DialogResult.Retry && undoFile != null) { DoUndoRename(undoFile); return; }
            if (choice != DialogResult.OK) return;

            Cursor = Cursors.WaitCursor;
            OpResult res;
            try { res = FileOps.ApplyRenames(_settings.BackupRoot, plans); }
            finally { Cursor = Cursors.Default; }
            AfterFileOp(L.T("main.normalize.opTitle"), res);
        }

        private void DoUndoRename(string undoFile)
        {
            if (!ReadyForFileOps()) return;
            var n = FileOps.UndoCount(undoFile);
            if (!Ui.Confirm(this, L.T("main.undo.title"), L.P("main.undo.message", n),
                    L.P("main.undo.ok", n))) return;
            AfterFileOp(L.T("main.undo.opTitle"), FileOps.UndoRenames(_settings.BackupRoot, undoFile));
        }

        private void DoMove()
        {
            if (!ReadyForFileOps()) return;
            var rows = SelectedRows();
            if (rows.Count == 0) { MessageBox.Show(this, L.T("main.move.needSelection"), L.T("main.move.title")); return; }
            using var dlg = new FolderBrowserDialog { Description = L.P("main.move.browseDesc", rows.Count), SelectedPath = _settings.BackupRoot, ShowNewFolderButton = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var outside = !Path.GetFullPath(dlg.SelectedPath).StartsWith(_settings.BackupRoot, StringComparison.OrdinalIgnoreCase);
            if (!Ui.Confirm(this, L.T("main.move.title"), L.P("main.move.confirmMessage", rows.Count, dlg.SelectedPath) +
                                              (outside ? L.T("main.move.outsideNote") : ""),
                    L.P("main.move.ok", rows.Count))) return;
            AfterFileOp(L.T("main.move.title"), FileOps.Move(_settings.BackupRoot, rows, dlg.SelectedPath));
        }

        private void DoDelete()
        {
            if (!ReadyForFileOps()) return;
            var rows = SelectedRows();
            if (rows.Count == 0) { MessageBox.Show(this, L.T("main.delete.needSelection"), L.T("main.delete.title")); return; }
            if (!Ui.Confirm(this, L.T("main.delete.title"), L.P("main.delete.confirmMessage", rows.Count),
                    L.P("main.delete.ok", rows.Count), destructive: true)) return;
            AfterFileOp(L.T("main.delete.title"), FileOps.Trash(_settings.BackupRoot, rows, new WindowsTrash()));
        }

        private void OpenOutlookBackup()
        {
            if (string.IsNullOrEmpty(_settings.BackupRoot) || !Directory.Exists(_settings.BackupRoot))
            {
                MessageBox.Show(this, L.T("main.outlook.needFolder"), L.T("main.outlook.title"));
                ChooseFolder();
                return;
            }
            if (_scanCts != null)
            {
                MessageBox.Show(this, L.T("main.busy.scanning"), L.T("main.busy.title"));
                return;
            }
            using var f = new OutlookBackupForm(_settings.BackupRoot, _settings, _all.Select(r => r.Mail.MessageId));
            f.ShowDialog(this);
            if (f.NewAddresses.Count > 0)
            {
                TrySaveSettings();
                foreach (var r in _all) r.Recalc(_settings.MyAddresses);
            }
            if (f.SavedCount > 0) StartScan();
            else if (f.NewAddresses.Count > 0) ApplyFilter();
        }

        private void DoDedupe()
        {
            if (!ReadyForFileOps()) return;
            var dups = FileOps.DuplicatesToRemove(_all);
            if (dups.Count == 0) { MessageBox.Show(this, L.T("main.dedupe.none"), L.T("main.dedupe.title")); return; }
            var lines = dups.OrderBy(d => d.Entry.DupGroup).Select(d => new PlanDialog.Line
            {
                Cells = new[] { d.Entry.DupGroup.ToString(), d.Entry.RelPath, d.Entry.KeeperRelPath ?? "", d.Mail.Subject },
            }).ToList();
            using var dlg = new PlanDialog(L.T("main.dedupe.dialogTitle"),
                L.P("main.dedupe.summary", dups.Count),
                new[] { L.T("main.dedupe.col.group"), L.T("main.dedupe.col.toTrash"), L.T("main.dedupe.col.toKeep"), L.T("main.dedupe.col.subject") }, new[] { 50, 380, 380, 280 }, lines,
                L.P("main.dedupe.ok", dups.Count), true);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            AfterFileOp(L.T("main.dedupe.opTitle"), FileOps.Trash(_settings.BackupRoot, dups, new WindowsTrash(), "TRASH_DUPLICATE"));
        }

        // ---------------- 창·설정 저장 ----------------

        private void RestoreWindow()
        {
            StartPosition = FormStartPosition.Manual;
            var b = new Rectangle(_settings.WindowX, _settings.WindowY, Math.Max(900, _settings.WindowW), Math.Max(560, _settings.WindowH));
            var visible = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(b)) && b.X >= -8;
            if (!visible)
            {
                var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
                b.Width = Math.Min(b.Width, wa.Width); b.Height = Math.Min(b.Height, wa.Height);
                b.X = wa.X + (wa.Width - b.Width) / 2; b.Y = wa.Y + (wa.Height - b.Height) / 2;
            }
            Bounds = b;
            if (_settings.WindowMaximized) WindowState = FormWindowState.Maximized;
        }

        private void SaveWindow()
        {
            _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _settings.WindowX = b.X; _settings.WindowY = b.Y; _settings.WindowW = b.Width; _settings.WindowH = b.Height;
            for (int i = 0; i < Columns.Length && i < _list.Columns.Count; i++)
                _settings.ColumnWidths[Columns[i].Id] = _list.Columns[i].Width;
            TrySaveSettings();
        }

        private void TrySaveSettings()
        {
            try { _settings.Save(); } catch { /* 설정 저장 실패는 치명적이지 않음 */ }
        }
    }
}

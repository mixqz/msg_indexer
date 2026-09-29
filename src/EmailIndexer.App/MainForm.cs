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
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    /// <summary>메인 화면 (명세 7·8·11장).</summary>
    internal sealed class MainForm : Form
    {
        private sealed class Col
        {
            public string Name = ""; public SortColumn Sort; public int Width; public HorizontalAlignment Align;
            public Func<MailRow, string> Get = _ => "";
        }

        private static readonly Col[] Columns =
        {
            new Col { Name = "일시", Sort = SortColumn.Time, Width = 128, Get = r => r.LocalTime.ToString("yyyy-MM-dd HH:mm") },
            new Col { Name = "구분", Sort = SortColumn.Direction, Width = 44, Get = r => Display.Direction(r.Direction) },
            new Col { Name = "발신자", Sort = SortColumn.Sender, Width = 220, Get = r => r.Mail.SenderName },
            new Col { Name = "제목", Sort = SortColumn.Subject, Width = 420, Get = r => r.Mail.IsError ? "[오류] " + r.Mail.ParseError : r.Mail.Subject },
            new Col { Name = "첨부", Sort = SortColumn.Attach, Width = 48, Align = HorizontalAlignment.Center, Get = r => r.Mail.HasAttachments ? r.Mail.AttachmentNames.Count + "개" : "" },
            new Col { Name = "일정", Sort = SortColumn.Meeting, Width = 48, Align = HorizontalAlignment.Center, Get = r => Display.Meeting(r.Mail.Meeting) },
            new Col { Name = "크기", Sort = SortColumn.Size, Width = 72, Align = HorizontalAlignment.Right, Get = r => Display.Size(r.Mail.FileSize) },
            new Col { Name = "상태", Sort = SortColumn.Status, Width = 80, Get = Display.Status },
            new Col { Name = "폴더", Sort = SortColumn.Folder, Width = 160, Get = r => r.Folder },
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
        private readonly CheckBox _dirIn = Chk("받음"), _dirOut = Chk("보냄");
        private readonly CheckBox _attYes = Chk("있음"), _attNo = Chk("없음");
        private readonly Dictionary<MeetingKind, CheckBox> _meet = new Dictionary<MeetingKind, CheckBox>
        {
            [MeetingKind.Request] = Chk("초대"), [MeetingKind.Accepted] = Chk("수락"), [MeetingKind.Declined] = Chk("거절"),
            [MeetingKind.Tentative] = Chk("미정"), [MeetingKind.Canceled] = Chk("취소"),
        };
        private readonly Dictionary<StatusFlag, CheckBox> _status = new Dictionary<StatusFlag, CheckBox>
        {
            [StatusFlag.Duplicate] = Chk("중복"), [StatusFlag.Similar] = Chk("유사"),
            [StatusFlag.Error] = Chk("오류"), [StatusFlag.NotNormalized] = Chk("미정규화"),
        };
        private readonly CheckBox _fmtMsg = Chk("msg"), _fmtEml = Chk("eml");
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
            Text = "메일 백업 폴더를 선택하세요.\n\n[폴더 변경]을 누르거나, 탐색기에서 폴더를 이 창으로 끌어다 놓으면 됩니다.",
        };
        // 상태줄
        private readonly ToolStripStatusLabel _statusLeft = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripProgressBar _progress = new ToolStripProgressBar { Visible = false, Width = 160 };
        private readonly ToolStripStatusLabel _counts = new ToolStripStatusLabel();

        private static CheckBox Chk(string text) => new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 1, 3, 1) };

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

            _btnScan = Ui.Btn("스캔 (F5)", (_, __) => StartScan(), 96); // '스캔 취소'로 바뀌어도 너비가 흔들리지 않게
            Icon = Ui.AppIcon ?? Icon;

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
            var t = new TableLayoutPanel { Dock = DockStyle.Top, Height = 52, ColumnCount = 9, Padding = new Padding(14, 10, 16, 4), BackColor = Theme.AppBg };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 아이콘
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 앱 이름
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // '백업 폴더'
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var logo = new PictureBox
            {
                Size = new Size(22, 22), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0),
                Image = (Ui.AppIcon != null ? new Icon(Ui.AppIcon, 32, 32).ToBitmap() : null),
            };
            t.Controls.Add(logo, 0, 0);
            t.Controls.Add(new Label
            {
                Text = "Email Archive Indexer", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 24, 0),
                Font = new Font(Ui.Base.FontFamily, 11F, FontStyle.Bold),
            }, 1, 0);
            t.Controls.Add(new Label { Text = "백업 폴더", AutoSize = true, ForeColor = Theme.Subtle, Anchor = AnchorStyles.Left }, 2, 0);
            _folderBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _folderBox.Margin = new Padding(6, 3, 8, 3);
            _folderBox.BackColor = Theme.Surface;
            t.Controls.Add(_folderBox, 3, 0);
            t.Controls.Add(Ui.Btn("폴더 변경", (_, __) => ChooseFolder()), 4, 0);
            t.Controls.Add(_btnScan, 5, 0);
            // 이 화면의 주 동작 1개만 강조색
            var outlook = Ui.Primary(Ui.Btn("Outlook 백업", (_, __) => OpenOutlookBackup()));
            new ToolTip().SetToolTip(outlook, "Outlook(classic)의 받은편지함·보낸편지함을 msg로 저장 (꺼져 있으면 자동 실행)");
            t.Controls.Add(outlook, 6, 0);
            t.Controls.Add(Ui.Btn("설정", (_, __) => OpenSettings()), 7, 0);
            var help = Ui.Btn("도움말 (F1)", (_, __) => OpenHelp());
            new ToolTip().SetToolTip(help, "각 기능 설명과 단축키");
            t.Controls.Add(help, 8, 0);
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
            head.Controls.Add(new Label { Text = "필터", Font = new Font(Ui.Base.FontFamily, 11F, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            var reset = new LinkLabel
            {
                Text = "초기화", AutoSize = true, Anchor = AnchorStyles.Right, LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent,
            };
            reset.LinkClicked += (_, __) => ResetFilters();
            head.Controls.Add(reset, 1, 0);
            flow.Controls.Add(head);

            _period.Items.AddRange(new object[] { "전체 기간", "오늘", "최근 7일", "최근 30일", "올해", "직접 지정" });
            _period.SelectedIndex = 0;
            _period.FlatStyle = FlatStyle.Flat;
            _period.Width = 200;
            var dates = Row(_from, new Label { Text = "~", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, _to);
            flow.Controls.Add(Group("기간", _period, dates));
            flow.Controls.Add(Group("받음 / 보냄", Row(_dirIn, _dirOut)));
            flow.Controls.Add(Group("첨부", Row(_attYes, _attNo)));
            flow.Controls.Add(Group("일정", Row(_meet[MeetingKind.Request], _meet[MeetingKind.Accepted], _meet[MeetingKind.Declined]),
                Row(_meet[MeetingKind.Tentative], _meet[MeetingKind.Canceled])));
            flow.Controls.Add(Group("상태", Row(_status[StatusFlag.Duplicate], _status[StatusFlag.Similar], _status[StatusFlag.Error]),
                Row(_status[StatusFlag.NotNormalized])));
            flow.Controls.Add(Group("형식", Row(_fmtMsg, _fmtEml)));
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
            flow.Controls.Add(Group("폴더", _tree));
            flow.Controls.Add(Group("발신자 TOP 30", _senders));
            return flow;
        }

        private static FlowLayoutPanel Row(params Control[] cs)
        {
            var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
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

            var searchRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(0) };
            // 검색창: 흰 카드 안에 테두리 없는 입력칸 (둥근 느낌의 넉넉한 여백)
            var searchCard = Theme.Card(new Padding(10, 7, 10, 4));
            searchCard.Size = new Size(460, 32);
            searchCard.Margin = new Padding(0, 0, 10, 0);
            _search.BorderStyle = BorderStyle.None;
            _search.Dock = DockStyle.Fill;
            _search.BackColor = Theme.Surface;
            searchCard.Controls.Add(_search);
            searchCard.Click += (_, __) => _search.Focus();
            Ui.SetCue(_search, "검색 — 제목·사람·본문·첨부파일명 (여러 단어 = 모두 포함, \"따옴표\" = 구문)");
            searchRow.Controls.Add(new Label { Text = "검색", AutoSize = true, Font = Ui.Bold, ForeColor = Theme.Subtle, Padding = new Padding(0, 8, 4, 0) });
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
            actions.Controls.Add(Ui.Btn("미리보기 (Enter)", (_, __) => OpenPreview()));
            actions.Controls.Add(Ui.Btn("열기 (Ctrl+O)", (_, __) => OpenSelected()));
            actions.Controls.Add(Ui.Btn("폴더에서 보기", (_, __) => { var r = FocusedRow(); if (r != null) Ui.ShowInFolder(r.Mail.FilePath); }));
            actions.Controls.Add(new Label { Width = 16 });
            actions.Controls.Add(Ui.Btn("파일명 정규화", (_, __) => DoNormalize()));
            actions.Controls.Add(Ui.Btn("선택 이동", (_, __) => DoMove()));
            actions.Controls.Add(Ui.Btn("선택 삭제 (Del)", (_, __) => DoDelete()));
            actions.Controls.Add(Ui.Btn("중복 정리", (_, __) => DoDedupe()));

            panel.Controls.Add(listHost);
            panel.Controls.Add(appliedRow);
            panel.Controls.Add(searchRow);
            panel.Controls.Add(actions);
            // Tab 순서: 검색 → 목록 → 동작 버튼 (화면에서 읽히는 순서)
            searchRow.TabIndex = 0; listHost.TabIndex = 1; actions.TabIndex = 2; appliedRow.TabIndex = 3;
            _search.AccessibleName = "메일 검색";
            _list.AccessibleName = "메일 목록";
            _list.AccessibleDescription = "Enter: 미리보기, Ctrl+O: Outlook으로 열기, Del: 휴지통으로 삭제";
            _folderBox.AccessibleName = "백업 폴더 경로";
            _tree.AccessibleName = "폴더 필터";
            _senders.AccessibleName = "발신자 필터";
            _period.AccessibleName = "기간 필터";
            _from.AccessibleName = "시작일";
            _to.AccessibleName = "종료일";
            foreach (var rb in _scopeButtons) rb.AccessibleName = "검색 범위: " + rb.Text;
            return panel;
        }

        private Control BuildStatus()
        {
            var s = new StatusStrip { SizingGrip = true };
            s.Items.AddRange(new ToolStripItem[] { _statusLeft, _progress, _counts });
            return s;
        }

        private void BuildColumns()
        {
            _list.Columns.Clear();
            foreach (var c in Columns)
            {
                var w = _settings.ColumnWidths.TryGetValue(c.Name, out var saved) && saved > 20 ? saved : c.Width;
                _list.Columns.Add(c.Name, w, c.Align);
            }
            UpdateSortHeader();

            var menu = new ContextMenuStrip();
            menu.Items.Add("미리보기", null, (_, __) => OpenPreview());
            menu.Items.Add("열기 (Outlook)", null, (_, __) => OpenSelected());
            menu.Items.Add("폴더에서 보기", null, (_, __) => { var r = FocusedRow(); if (r != null) Ui.ShowInFolder(r.Mail.FilePath); });
            menu.Items.Add("경로 복사", null, (_, __) =>
            {
                var paths = SelectedRows().Select(r => r.Mail.FilePath).ToList();
                if (paths.Count > 0) Clipboard.SetText(string.Join(Environment.NewLine, paths));
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("파일명 정규화", null, (_, __) => DoNormalize());
            menu.Items.Add("선택 이동…", null, (_, __) => DoMove());
            menu.Items.Add("휴지통으로 삭제", null, (_, __) => DoDelete());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("이 발신자만 보기", null, (_, __) =>
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
                _statusLeft.Text = "백업 폴더를 선택하세요.";
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
                _statusLeft.Text = "캐시 불러오는 중…";
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
            using var dlg = new FolderBrowserDialog { Description = "메일(.msg/.eml) 백업 폴더를 선택하세요", ShowNewFolderButton = false };
            if (Directory.Exists(_settings.BackupRoot)) dlg.SelectedPath = _settings.BackupRoot;
            if (dlg.ShowDialog(this) == DialogResult.OK) SetRoot(dlg.SelectedPath);
        }

        private async void StartScan()
        {
            if (_scanCts != null) { _scanCts.Cancel(); return; } // 두 번째 누름 = 취소
            var root = _settings.BackupRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) { ChooseFolder(); return; }

            var cts = _scanCts = new CancellationTokenSource();
            _btnScan.Text = "스캔 취소";
            UpdateEmptyHint();
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            var progress = new Progress<ScanProgress>(p =>
            {
                if (cts.IsCancellationRequested) return;
                _statusLeft.Text = p.Total > 0 ? $"{p.Phase} {p.Done:#,0} / {p.Total:#,0}" : p.Phase + "…";
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
                    MessageBox.Show(this,
                        $"이미 있는 메일과 같은 파일 {result.AutoTrashed.Count:#,0}개가 새로 들어와 휴지통으로 보냈습니다. 원본은 그대로 남아 있습니다.\n\n" +
                        string.Join("\n", result.AutoTrashed.Take(10).Select(kv => "· " + Path.GetFileName(kv.Key))) +
                        (result.AutoTrashed.Count > 10 ? $"\n… 외 {result.AutoTrashed.Count - 10}개" : "") +
                        "\n\n필요하면 Windows 휴지통에서 복원할 수 있으며, 복원한 파일은 다시 지우지 않습니다.",
                        "새 중복 파일 정리", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (result.TrashFailures.Count > 0)
                    MessageBox.Show(this, "새 중복 파일 일부를 휴지통으로 보내지 못했습니다 (목록에 '중복'으로 표시됨):\n\n" +
                                          string.Join("\n", result.TrashFailures.Take(10)) +
                                          "\n\n다른 프로그램(Outlook 등)이 파일을 열고 있다면 닫은 뒤 [스캔]을 다시 누르세요.",
                        "중복 정리", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (OperationCanceledException) { _statusLeft.Text = "스캔을 취소했습니다."; }
            catch (Exception ex)
            {
                _statusLeft.Text = "스캔 실패 — [스캔]을 눌러 다시 시도하세요";
                MessageBox.Show(this,
                    $"백업 폴더를 읽지 못했습니다.\n{root}\n\n{ex.Message}\n\n" +
                    "폴더가 그대로 있는지, 접근 권한이 있는지 확인한 뒤 [스캔]을 다시 누르세요. " +
                    "계속 실패하면 [폴더 변경]으로 폴더를 다시 선택하세요.",
                    "스캔 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (ReferenceEquals(_scanCts, cts)) _scanCts = null;
                cts.Dispose();
                _btnScan.Text = "스캔 (F5)";
                _progress.Visible = false;
                UpdateEmptyHint();
            }
        }

        private static string Summary(ScanResult r)
        {
            var parts = new List<string> { $"스캔 완료 {r.Elapsed.TotalSeconds:0.0}초" };
            if (r.FirstScan) parts.Add("첫 스캔");
            if (r.CacheWasCorrupt) parts.Add("캐시 재구축");
            parts.Add($"신규 {r.Added:#,0}");
            if (r.Changed > 0) parts.Add($"변경 {r.Changed:#,0}");
            if (r.Moved > 0) parts.Add($"이동 {r.Moved:#,0}");
            if (r.Removed > 0) parts.Add($"사라짐 {r.Removed:#,0}");
            if (r.AutoTrashed.Count > 0) parts.Add($"새 중복 {r.AutoTrashed.Count}건 휴지통으로 이동");
            if (r.Errors > 0) parts.Add($"오류 {r.Errors:#,0}");
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
                text = "메일 백업 폴더를 선택하세요.\n\n여기를 누르거나 [폴더 변경]을 누르세요. 탐색기에서 폴더를 이 창으로 끌어다 놓아도 됩니다.";
                _hintAction = ChooseFolder;
            }
            else if (_all.Count == 0)
            {
                if (_loading || _scanCts != null)
                    text = "메일 목록을 만드는 중…\n\n처음 여는 폴더는 파일 수에 따라 시간이 걸립니다. 진행 상황은 아래 상태줄에 표시됩니다.";
                else
                {
                    text = "이 폴더(하위 폴더 포함)에서 .msg / .eml 파일을 찾지 못했습니다.\n\n" +
                           "다른 폴더를 쓰려면 여기를 누르세요. Outlook에서 가져오려면 [Outlook 백업]을 누르세요.";
                    _hintAction = ChooseFolder;
                }
            }
            else if (_view.Count == 0)
            {
                text = "조건에 맞는 메일이 없습니다.\n\n" +
                       (string.IsNullOrWhiteSpace(_search.Text) ? "" : $"검색어: \"{_search.Text.Trim()}\"\n") +
                       "여기를 누르면 검색어와 필터를 모두 초기화합니다.";
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
            var root = _tree.Nodes.Add($"(전체) {_all.Count:#,0}");
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
                var node = parent.Nodes.Add($"{(cut < 0 ? p : p.Substring(cut + 1))} ({counts[p]:#,0})");
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
            _senders.Items.Add(new SenderItem { Key = null, Label = "(전체 발신자)" });
            foreach (var (key, name, count) in MailSort.TopSenders(_all, 30))
                _senders.Items.Add(new SenderItem { Key = key, Label = $"{(string.IsNullOrEmpty(name) ? key : name)} ({count})" });
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
            _applied.Text = d.Count == 0 ? "" : "적용: " + string.Join("  ·  ", d);
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
                _list.Columns[i].Text = Columns[i].Name + (Columns[i].Sort == _sortCol ? (_sortAsc ? " ▲" : " ▼") : "");
        }

        private void UpdateCounts()
            => _counts.Text = $"전체 {_all.Count:#,0} · 표시 {_view.Count:#,0} · 선택 {_list.SelectedIndices.Count:#,0}";

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
            if (rows.Count > 5 && !Ui.Confirm(this, "열기", $"선택한 {rows.Count}개 파일을 Outlook에서 한꺼번에 엽니다.", $"{rows.Count}개 열기")) return;
            foreach (var r in rows) Ui.OpenFile(this, r.Mail.FilePath);
        }

        private void OpenSettings()
        {
            using var f = new SettingsForm(_settings);
            if (f.ShowDialog(this) != DialogResult.OK) return;
            TrySaveSettings();
            foreach (var r in _all) r.Recalc(_settings.MyAddresses);
            ApplyFilter();
        }

        // ---------------- 파일 관리 (명세 4·6·9장) ----------------

        /// <summary>파일 작업 전 공통 점검: 폴더 선택됨 + 스캔 중 아님.</summary>
        private bool ReadyForFileOps()
        {
            if (string.IsNullOrEmpty(_settings.BackupRoot) || _all.Count == 0) return false;
            if (_scanCts != null)
            {
                MessageBox.Show(this, "스캔 중입니다. 스캔이 끝난 뒤 다시 시도하세요.", "잠시만요", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        /// <summary>선택한 항목, 없으면 현재 표시 중인 전체.</summary>
        private (List<MailRow> rows, string scope) TargetRows()
        {
            var sel = SelectedRows();
            return sel.Count > 0 ? (sel, $"선택한 {sel.Count:#,0}개") : (_view.ToList(), $"현재 표시된 {_view.Count:#,0}개 전체");
        }

        private void AfterFileOp(string title, OpResult res)
        {
            _statusLeft.Text = $"{title}: {res}";
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
                Cells = new[] { p.Status == PlanStatus.Rename ? "변경" : p.Status == PlanStatus.Skip ? "건너뜀" : "불가", p.OldName, p.NewName, p.Note },
                Color = p.Status == PlanStatus.Rename ? (Color?)null : p.Status == PlanStatus.Skip ? SystemColors.GrayText : Color.Firebrick,
            }).ToList();
            var undoFile = FileOps.LatestUndo(_settings.BackupRoot);

            using var dlg = new PlanDialog("파일명 정규화 - 미리보기",
                $"대상: {scope}   →   변경 {n:#,0} · 건너뜀 {skip:#,0} · 불가 {err:#,0}\n" +
                "규칙: 날짜_시간_발신자_제목_첨부O/X  (받은 메일=수신 시각, 보낸 메일=발신 시각). 실행 후 [되돌리기]로 원래 이름으로 돌릴 수 있습니다.",
                new[] { "처리", "변경 전", "변경 후", "비고" }, new[] { 70, 420, 460, 140 }, lines,
                $"{n:#,0}개 이름 바꾸기", n > 0,
                undoFile != null ? $"마지막 정규화 되돌리기 ({FileOps.UndoCount(undoFile)}개)" : null,
                undoFile != null ? () => DoUndoRename(undoFile) : (Action?)null);
            var choice = dlg.ShowDialog(this);
            if (choice == DialogResult.Retry && undoFile != null) { DoUndoRename(undoFile); return; }
            if (choice != DialogResult.OK) return;

            Cursor = Cursors.WaitCursor;
            OpResult res;
            try { res = FileOps.ApplyRenames(_settings.BackupRoot, plans); }
            finally { Cursor = Cursors.Default; }
            AfterFileOp("파일명 정규화", res);
        }

        private void DoUndoRename(string undoFile)
        {
            if (!ReadyForFileOps()) return;
            var n = FileOps.UndoCount(undoFile);
            if (!Ui.Confirm(this, "정규화 되돌리기", $"마지막 파일명 정규화에서 바꾼 {n:#,0}개 파일의 이름을 원래대로 되돌립니다.\n그 사이 옮기거나 지운 파일은 건너뜁니다.",
                    $"{n:#,0}개 이름 되돌리기")) return;
            AfterFileOp("정규화 되돌리기", FileOps.UndoRenames(_settings.BackupRoot, undoFile));
        }

        private void DoMove()
        {
            if (!ReadyForFileOps()) return;
            var rows = SelectedRows();
            if (rows.Count == 0) { MessageBox.Show(this, "이동할 메일을 먼저 선택하세요.", "선택 이동"); return; }
            using var dlg = new FolderBrowserDialog { Description = $"선택한 {rows.Count:#,0}개를 옮길 폴더", SelectedPath = _settings.BackupRoot, ShowNewFolderButton = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var outside = !Path.GetFullPath(dlg.SelectedPath).StartsWith(_settings.BackupRoot, StringComparison.OrdinalIgnoreCase);
            if (!Ui.Confirm(this, "선택 이동", $"선택한 {rows.Count:#,0}개 파일을 다음 폴더로 옮깁니다.\n{dlg.SelectedPath}" +
                                              (outside ? "\n\n백업 폴더 밖이므로 옮긴 뒤에는 이 목록에 나오지 않습니다." : ""),
                    $"{rows.Count:#,0}개 옮기기")) return;
            AfterFileOp("선택 이동", FileOps.Move(_settings.BackupRoot, rows, dlg.SelectedPath));
        }

        private void DoDelete()
        {
            if (!ReadyForFileOps()) return;
            var rows = SelectedRows();
            if (rows.Count == 0) { MessageBox.Show(this, "삭제할 메일을 목록에서 먼저 선택하세요.", "선택 삭제"); return; }
            if (!Ui.Confirm(this, "선택 삭제", $"선택한 {rows.Count:#,0}개 파일을 휴지통으로 보냅니다.\nWindows 휴지통에서 복원할 수 있습니다.",
                    $"{rows.Count:#,0}개 휴지통으로 보내기", destructive: true)) return;
            AfterFileOp("선택 삭제", FileOps.Trash(_settings.BackupRoot, rows, new WindowsTrash()));
        }

        private void OpenOutlookBackup()
        {
            if (string.IsNullOrEmpty(_settings.BackupRoot) || !Directory.Exists(_settings.BackupRoot))
            {
                MessageBox.Show(this, "먼저 메일을 저장할 백업 폴더를 선택하세요.", "Outlook 백업");
                ChooseFolder();
                return;
            }
            if (_scanCts != null)
            {
                MessageBox.Show(this, "스캔 중입니다. 스캔이 끝난 뒤 다시 시도하세요.", "잠시만요");
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
            if (dups.Count == 0) { MessageBox.Show(this, "정리할 중복 파일이 없습니다.", "중복 정리"); return; }
            var lines = dups.OrderBy(d => d.Entry.DupGroup).Select(d => new PlanDialog.Line
            {
                Cells = new[] { d.Entry.DupGroup.ToString(), d.Entry.RelPath, d.Entry.KeeperRelPath ?? "", d.Mail.Subject },
            }).ToList();
            using var dlg = new PlanDialog("중복 정리 - 미리보기",
                $"같은 메일로 확인된 파일 {dups.Count:#,0}개를 휴지통으로 보내고, 각 묶음의 원본 1개씩은 남깁니다.\n" +
                "남길 파일 우선순위: 정규화된 이름 → msg 형식 → 먼저 들어온 파일. ('유사' 메일은 대상이 아닙니다)",
                new[] { "묶음", "휴지통으로 보낼 파일", "남길 파일", "제목" }, new[] { 50, 380, 380, 280 }, lines,
                $"{dups.Count:#,0}개 휴지통으로", true);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            AfterFileOp("중복 정리", FileOps.Trash(_settings.BackupRoot, dups, new WindowsTrash(), "TRASH_DUPLICATE"));
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
                _settings.ColumnWidths[Columns[i].Name] = _list.Columns[i].Width;
            TrySaveSettings();
        }

        private void TrySaveSettings()
        {
            try { _settings.Save(); } catch { /* 설정 저장 실패는 치명적이지 않음 */ }
        }
    }
}

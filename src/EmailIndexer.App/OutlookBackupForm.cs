using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Outlook;
using EmailIndexer.Core.View;

namespace EmailIndexer.App
{
    /// <summary>Outlook(classic) 자동 백업 창 (명세 10장).</summary>
    internal sealed class OutlookBackupForm : Form
    {
        private readonly string _root;
        private readonly AppSettings _settings;
        private readonly List<string> _knownIds;
        private readonly RadioButton _since = new RadioButton { Text = "마지막 백업 이후", AutoSize = true, Checked = true };
        private readonly RadioButton _days = new RadioButton { Text = "최근", AutoSize = true };
        private readonly NumericUpDown _dayCount = new NumericUpDown { Minimum = 1, Maximum = 3650, Value = 30, Width = 60 };
        private readonly RadioButton _all = new RadioButton { Text = "전체", AutoSize = true };
        private readonly Label _status = new Label { Dock = DockStyle.Top, Height = 96, Padding = new Padding(12, 6, 12, 0) };
        private readonly ListBox _failures = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
        private readonly Button _start, _cancel, _close;
        private CancellationTokenSource? _cts;

        /// <summary>새로 저장된 파일 수 (닫힌 뒤 메인 화면이 다시 스캔할지 판단).</summary>
        public int SavedCount { get; private set; }
        public List<string> NewAddresses { get; } = new List<string>();

        public OutlookBackupForm(string root, AppSettings settings, IEnumerable<string> knownMessageIds)
        {
            _root = root;
            _settings = settings;
            _knownIds = knownMessageIds.ToList();
            Text = "Outlook 백업";
            Font = Ui.Base;
            Icon = Ui.AppIcon ?? Icon;
            Size = new Size(820, 560);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;

            var intro = new Label
            {
                Dock = DockStyle.Top, Height = 70, Padding = new Padding(12, 10, 12, 0),
                Text = "Outlook(classic)의 [받은편지함]·[보낸편지함] 메일을 .msg 파일로 저장합니다. Outlook이 꺼져 있으면 자동으로 실행합니다. Outlook의 원본 메일은 그대로 둡니다.\n" +
                       $"저장 위치: {root}   (하위 폴더 없이 바로 저장, 파일명은 정규화 규칙 적용)\n" +
                       "이미 백업된 메일은 건너뜁니다. Outlook에 보안 확인 창이 뜨면 '허용'을 눌러 주세요.",
            };
            var range = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(10, 6, 10, 0), WrapContents = false };
            range.Controls.AddRange(new Control[]
            {
                new Label { Text = "기간:", AutoSize = true, Font = Ui.Bold, Padding = new Padding(0, 4, 6, 0) },
                _since, _days, _dayCount, new Label { Text = "일", AutoSize = true, Padding = new Padding(0, 4, 12, 0) }, _all,
            });
            _dayCount.ValueChanged += (_, __) => _days.Checked = true;

            var failBox = new GroupBox { Text = "실패·경고 (메일별 사유)", Dock = DockStyle.Fill, Padding = new Padding(8) };
            failBox.Controls.Add(_failures);
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0) };
            body.Controls.Add(failBox);

            _start = Ui.Primary(Ui.Btn("백업 시작", (_, __) => Start(), 110));
            _cancel = Ui.Btn("중지", (_, __) => _cts?.Cancel(), 80);
            _cancel.Enabled = false;
            _close = Ui.Btn("닫기", (_, __) => Close(), 80);
            var log = Ui.Btn("기록 파일 열기", (_, __) =>
            {
                var p = Path.Combine(IndexStore.IndexDir(_root), OutlookBackup.LogName);
                if (File.Exists(p)) try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); } catch { }
            });
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 8, 8, 0) };
            bar.Controls.AddRange(new Control[] { _close, _cancel, _start, log });

            Controls.Add(body);
            Controls.Add(_status);
            Controls.Add(range);
            Controls.Add(intro);
            Controls.Add(bar);

            Theme.Apply(this);
            FormClosing += (_, e) =>
            {
                if (_cts != null) { e.Cancel = true; _cts.Cancel(); _status.Text += "\n중지하는 중… 현재 메일 저장이 끝나면 닫을 수 있습니다."; }
            };
            _status.Text = "기간을 고른 뒤 [백업 시작]을 누르세요.";
        }

        private void Start()
        {
            var options = new BackupOptions
            {
                Range = _all.Checked ? BackupRange.All : _days.Checked ? BackupRange.LastDays : BackupRange.SinceLastBackup,
                Days = (int)_dayCount.Value,
            };
            _failures.Items.Clear();
            _start.Enabled = false; _cancel.Enabled = true; _close.Enabled = false;
            _status.Text = "Outlook에 연결하는 중…";
            var cts = _cts = new CancellationTokenSource();

            // Outlook COM은 STA 스레드에서
            var t = new Thread(() => Worker(options, cts.Token)) { IsBackground = true, Name = "OutlookBackup" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void Worker(BackupOptions options, CancellationToken ct)
        {
            object? app = null;
            List<IOutlookFolder>? folders = null;
            try
            {
                var (state, a, message) = OutlookCom.Connect(s => Ui_(() => _status.Text = s), ct);
                if (state != OutlookCom.ConnectState.Ok) { Ui_(() => Finish(null, message)); return; }
                app = a;
                OutlookBackup.CleanupTempFiles(_root);
                var (fs, addresses) = OutlookCom.GetFolders(app!);
                folders = fs;
                Ui_(() =>
                {
                    foreach (var addr in addresses)
                        if (!_settings.MyAddresses.Contains(addr, StringComparer.OrdinalIgnoreCase)) { _settings.MyAddresses.Add(addr); NewAddresses.Add(addr); }
                });

                var backup = new OutlookBackup(_root, _knownIds);
                var lines = new Dictionary<string, string>();
                var report = backup.Run(folders, options, new InlineProgress(s =>
                {
                    lock (lines) lines[s.Folder] = s.ToString();
                    string text;
                    lock (lines) text = string.Join("\n", lines.Values);
                    Ui_(() => _status.Text = "백업 중…\n" + text);
                }), ct);
                Ui_(() => Finish(report, null));
            }
            catch (Exception ex)
            {
                Ui_(() => Finish(null, "백업 중 오류: " + OutlookBackup.Explain(ex)));
            }
            finally
            {
                if (folders != null) foreach (var f in folders) (f as IDisposable)?.Dispose();
                OutlookCom.Release(app);
            }
        }

        private void Finish(BackupReport? report, string? error)
        {
            _cts?.Dispose();
            _cts = null;
            _start.Enabled = true; _cancel.Enabled = false; _close.Enabled = true;
            if (report == null)
            {
                _status.Text = error ?? "알 수 없는 오류";
                _status.ForeColor = Color.Firebrick;
                return;
            }
            _status.ForeColor = SystemColors.ControlText;
            SavedCount += report.Saved;
            _status.Text = (report.Cancelled ? "중지됨. " : "완료. ") +
                           $"확인 {report.Checked:#,0} · 저장 {report.Saved:#,0} · 건너뜀(이미 백업됨) {report.Skipped:#,0} · 실패 {report.Failed:#,0}\n" +
                           string.Join("\n", report.Folders.Select(f => f.ToString())) +
                           (NewAddresses.Count > 0 ? $"\n내 주소 자동 등록: {string.Join(", ", NewAddresses)}" : "");
            foreach (var f in report.Failures) _failures.Items.Add(f);
            if (report.Checked == 0 && report.Failed == 0)
                _failures.Items.Add("확인한 메일이 없습니다. 기간을 '전체'로 바꿔 보거나 Outlook에서 폴더가 동기화되었는지 확인하세요.");
        }

        private void Ui_(Action a)
        {
            try { if (!IsDisposed) BeginInvoke(a); } catch { }
        }

        /// <summary>작업 스레드에서 바로 호출되는 IProgress.</summary>
        private sealed class InlineProgress : IProgress<BackupFolderStats>
        {
            private readonly Action<BackupFolderStats> _a;
            public InlineProgress(Action<BackupFolderStats> a) => _a = a;
            public void Report(BackupFolderStats value) => _a(value);
        }
    }
}

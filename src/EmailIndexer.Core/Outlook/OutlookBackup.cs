using System;
using EmailIndexer.Core.Text;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using EmailIndexer.Core.Files;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.View;

namespace EmailIndexer.Core.Outlook
{
    /// <summary>Outlook 항목 1개 (실제 구현은 App의 COM 래퍼, 테스트는 가짜).</summary>
    public interface IOutlookItem : IDisposable
    {
        string EntryId { get; }
        /// <summary>인터넷 Message-ID (없으면 빈 문자열).</summary>
        string MessageId { get; }
        /// <summary>받은편지함=수신 시각, 보낸편지함=발신 시각 (현지 시간).</summary>
        DateTime SortTime { get; }
        string Subject { get; }
        /// <summary>Unicode msg로 저장 (olMSGUnicode).</summary>
        void SaveAsMsg(string path);
    }

    public interface IOutlookFolder
    {
        /// <summary>진행 표시용 이름 (예: "me@company.com / 받은편지함").</summary>
        string DisplayName { get; }
        /// <summary>상태 기록용 고유 키 (계정 주소 + 폴더 종류).</summary>
        string Key { get; }
        MailDirection Direction { get; }
        int Count { get; }
        /// <summary>최신순으로 하나씩 (호출자가 각 항목을 Dispose).</summary>
        IEnumerable<IOutlookItem> ItemsNewestFirst();
    }

    public enum BackupRange { SinceLastBackup, LastDays, All }

    public sealed class BackupOptions
    {
        public BackupRange Range { get; set; } = BackupRange.SinceLastBackup;
        public int Days { get; set; } = 30;
    }

    public sealed class BackupFolderStats
    {
        public string Folder { get; set; } = "";
        public int Checked { get; set; }
        public int Saved { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public override string ToString() => L.F("backup.stats", Folder, Checked.ToString("#,0", L.Culture), Saved.ToString("#,0", L.Culture), Skipped.ToString("#,0", L.Culture), Failed.ToString("#,0", L.Culture));
    }

    public sealed class BackupReport
    {
        public List<BackupFolderStats> Folders { get; } = new List<BackupFolderStats>();
        public List<string> Failures { get; } = new List<string>();
        public List<string> SavedFiles { get; } = new List<string>();
        public bool Cancelled { get; set; }
        public int Saved => Folders.Sum(f => f.Saved);
        public int Failed => Folders.Sum(f => f.Failed);
        public int Skipped => Folders.Sum(f => f.Skipped);
        public int Checked => Folders.Sum(f => f.Checked);
    }

    /// <summary>
    /// Outlook(classic) → msg 백업 (명세 10장).
    /// 1) 이미 백업된 메일(Message-ID·EntryID)은 저장하지 않음
    /// 2) 안전한 임시 이름으로 저장 → 파일이 실제로 생겼는지(크기 &gt; 0) 확인
    /// 3) 저장된 파일을 읽어 파일명 규칙대로 이름 변경, 원본 폴더 방향 기록
    /// Outlook의 원본 메일은 건드리지 않는다.
    /// </summary>
    public sealed class OutlookBackup
    {
        public const string SavedLogName = "outlook-saved.tsv";
        public const string StateName = "outlook-state.tsv";
        public const string LogName = "outlook-backup.log";
        private const string TempPrefix = "~eidx_";

        private readonly string _root;
        private readonly HashSet<string> _knownIds;
        private readonly HashSet<string> _savedEntryIds;
        private readonly Dictionary<string, DateTime> _lastRun;

        /// <param name="knownMessageIds">캐시에 이미 있는 메일의 Message-ID (정규화된 소문자)</param>
        public OutlookBackup(string root, IEnumerable<string> knownMessageIds)
        {
            _root = root;
            _knownIds = new HashSet<string>(knownMessageIds.Where(s => !string.IsNullOrEmpty(s)), StringComparer.OrdinalIgnoreCase);
            _savedEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in ReadLines(SavedLogName))
            {
                var p = line.Split('\t');
                if (p.Length >= 1 && p[0].Length > 0) _savedEntryIds.Add(p[0]);
                if (p.Length >= 2 && p[1].Length > 0) _knownIds.Add(p[1]);
            }
            _lastRun = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in ReadLines(StateName))
            {
                var p = line.Split('\t');
                if (p.Length == 2 && DateTime.TryParse(p[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t))
                    _lastRun[p[0]] = t;
            }
        }

        public DateTime? LastRun(string folderKey) => _lastRun.TryGetValue(folderKey, out var t) ? t : (DateTime?)null;

        /// <summary>이 시각보다 오래된 항목에서 멈춘다 (null = 끝까지).</summary>
        public DateTime? Cutoff(IOutlookFolder folder, BackupOptions o, DateTime now)
        {
            switch (o.Range)
            {
                case BackupRange.LastDays: return now.Date.AddDays(-Math.Max(1, o.Days) + 1);
                case BackupRange.SinceLastBackup:
                    // 늦게 동기화되는 메일을 놓치지 않게 7일 여유 (이미 있는 건 어차피 건너뜀)
                    return LastRun(folder.Key)?.AddDays(-7);
                default: return null;
            }
        }

        public BackupReport Run(IEnumerable<IOutlookFolder> folders, BackupOptions options,
            IProgress<BackupFolderStats>? progress = null, CancellationToken ct = default)
        {
            var report = new BackupReport();
            Log($"=== backup start ({options.Range}{(options.Range == BackupRange.LastDays ? " " + options.Days + " days" : "")})");
            foreach (var folder in folders)
            {
                var stats = new BackupFolderStats { Folder = folder.DisplayName };
                report.Folders.Add(stats);
                var started = DateTime.Now;
                var cutoff = Cutoff(folder, options, started);
                bool completed = false;
                try
                {
                    foreach (var item in folder.ItemsNewestFirst())
                    {
                        using (item)
                        {
                            if (ct.IsCancellationRequested) { report.Cancelled = true; break; }
                            DateTime time;
                            try { time = item.SortTime; } catch { time = DateTime.MinValue; }
                            if (cutoff.HasValue && time != DateTime.MinValue && time < cutoff.Value) { completed = true; break; }
                            stats.Checked++;
                            ProcessItem(item, time, folder, stats, report);
                        }
                        if (stats.Checked % 10 == 0) progress?.Report(stats);
                    }
                    if (!report.Cancelled) completed = true;
                }
                catch (Exception ex)
                {
                    report.Failures.Add(L.F("backup.folderError", folder.DisplayName, ex.Message));
                    Log($"FOLDER_ERROR\t{folder.DisplayName}\t{ex.Message}");
                }
                progress?.Report(stats);
                Log($"{stats.Folder}: checked {stats.Checked} saved {stats.Saved} skipped {stats.Skipped} failed {stats.Failed}");
                if (completed && stats.Failed == 0) SetLastRun(folder.Key, started);
                if (report.Cancelled) break;
            }
            SaveState();
            Log($"=== end: saved {report.Saved} / skipped {report.Skipped} / failed {report.Failed}{(report.Cancelled ? " (cancelled)" : "")}");
            return report;
        }

        private void ProcessItem(IOutlookItem item, DateTime time, IOutlookFolder folder, BackupFolderStats stats, BackupReport report)
        {
            string entryId = "", messageId = "", subject = "";
            try { entryId = item.EntryId ?? ""; } catch { }
            try { messageId = MailReader.NormalizeMessageId(item.MessageId); } catch { }
            try { subject = item.Subject ?? ""; } catch { }

            if ((messageId.Length > 0 && _knownIds.Contains(messageId)) || (entryId.Length > 0 && _savedEntryIds.Contains(entryId)))
            {
                stats.Skipped++;
                return;
            }

            // 하위 폴더 없이 백업 폴더에 바로 저장 (파일명으로 인덱싱되고, 사용자가 옮겨도 스캔이 찾아냄)
            var dir = _root;
            string temp = "";
            try
            {
                Directory.CreateDirectory(dir);
                temp = Path.Combine(dir, TempPrefix + Guid.NewGuid().ToString("N").Substring(0, 12) + ".msg");
                item.SaveAsMsg(temp);

                var fi = new FileInfo(temp);
                if (!fi.Exists || fi.Length == 0)
                    throw new IOException(L.T("backup.err.noFile"));

                var info = MailReader.Read(temp);
                if (!info.IsError && info.MessageId.Length > 0 && _knownIds.Contains(info.MessageId))
                {
                    File.Delete(temp); // 방금 만든 임시 파일 (Outlook 속성으로는 몰랐던 기존 메일)
                    _savedEntryIds.Add(entryId);
                    stats.Skipped++;
                    return;
                }

                var finalPath = FinalPath(info, folder.Direction, dir, time, subject);
                File.Move(temp, finalPath);
                var rel = finalPath.Substring(_root.Length).TrimStart('\\', '/');
                FolderHints.Append(_root, rel, folder.Direction);
                var id = info.IsError ? messageId : info.MessageId;
                AppendSaved(entryId, id, rel);
                if (id.Length > 0) _knownIds.Add(id);
                if (entryId.Length > 0) _savedEntryIds.Add(entryId);
                stats.Saved++;
                report.SavedFiles.Add(rel);
                if (info.IsError)
                {
                    report.Failures.Add(L.F("backup.warnUnreadable", rel, Display.Error(info)));
                    Log($"SAVED_UNREADABLE\t{rel}\t{info.ParseError}");
                }
            }
            catch (Exception ex)
            {
                stats.Failed++;
                var what = $"{time:yyyy-MM-dd HH:mm} {Shorten(subject)}";
                report.Failures.Add($"{folder.DisplayName} · {what}: {Explain(ex)}");
                Log($"FAILED\t{folder.DisplayName}\t{what}\t{ex.GetType().Name}: {ex.Message}");
                try { if (temp.Length > 0 && File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        private string FinalPath(MailInfo info, MailDirection direction, string dir, DateTime time, string subject)
        {
            string? name;
            if (info.IsError)
                name = $"{time:yyMMdd_HHmmss}_(읽기 실패)_{FileNameRule.Clean(Shorten(subject, 60))}.msg";
            else
            {
                var row = new MailRow(new IndexEntry { RelPath = "x.msg", Mail = info, FolderHint = direction }, null);
                name = FileNameRule.BuildForDirectory(row, dir) ?? $"{time:yyMMdd_HHmmss}.msg";
            }
            var path = Path.Combine(dir, name);
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(dir, FileNameRule.WithNumber(name, n));
            return path;
        }

        /// <summary>자주 나오는 오류를 사용자 말로.</summary>
        public static string Explain(Exception ex)
        {
            var m = ex.Message ?? "";
            var hr = (uint)ex.HResult;
            if (hr == 0x80070005 || ex is UnauthorizedAccessException) return L.T("backup.err.access");
            if (ex is PathTooLongException) return L.T("backup.err.pathTooLong");
            if (hr == 0x8004010F || m.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) return L.T("backup.err.notFound");
            if (hr == 0x80040115 || hr == 0x8004011D) return L.T("backup.err.exchange");
            if (hr == 0x80004004) return L.T("backup.err.aborted");
            if (m.IndexOf("too many", StringComparison.OrdinalIgnoreCase) >= 0) return L.T("backup.err.tooMany");
            return $"{ex.GetType().Name}: {m}";
        }

        private static string Shorten(string s, int n = 40) => s.Length > n ? s.Substring(0, n) + "…" : s;

        // ---------------- 기록 파일 ----------------

        private IEnumerable<string> ReadLines(string name)
        {
            var p = Path.Combine(IndexStore.IndexDir(_root), name);
            if (!File.Exists(p)) return Enumerable.Empty<string>();
            try { return File.ReadAllLines(p, Encoding.UTF8); } catch { return Enumerable.Empty<string>(); }
        }

        private void AppendSaved(string entryId, string messageId, string rel)
        {
            Directory.CreateDirectory(IndexStore.IndexDir(_root));
            File.AppendAllText(Path.Combine(IndexStore.IndexDir(_root), SavedLogName), $"{entryId}\t{messageId}\t{rel}\n", new UTF8Encoding(false));
        }

        private void SetLastRun(string key, DateTime t) => _lastRun[key] = t;

        private void SaveState()
        {
            try
            {
                Directory.CreateDirectory(IndexStore.IndexDir(_root));
                File.WriteAllLines(Path.Combine(IndexStore.IndexDir(_root), StateName),
                    _lastRun.Select(kv => $"{kv.Key}\t{kv.Value.ToString("o", CultureInfo.InvariantCulture)}"), new UTF8Encoding(false));
            }
            catch { }
        }

        private void Log(string line)
        {
            try
            {
                Directory.CreateDirectory(IndexStore.IndexDir(_root));
                File.AppendAllText(Path.Combine(IndexStore.IndexDir(_root), LogName), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{line}\n", new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>남은 임시 파일(비정상 종료 등) 정리 — 앱이 만든 '~eidx_*.msg'만 대상 (이전 버전의 하위 폴더 포함).</summary>
        public static int CleanupTempFiles(string root)
        {
            int n = 0;
            if (!Directory.Exists(root)) return 0;
            foreach (var f in Scanner.EnumerateAllFiles(root, TempPrefix + "*.msg"))
                try { File.Delete(f); n++; } catch { }
            return n;
        }
    }
}

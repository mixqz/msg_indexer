using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.Core.Index
{
    public sealed class ScanProgress
    {
        public string Phase { get; set; } = "";
        public int Done { get; set; }
        public int Total { get; set; }
    }

    public sealed class ScanResult
    {
        public int Total { get; set; }
        public int Added { get; set; }
        public int Changed { get; set; }
        public int Removed { get; set; }
        public int Moved { get; set; }
        public int Unchanged { get; set; }
        public int Errors { get; set; }
        public bool FirstScan { get; set; }
        public bool CacheWasCorrupt { get; set; }
        /// <summary>자동으로 휴지통에 보낸 새 중복 파일 (상대 경로 → 남긴 파일).</summary>
        public List<KeyValuePair<string, string>> AutoTrashed { get; } = new List<KeyValuePair<string, string>>();
        public List<string> TrashFailures { get; } = new List<string>();
        public TimeSpan Elapsed { get; set; }
        public List<IndexEntry> Entries { get; set; } = new List<IndexEntry>();
    }

    /// <summary>
    /// 증분 스캔 (명세 5·6장): 상대경로+크기+수정시각이 같으면 다시 읽지 않고,
    /// 새로 들어온 파일이 기존 파일과 동일 파일/동일 메일이면 휴지통으로 보낸다.
    /// </summary>
    public sealed class Scanner
    {
        private readonly ITrash _trash;
        public Scanner(ITrash trash) => _trash = trash;

        public ScanResult Scan(string root, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            root = Path.GetFullPath(root);
            var loaded = IndexStore.Load(root);
            var result = new ScanResult { FirstScan = !loaded.Existed, CacheWasCorrupt = loaded.WasCorrupt };

            var old = new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in loaded.Entries) old[e.RelPath] = e;

            // 1) 폴더 목록 비교
            progress?.Report(new ScanProgress { Phase = "파일 목록 확인" });
            var files = EnumerateMailFiles(root).ToList();
            var current = new List<IndexEntry>(files.Count);
            var toRead = new List<(string full, string rel, IndexEntry? prev)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var full in files)
            {
                ct.ThrowIfCancellationRequested();
                var rel = Rel(root, full);
                seen.Add(rel);
                var fi = new FileInfo(full);
                if (old.TryGetValue(rel, out var prev) && prev.Mail.FileSize == fi.Length
                    && prev.Mail.FileModifiedUtc == fi.LastWriteTimeUtc)
                {
                    prev.Mail.FilePath = full;
                    current.Add(prev);
                    result.Unchanged++;
                }
                else toRead.Add((full, rel, prev));
            }
            var removed = old.Values.Where(e => !seen.Contains(e.RelPath)).ToList();
            var hints = toRead.Count > 0 ? FolderHints.Load(root) : new Dictionary<string, MailDirection>();

            // 2) 새 파일·바뀐 파일만 읽기 (병렬)
            var fresh = new ConcurrentBag<(IndexEntry entry, IndexEntry? prev)>();
            int done = 0;
            var now = DateTime.UtcNow;
            // 첫 파일은 혼자 읽어 파서 내부 초기화를 끝낸 뒤 병렬로 (동시 초기화 경합 방지)
            var ordered = toRead.OrderBy(t => t.full.EndsWith(".msg", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
            if (ordered.Count > 0) ReadOne(ordered[0]);
            var firstEml = ordered.FindIndex(t => !t.full.EndsWith(".msg", StringComparison.OrdinalIgnoreCase));
            if (firstEml > 0) ReadOne(ordered[firstEml]);
            var rest = ordered.Where((t, i) => i != 0 && i != firstEml).ToList();

            void ReadOne((string full, string rel, IndexEntry? prev) item)
            {
                var mail = MailReader.Read(item.full);
                string hash = "";
                try { hash = Duplicates.FileSha256(item.full); } catch { /* 읽기 오류는 mail.ParseError에 이미 반영 */ }
                var entry = new IndexEntry
                {
                    RelPath = item.rel,
                    Mail = mail,
                    ContentHash = hash,
                    FirstSeenUtc = item.prev?.FirstSeenUtc ?? now,
                    FolderHint = item.prev?.FolderHint
                                 ?? (hints.TryGetValue(FolderHints.Norm(item.rel), out var h) ? h : (MailDirection?)null),
                };
                fresh.Add((entry, item.prev));
                var d = Interlocked.Increment(ref done);
                if (d % 50 == 0 || d == toRead.Count)
                    progress?.Report(new ScanProgress { Phase = "메일 읽는 중", Done = d, Total = toRead.Count });
            }

            Parallel.ForEach(rest,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                ReadOne);

            // 3) 사라진 파일과 내용이 같은 새 파일 = 이동/이름변경 (중복 아님)
            var removedByHash = removed.Where(r => r.ContentHash.Length > 0)
                .GroupBy(r => r.ContentHash).ToDictionary(g => g.Key, g => new Queue<IndexEntry>(g));
            var newcomers = new List<IndexEntry>();
            foreach (var (entry, prev) in fresh.OrderBy(f => f.entry.RelPath, StringComparer.OrdinalIgnoreCase))
            {
                if (prev != null) { result.Changed++; current.Add(entry); continue; }
                if (removedByHash.TryGetValue(entry.ContentHash, out var q) && q.Count > 0)
                {
                    var src = q.Dequeue();
                    entry.FirstSeenUtc = src.FirstSeenUtc;
                    entry.FolderHint = src.FolderHint ?? entry.FolderHint;
                    result.Moved++;
                    current.Add(entry);
                    continue;
                }
                result.Added++;
                newcomers.Add(entry);
                current.Add(entry);
            }
            result.Removed = removed.Count - result.Moved;

            // 4) 자동 휴지통: 캐시가 있던 상태에서 새로 들어온 파일이 기존 파일과 같은 메일이면
            if (loaded.Existed && newcomers.Count > 0)
            {
                Duplicates.Recompute(current);
                var newSet = new HashSet<IndexEntry>(newcomers);
                var groups = current.Where(e => e.DupGroup > 0 && e.Dup != DupStatus.Similar)
                    .GroupBy(e => e.DupGroup).ToList();
                var trashed = new HashSet<IndexEntry>();
                foreach (var g in groups)
                {
                    var existing = Duplicates.KeeperOrder(g.Where(e => !newSet.Contains(e))).FirstOrDefault();
                    if (existing == null) continue; // 새 파일끼리만 겹침 → 표시만
                    // 자동 삭제는 '확실한' 경우만: 기존 파일과 내용 해시 또는 Message-ID가 직접 일치.
                    // (Message-ID 없는 메일의 지문 일치는 표시만 — 서로 다른 메일이 우연히 겹칠 수 있음)
                    var olds = g.Where(e => !newSet.Contains(e)).ToList();
                    var oldHashes = new HashSet<string>(olds.Select(e => e.ContentHash).Where(h => h.Length > 0), StringComparer.OrdinalIgnoreCase);
                    var oldIds = new HashSet<string>(olds.Select(e => e.Mail.MessageId).Where(i => i.Length > 0), StringComparer.OrdinalIgnoreCase);
                    foreach (var n in g.Where(newSet.Contains))
                    {
                        if (n.ContentHash.Length == 0) continue; // 해시를 못 읽은 파일은 판단 보류
                        bool sameFile = oldHashes.Contains(n.ContentHash);
                        bool sameId = n.Mail.MessageId.Length > 0 && oldIds.Contains(n.Mail.MessageId);
                        if (!sameFile && !sameId) continue;
                        // 사용자가 휴지통에서 복원한 파일 (내용 또는 Message-ID로 기억)
                        if (loaded.TrashedHashes.Contains(n.ContentHash)
                            || (n.Mail.MessageId.Length > 0 && loaded.TrashedHashes.Contains(IdKey(n.Mail.MessageId)))) continue;
                        if (_trash.SendToRecycleBin(n.Mail.FilePath, out var err))
                        {
                            trashed.Add(n);
                            loaded.TrashedHashes.Add(n.ContentHash);
                            if (n.Mail.MessageId.Length > 0) loaded.TrashedHashes.Add(IdKey(n.Mail.MessageId));
                            result.AutoTrashed.Add(new KeyValuePair<string, string>(n.RelPath, existing.RelPath));
                            ActionLog.Write(root, "TRASH_DUPLICATE", n.RelPath, "기존 파일: " + existing.RelPath);
                        }
                        else
                        {
                            result.TrashFailures.Add($"{n.RelPath}: {err}");
                            ActionLog.Write(root, "TRASH_FAILED", n.RelPath, err ?? "");
                        }
                    }
                }
                current.RemoveAll(trashed.Contains);
                result.Added -= trashed.Count;
            }

            Duplicates.Recompute(current);
            current.Sort((a, b) => string.Compare(a.RelPath, b.RelPath, StringComparison.OrdinalIgnoreCase));
            result.Entries = current;
            result.Total = current.Count;
            result.Errors = current.Count(e => e.Mail.IsError);

            progress?.Report(new ScanProgress { Phase = "캐시 저장" });
            IndexStore.Save(root, current, loaded.TrashedHashes);
            result.Elapsed = sw.Elapsed;
            WriteScanLog(root, result);
            return result;
        }

        /// <summary>복원 방지 기록에 Message-ID를 넣을 때의 키 (해시와 섞이지 않게 접두어).</summary>
        private static string IdKey(string messageId) => "id:" + messageId.ToLowerInvariant();

        /// <summary><c>.emailindex\last-scan.log</c>: 마지막 스캔 요약과 오류 파일별 사유 (현장 진단용).</summary>
        private static void WriteScanLog(string root, ScanResult r)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 전체 {r.Total} 신규 {r.Added} 변경 {r.Changed} 이동 {r.Moved} " +
                              $"사라짐 {r.Removed} 그대로 {r.Unchanged} 오류 {r.Errors} 자동휴지통 {r.AutoTrashed.Count} {r.Elapsed.TotalSeconds:0.00}s");
                foreach (var e in r.Entries.Where(e => e.Mail.IsError).Take(500))
                    sb.AppendLine($"ERROR\t{e.RelPath}\t{e.Mail.ParseError}");
                foreach (var t in r.TrashFailures) sb.AppendLine("TRASH_FAILED\t" + t);
                File.WriteAllText(Path.Combine(IndexStore.IndexDir(root), "last-scan.log"), sb.ToString(), new System.Text.UTF8Encoding(true));
            }
            catch { }
        }

        /// <summary>하위 폴더 포함 .msg/.eml. 캐시 폴더와 접근 불가 폴더는 건너뛴다.</summary>
        public static IEnumerable<string> EnumerateMailFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                string[] subdirs, files;
                try
                {
                    files = Directory.GetFiles(dir);
                    subdirs = Directory.GetDirectories(dir);
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }

                foreach (var f in files)
                    if (MailReader.IsMailFile(f) && !Path.GetFileName(f).StartsWith("~eidx_", StringComparison.OrdinalIgnoreCase))
                        yield return f; // Outlook 백업 중인 임시 파일 제외
                foreach (var s in subdirs)
                    if (!Path.GetFileName(s).Equals(IndexStore.FolderName, StringComparison.OrdinalIgnoreCase))
                        stack.Push(s);
            }
        }

        /// <summary>하위 폴더 포함 패턴에 맞는 파일 (캐시 폴더·접근 불가 폴더 제외).</summary>
        public static IEnumerable<string> EnumerateAllFiles(string root, string pattern)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                string[] subdirs, files;
                try
                {
                    files = Directory.GetFiles(dir, pattern);
                    subdirs = Directory.GetDirectories(dir);
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                foreach (var f in files) yield return f;
                foreach (var s in subdirs)
                    if (!Path.GetFileName(s).Equals(IndexStore.FolderName, StringComparison.OrdinalIgnoreCase))
                        stack.Push(s);
            }
        }

        public static string Rel(string root, string full)
        {
            var r = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return r;
        }
    }
}

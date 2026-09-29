using System;
using EmailIndexer.Core.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.View;

namespace EmailIndexer.Core.Files
{
    public enum PlanStatus { Rename, Skip, Error }

    /// <summary>이름변경 계획 1건 (미리보기 표의 한 줄).</summary>
    public sealed class RenamePlan
    {
        public MailRow Row { get; set; } = null!;
        public string OldPath { get; set; } = "";
        public string NewPath { get; set; } = "";
        public PlanStatus Status { get; set; }
        public string Note { get; set; } = "";
        public string OldName => Path.GetFileName(OldPath);
        public string NewName => Path.GetFileName(NewPath);
    }

    public sealed class OpResult
    {
        public int Done { get; set; }
        public List<string> Failures { get; } = new List<string>();
        public override string ToString() => Failures.Count == 0 ? L.F("ops.done", Done) : L.F("ops.doneWithFailures", Done, Failures.Count);
    }

    /// <summary>
    /// 파일 관리 (명세 4·6·9장). 모든 작업은 <c>actions.log</c>에 기록하고,
    /// 삭제는 휴지통, 이름변경은 되돌리기 기록을 남긴다.
    /// 작업 후 캐시는 증분 스캔이 '이동'으로 인식해 갱신한다(내용 해시가 같으므로 다시 중복 처리되지 않음).
    /// </summary>
    public static class FileOps
    {
        public const string UndoPrefix = "rename-undo-";

        // ---------------- 파일명 정규화 ----------------

        public static List<RenamePlan> PlanRenames(IEnumerable<MailRow> rows)
        {
            var plans = new List<RenamePlan>();
            // 같은 폴더 안에서 이번 작업이 차지할 이름 (대소문자 무시)
            var taken = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows.OrderBy(x => x.Mail.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                var old = r.Mail.FilePath;
                var p = new RenamePlan { Row = r, OldPath = old, NewPath = old };
                plans.Add(p);
                if (r.Mail.IsError) { p.Status = PlanStatus.Skip; p.Note = L.T("plan.skip.error"); continue; }

                var dir = Path.GetDirectoryName(old) ?? "";
                var target = FileNameRule.BuildForDirectory(r, dir);
                if (target == null) { p.Status = PlanStatus.Error; p.Note = L.T("plan.error.pathTooLong"); continue; }
                if (FileNameRule.IsAlreadyAnyLanguage(Path.GetFileName(old), r, dir)) { p.Status = PlanStatus.Skip; p.Note = L.T("plan.skip.already"); continue; }

                if (!taken.TryGetValue(dir, out var used))
                    taken[dir] = used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var name = target;
                for (int n = 2; used.Contains(name) || ExistsOther(Path.Combine(dir, name), old); n++)
                {
                    name = FileNameRule.WithNumber(target, n);
                    if (n > 999) { name = ""; break; }
                }
                if (name.Length == 0) { p.Status = PlanStatus.Error; p.Note = L.T("plan.error.tooMany"); continue; }
                used.Add(name);
                p.NewPath = Path.Combine(dir, name);
                p.Status = PlanStatus.Rename;
                if (name != target) p.Note = L.T("plan.note.numbered");
                else if (target != FileNameRule.Build(r)) p.Note = L.T("plan.note.trimmed");
            }
            return plans;
        }

        private static bool ExistsOther(string path, string self)
            => !string.Equals(path, self, StringComparison.OrdinalIgnoreCase) && (File.Exists(path) || Directory.Exists(path));

        /// <summary>계획대로 이름을 바꾸고 되돌리기 기록을 남긴다.</summary>
        public static OpResult ApplyRenames(string root, IEnumerable<RenamePlan> plans)
        {
            var res = new OpResult();
            var undo = new StringBuilder();
            foreach (var p in plans.Where(x => x.Status == PlanStatus.Rename))
            {
                try
                {
                    if (File.Exists(p.NewPath)) throw new IOException(L.T("ops.err.targetExists"));
                    File.Move(p.OldPath, p.NewPath);
                    undo.Append(Rel(root, p.OldPath)).Append('\t').Append(Rel(root, p.NewPath)).Append('\n');
                    ActionLog.Write(root, "RENAME", Rel(root, p.OldPath), Rel(root, p.NewPath));
                    res.Done++;
                }
                catch (Exception ex)
                {
                    res.Failures.Add($"{p.OldName}: {ex.Message}");
                    ActionLog.Write(root, "RENAME_FAILED", Rel(root, p.OldPath), ex.Message);
                }
            }
            if (res.Done > 0)
            {
                var dir = IndexStore.IndexDir(root);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"{UndoPrefix}{DateTime.Now:yyyyMMdd-HHmmss-fff}.tsv"), undo.ToString(), new UTF8Encoding(false));
            }
            return res;
        }

        /// <summary>가장 최근 이름변경 묶음 (없으면 null).</summary>
        public static string? LatestUndo(string root)
        {
            var dir = IndexStore.IndexDir(root);
            if (!Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir, UndoPrefix + "*.tsv").OrderByDescending(f => f, StringComparer.Ordinal).FirstOrDefault();
        }

        public static int UndoCount(string undoFile)
            => File.ReadAllLines(undoFile, Encoding.UTF8).Count(l => l.IndexOf('\t') >= 0);

        /// <summary>
        /// 최근 이름변경 되돌리기: 바뀐 이름이 그대로 있고 원래 이름이 비어 있는 파일만 되돌린다.
        /// 끝나면 기록 파일을 '.done'으로 바꿔 두 번 되돌리지 않게 한다.
        /// </summary>
        public static OpResult UndoRenames(string root, string undoFile)
        {
            var res = new OpResult();
            foreach (var line in File.ReadAllLines(undoFile, Encoding.UTF8).Reverse())
            {
                var parts = line.Split('\t');
                if (parts.Length != 2) continue;
                var oldPath = Path.Combine(root, parts[0]);
                var newPath = Path.Combine(root, parts[1]);
                try
                {
                    if (!File.Exists(newPath)) throw new IOException(L.T("ops.err.renamedMissing"));
                    if (File.Exists(oldPath)) throw new IOException(L.T("ops.err.originalExists"));
                    File.Move(newPath, oldPath);
                    ActionLog.Write(root, "RENAME_UNDO", parts[1], parts[0]);
                    res.Done++;
                }
                catch (Exception ex) { res.Failures.Add($"{Path.GetFileName(newPath)}: {ex.Message}"); }
            }
            try { File.Move(undoFile, undoFile + ".done"); } catch { }
            return res;
        }

        // ---------------- 이동·삭제·중복 정리 ----------------

        /// <summary>선택 파일을 대상 폴더로 이동. 같은 이름이 있으면 번호를 붙인다.</summary>
        public static OpResult Move(string root, IEnumerable<MailRow> rows, string destDir)
        {
            var res = new OpResult();
            Directory.CreateDirectory(destDir);
            foreach (var r in rows)
            {
                var src = r.Mail.FilePath;
                try
                {
                    if (string.Equals(Path.GetFullPath(Path.GetDirectoryName(src) ?? "").TrimEnd('\\', '/'), Path.GetFullPath(destDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                        continue; // 이미 그 폴더
                    var name = Path.GetFileName(src);
                    var dest = Path.Combine(destDir, name);
                    for (int n = 2; File.Exists(dest); n++) dest = Path.Combine(destDir, FileNameRule.WithNumber(name, n));
                    if (dest.Length > FileNameRule.MaxPath) throw new PathTooLongException(L.T("ops.err.destTooLong"));
                    File.Move(src, dest);
                    ActionLog.Write(root, "MOVE", Rel(root, src), dest);
                    res.Done++;
                }
                catch (Exception ex)
                {
                    res.Failures.Add($"{Path.GetFileName(src)}: {ex.Message}");
                    ActionLog.Write(root, "MOVE_FAILED", Rel(root, src), ex.Message);
                }
            }
            return res;
        }

        public static OpResult Trash(string root, IEnumerable<MailRow> rows, ITrash trash, string action = "TRASH")
        {
            var res = new OpResult();
            foreach (var r in rows)
            {
                if (trash.SendToRecycleBin(r.Mail.FilePath, out var err))
                {
                    ActionLog.Write(root, action, r.Entry.RelPath, r.Entry.KeeperRelPath != null ? "kept: " + r.Entry.KeeperRelPath : "");
                    res.Done++;
                }
                else
                {
                    res.Failures.Add($"{r.FileName}: {err}");
                    ActionLog.Write(root, action + "_FAILED", r.Entry.RelPath, err ?? "");
                }
            }
            return res;
        }

        /// <summary>중복 정리 대상: '중복' 표시된 파일 (원본은 남김).</summary>
        public static List<MailRow> DuplicatesToRemove(IEnumerable<MailRow> rows)
            => rows.Where(r => r.Entry.Dup == DupStatus.Duplicate && r.Entry.KeeperRelPath != null).ToList();

        private static string Rel(string root, string full)
            => full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(root.Length).TrimStart('\\', '/')
                : full;
    }
}

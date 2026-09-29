using System;
using System.IO;
using System.Linq;
using System.Text;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.App
{
    /// <summary>
    /// 진단용: <c>EmailIndexer.exe --scan-test &lt;폴더&gt;</c> → 폴더의 메일을 읽어
    /// 결과를 <c>scan-test.txt</c>(폴더 안)에 기록. 화면 없이 엔진만 검증한다.
    /// </summary>
    internal static class ScanTest
    {
        public static int Run(string folder)
        {
            var sb = new StringBuilder();
            int ok = 0, err = 0;
            var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(MailReader.IsMailFile).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var f in files)
            {
                var m = MailReader.Read(f);
                var dir = MailClassifier.GetDirection(m, null);
                if (m.IsError) { err++; sb.AppendLine($"[Error] {Path.GetFileName(f)} :: {EmailIndexer.Core.View.Display.Error(m)}"); continue; }
                ok++;
                sb.AppendLine(string.Join(" | ",
                    Path.GetFileName(f),
                    MailClassifier.GetReferenceTime(m, dir).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    m.SenderName, m.Subject,
                    m.HasAttachments ? "Attachments(" + string.Join(",", m.AttachmentNames) + ")" : "No attachments",
                    m.Meeting.ToString(), m.FileSize + "B"));
            }
            sb.Insert(0, $"{files.Count} files, {ok} ok, {err} errors, {sw.ElapsedMilliseconds}ms{Environment.NewLine}");
            var report = sb.ToString();
            File.WriteAllText(Path.Combine(folder, "scan-test.txt"), report, new UTF8Encoding(true));
            Console.Out.Write(report);
            return err == 0 ? 0 : 1;
        }

        /// <summary>
        /// <c>--index-test &lt;폴더&gt;</c>: 캐시 생성·증분 스캔·중복 판정을 실행한다.
        /// 진단용이므로 파일은 절대 지우지 않는다(자동 휴지통 대상은 '보류'로만 기록).
        /// </summary>
        public static int RunIndex(string folder)
        {
            var trash = new DryRunTrash();
            var r = new EmailIndexer.Core.Index.Scanner(trash).Scan(folder);
            var sb = new StringBuilder();
            sb.AppendLine($"{(r.FirstScan ? "First scan" : "Incremental scan")} {r.Elapsed.TotalSeconds:F2}s: total {r.Total}, added {r.Added}, changed {r.Changed}, " +
                          $"removed {r.Removed}, moved {r.Moved}, unchanged {r.Unchanged}, errors {r.Errors}");
            foreach (var e in r.Entries.Where(e => e.Dup != EmailIndexer.Core.Index.DupStatus.None))
                sb.AppendLine($"[{e.Dup}] group{e.DupGroup} {e.RelPath}{(e.KeeperRelPath != null ? " -> keeper: " + e.KeeperRelPath : "")}");
            foreach (var t in r.TrashFailures) sb.AppendLine("[auto-delete skipped] " + t);
            var report = sb.ToString();
            File.WriteAllText(Path.Combine(folder, "index-test.txt"), report, new UTF8Encoding(true));
            Console.Out.Write(report);
            return 0;
        }

        private sealed class DryRunTrash : EmailIndexer.Core.Index.ITrash
        {
            public bool SendToRecycleBin(string path, out string? error)
            {
                error = "diagnostic mode: not deleted";
                return false;
            }
        }
    }
}

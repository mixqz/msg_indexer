using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.Core.Index
{
    /// <summary>
    /// Outlook 백업이 저장한 파일의 원본 폴더 방향 (<c>.emailindex\folder-hints.tsv</c>: 상대경로 → Received/Sent).
    /// 스캐너가 새 파일을 캐시에 넣을 때 적용한다. 이름변경·이동 후에는 캐시 항목이 방향을 이어받는다.
    /// </summary>
    public static class FolderHints
    {
        public const string FileName = "folder-hints.tsv";
        private static readonly object Gate = new object();

        public static Dictionary<string, MailDirection> Load(string root)
        {
            var map = new Dictionary<string, MailDirection>(StringComparer.OrdinalIgnoreCase);
            var path = Path.Combine(IndexStore.IndexDir(root), FileName);
            if (!File.Exists(path)) return map;
            try
            {
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    if (p.Length == 2 && Enum.TryParse(p[1], out MailDirection d)) map[Norm(p[0])] = d;
                }
            }
            catch { }
            return map;
        }

        public static void Append(string root, string relPath, MailDirection direction)
        {
            var dir = IndexStore.IndexDir(root);
            Directory.CreateDirectory(dir);
            lock (Gate) File.AppendAllText(Path.Combine(dir, FileName), $"{Norm(relPath)}\t{direction}\n", new UTF8Encoding(false));
        }

        public static string Norm(string rel) => rel.Replace('/', '\\');
    }
}

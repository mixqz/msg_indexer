using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.Core.Index
{
    /// <summary>중복 판정 (명세 6장). 오류 파일은 판정에서 제외.</summary>
    public static class Duplicates
    {
        private static readonly Regex NormalizedName = new Regex(
            @"^\d{6}_\d{6}_.+_첨부[OX](\s\(\d+\))?\.(msg|eml)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool LooksNormalized(string relPath) => NormalizedName.IsMatch(Path.GetFileName(relPath));

        /// <summary>'동일 메일' 판정 키. Message-ID가 없으면 발신주소+발신시각(초)+제목+본문 해시.</summary>
        public static string MailKey(MailInfo m)
        {
            if (!string.IsNullOrEmpty(m.MessageId)) return "id:" + m.MessageId;
            // 구별할 정보가 거의 없는 메일은 서로 묶지 않는다 (서로 다른 메일이 같은 지문이 되는 것 방지)
            if (m.SentTime == null && string.IsNullOrWhiteSpace(m.SenderEmail) && string.IsNullOrWhiteSpace(m.Subject))
                return "uniq:" + m.FilePath;
            var t = m.SentTime?.UtcDateTime.ToString("yyyyMMddHHmmss") ?? "-";
            return "fp:" + Sha256Hex(Encoding.UTF8.GetBytes(
                $"{m.SenderEmail.ToLowerInvariant()}|{t}|{m.Subject}|{Sha256Hex(Encoding.UTF8.GetBytes(m.BodyText ?? ""))}"));
        }

        /// <summary>남길 파일 우선순위: 정규화된 이름 > msg > 먼저 들어온 파일 > 수정시각 오래된 파일 > 경로.</summary>
        public static IOrderedEnumerable<IndexEntry> KeeperOrder(IEnumerable<IndexEntry> group)
            => group.OrderByDescending(e => LooksNormalized(e.RelPath))
                    .ThenBy(e => e.Mail.Format == MailFormat.Msg ? 0 : 1)
                    .ThenBy(e => e.FirstSeenUtc)
                    .ThenBy(e => e.Mail.FileModifiedUtc)
                    .ThenBy(e => e.RelPath, StringComparer.OrdinalIgnoreCase);

        /// <summary>전체 목록의 Dup/DupGroup/KeeperRelPath를 다시 계산한다.</summary>
        public static void Recompute(IList<IndexEntry> entries)
        {
            foreach (var e in entries) { e.Dup = DupStatus.None; e.DupGroup = 0; e.KeeperRelPath = null; }
            var valid = entries.Where(e => !e.Mail.IsError).ToList();
            int gid = 0;

            // 1) 동일 파일(해시) 또는 동일 메일(키) — 두 기준을 합쳐 연결된 묶음 (Union-Find)
            var parent = new Dictionary<IndexEntry, IndexEntry>();
            IndexEntry Find(IndexEntry x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            void Union(IndexEntry a, IndexEntry b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }
            foreach (var e in valid) parent[e] = e;
            foreach (var g in valid.Where(e => e.ContentHash.Length > 0).GroupBy(e => e.ContentHash))
                LinkAll(g, Union);
            foreach (var g in valid.GroupBy(e => MailKey(e.Mail)))
                LinkAll(g, Union);

            foreach (var group in valid.GroupBy(Find).Where(g => g.Count() > 1))
            {
                gid++;
                var ordered = KeeperOrder(group).ToList();
                var keeper = ordered[0];
                keeper.Dup = DupStatus.Keeper;
                keeper.DupGroup = gid;
                foreach (var d in ordered.Skip(1))
                {
                    d.Dup = DupStatus.Duplicate;
                    d.DupGroup = gid;
                    d.KeeperRelPath = keeper.RelPath;
                }
            }

            // 2) 유사 메일: 발신자·제목 같고 기준 시각 1분 이내, 위 묶음에 속하지 않은 것
            var rest = valid.Where(e => e.Dup == DupStatus.None);
            foreach (var g in rest.GroupBy(e => (e.Mail.SenderEmail.ToLowerInvariant() + "|" + e.Mail.Subject.Trim().ToLowerInvariant())))
            {
                var sorted = g.Select(e => (e, t: SortTime(e.Mail))).OrderBy(x => x.t).ToList();
                for (int i = 0; i < sorted.Count;)
                {
                    int j = i;
                    while (j + 1 < sorted.Count && (sorted[j + 1].t - sorted[j].t) <= TimeSpan.FromMinutes(1)) j++;
                    if (j > i)
                    {
                        gid++;
                        for (int k = i; k <= j; k++) { sorted[k].e.Dup = DupStatus.Similar; sorted[k].e.DupGroup = gid; }
                    }
                    i = j + 1;
                }
            }
        }

        private static void LinkAll(IEnumerable<IndexEntry> g, Action<IndexEntry, IndexEntry> union)
        {
            IndexEntry? first = null;
            foreach (var e in g) { if (first == null) first = e; else union(first, e); }
        }

        private static DateTimeOffset SortTime(MailInfo m)
            => m.SentTime ?? m.ReceivedTime ?? new DateTimeOffset(DateTime.SpecifyKind(m.FileModifiedUtc, DateTimeKind.Utc));

        public static string Sha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(data));
        }

        public static string FileSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            return ToHex(sha.ComputeHash(fs));
        }

        private static string ToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}

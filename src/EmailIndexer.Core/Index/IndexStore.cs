using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.Core.Index
{
    /// <summary>
    /// 백업 폴더 안 <c>.emailindex\index.bin</c> (GZip 압축 바이너리) 읽기/쓰기.
    /// 손상·버전 불일치 시 빈 캐시로 시작한다(= 첫 스캔처럼 동작, 자동 삭제 없음).
    /// </summary>
    public static class IndexStore
    {
        public const string FolderName = ".emailindex";
        public const string FileName = "index.bin";
        private const int Magic = 0x58444945; // "EIDX"
        private const int Version = 1;
        /// <summary>검색용 본문은 메일당 이 길이까지만 저장.</summary>
        public const int MaxBodyChars = 64 * 1024;

        public static string IndexDir(string root) => Path.Combine(root, FolderName);
        public static string IndexPath(string root) => Path.Combine(IndexDir(root), FileName);

        public sealed class LoadResult
        {
            public List<IndexEntry> Entries { get; set; } = new List<IndexEntry>();
            /// <summary>캐시가 원래 있었는지 (false면 첫 스캔).</summary>
            public bool Existed { get; set; }
            /// <summary>있었지만 손상/버전 불일치로 버렸는지.</summary>
            public bool WasCorrupt { get; set; }
            /// <summary>자동 휴지통으로 보낸 적 있는 파일 해시 (복원하면 다시 지우지 않기 위해).</summary>
            public HashSet<string> TrashedHashes { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static LoadResult Load(string root)
        {
            var path = IndexPath(root);
            if (!File.Exists(path)) return new LoadResult();
            try
            {
                using var fs = File.OpenRead(path);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var r = new BinaryReader(gz, Encoding.UTF8);
                if (r.ReadInt32() != Magic || r.ReadInt32() != Version)
                    return new LoadResult { WasCorrupt = true };
                int n = r.ReadInt32();
                var list = new List<IndexEntry>(n);
                for (int i = 0; i < n; i++) list.Add(ReadEntry(r, root));
                var trashed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int t = r.ReadInt32();
                for (int i = 0; i < t; i++) trashed.Add(r.ReadString());
                return new LoadResult { Entries = list, Existed = true, TrashedHashes = trashed };
            }
            catch (Exception)
            {
                return new LoadResult { WasCorrupt = true };
            }
        }

        /// <summary>임시 파일에 쓴 뒤 교체 → 저장 중 꺼져도 기존 캐시가 깨지지 않음.</summary>
        public static void Save(string root, IReadOnlyCollection<IndexEntry> entries, ICollection<string>? trashedHashes = null)
        {
            var dir = IndexDir(root);
            var di = Directory.CreateDirectory(dir);
            try { di.Attributes |= FileAttributes.Hidden; } catch { /* 숨김 실패는 무시 */ }

            var path = IndexPath(root);
            var tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
            using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            using (var w = new BinaryWriter(gz, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(entries.Count);
                foreach (var e in entries) WriteEntry(w, e);
                w.Write(trashedHashes?.Count ?? 0);
                if (trashedHashes != null) foreach (var h in trashedHashes) w.Write(h);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        private static void WriteEntry(BinaryWriter w, IndexEntry e)
        {
            var m = e.Mail;
            w.Write(e.RelPath);
            w.Write(e.ContentHash ?? "");
            w.Write(e.FirstSeenUtc.Ticks);
            w.Write(e.FolderHint.HasValue ? (int)e.FolderHint.Value : -1);
            w.Write((int)m.Format);
            w.Write(m.FileSize);
            w.Write(m.FileModifiedUtc.Ticks);
            WriteTime(w, m.SentTime);
            WriteTime(w, m.ReceivedTime);
            w.Write(m.SenderName ?? "");
            w.Write(m.SenderEmail ?? "");
            WriteList(w, m.To);
            WriteList(w, m.Cc);
            w.Write(m.Subject ?? "");
            w.Write(m.MessageId ?? "");
            WriteList(w, m.AttachmentNames);
            w.Write((int)m.Meeting);
            var body = m.BodyText ?? "";
            w.Write(body.Length > MaxBodyChars ? body.Substring(0, MaxBodyChars) : body);
            w.Write(m.ParseError != null);
            if (m.ParseError != null) w.Write(m.ParseError);
        }

        private static IndexEntry ReadEntry(BinaryReader r, string root)
        {
            var e = new IndexEntry
            {
                RelPath = r.ReadString(),
                ContentHash = r.ReadString(),
                FirstSeenUtc = new DateTime(r.ReadInt64(), DateTimeKind.Utc),
            };
            var hint = r.ReadInt32();
            e.FolderHint = hint < 0 ? (MailDirection?)null : (MailDirection)hint;
            var m = e.Mail;
            m.FilePath = Path.Combine(root, e.RelPath);
            m.Format = (MailFormat)r.ReadInt32();
            m.FileSize = r.ReadInt64();
            m.FileModifiedUtc = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
            m.SentTime = ReadTime(r);
            m.ReceivedTime = ReadTime(r);
            m.SenderName = r.ReadString();
            m.SenderEmail = r.ReadString();
            m.To = ReadList(r);
            m.Cc = ReadList(r);
            m.Subject = r.ReadString();
            m.MessageId = r.ReadString();
            m.AttachmentNames = ReadList(r);
            m.Meeting = (MeetingKind)r.ReadInt32();
            m.BodyText = r.ReadString();
            m.ParseError = r.ReadBoolean() ? r.ReadString() : null;
            return e;
        }

        private static void WriteTime(BinaryWriter w, DateTimeOffset? t)
        {
            w.Write(t.HasValue);
            if (!t.HasValue) return;
            w.Write(t.Value.UtcTicks);
            w.Write((short)t.Value.Offset.TotalMinutes);
        }

        private static DateTimeOffset? ReadTime(BinaryReader r)
        {
            if (!r.ReadBoolean()) return null;
            var utc = r.ReadInt64();
            var off = TimeSpan.FromMinutes(r.ReadInt16());
            return new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(off);
        }

        private static void WriteList(BinaryWriter w, List<string> list)
        {
            w.Write(list.Count);
            foreach (var s in list) w.Write(s ?? "");
        }

        private static List<string> ReadList(BinaryReader r)
        {
            int n = r.ReadInt32();
            var l = new List<string>(n);
            for (int i = 0; i < n; i++) l.Add(r.ReadString());
            return l;
        }
    }
}

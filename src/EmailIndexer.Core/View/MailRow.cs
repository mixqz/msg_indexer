using System;
using EmailIndexer.Core.Text;
using System.Collections.Generic;
using System.IO;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;

namespace EmailIndexer.Core.View
{
    /// <summary>목록 1행: 캐시 항목 + 화면용 계산값.</summary>
    public sealed class MailRow
    {
        public IndexEntry Entry { get; }
        public MailInfo Mail => Entry.Mail;
        public MailDirection Direction { get; private set; }
        /// <summary>기준 시각(받음=수신, 보냄=발신)의 PC 현지 시간.</summary>
        public DateTime LocalTime { get; private set; }
        /// <summary>상대 경로의 폴더 부분 ('' = 최상위). 구분자는 '\'로 통일.</summary>
        public string Folder { get; }
        public bool Normalized { get; }

        public MailRow(IndexEntry entry, ICollection<string>? myAddresses)
        {
            Entry = entry;
            var rel = entry.RelPath.Replace('/', '\\');
            var cut = rel.LastIndexOf('\\');
            Folder = cut < 0 ? "" : rel.Substring(0, cut);
            FileName = cut < 0 ? rel : rel.Substring(cut + 1);
            Normalized = Duplicates.LooksNormalized(FileName);
            Recalc(myAddresses);
        }

        /// <summary>'내 주소' 설정이 바뀌면 방향·기준 시각을 다시 계산.</summary>
        public void Recalc(ICollection<string>? myAddresses)
        {
            Direction = MailClassifier.GetDirection(Mail, myAddresses, Entry.FolderHint);
            LocalTime = MailClassifier.GetReferenceTime(Mail, Direction).LocalDateTime;
        }

        public string FileName { get; }
    }

    /// <summary>화면 표시용 한글 문자열.</summary>
    public static class Display
    {
        public static string Meeting(MeetingKind k)
        {
            switch (k)
            {
                case MeetingKind.Request: return L.T("meeting.request");
                case MeetingKind.Accepted: return L.T("meeting.accepted");
                case MeetingKind.Declined: return L.T("meeting.declined");
                case MeetingKind.Tentative: return L.T("meeting.tentative");
                case MeetingKind.Canceled: return L.T("meeting.canceled");
                case MeetingKind.Other: return L.T("meeting.other");
                default: return "";
            }
        }

        public static string Direction(MailDirection d) => L.T(d == MailDirection.Sent ? "dir.sent" : "dir.received");

        public static string Status(MailRow r)
        {
            if (r.Mail.IsError) return L.T("status.error");
            switch (r.Entry.Dup)
            {
                case DupStatus.Duplicate: return L.T("status.duplicate");
                case DupStatus.Keeper: return L.T("status.keeper");
                case DupStatus.Similar: return L.T("status.similar");
            }
            return r.Normalized ? L.T("status.normalized") : "";
        }

        /// <summary>읽기 오류 사유 (캐시에는 "코드\t상세"로 저장, 표시할 때 현재 언어로).</summary>
        public static string Error(MailInfo m)
        {
            var e = m.ParseError;
            if (string.IsNullOrEmpty(e)) return "";
            var tab = e!.IndexOf('\t');
            if (tab < 0) return e.StartsWith("err.") ? L.T(e) : e; // 이전 버전 캐시의 문장은 그대로
            return L.F(e.Substring(0, tab), e.Substring(tab + 1));
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(L.Culture) + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("#,0", L.Culture) + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("#,0.0", L.Culture) + " MB";
        }
    }
}

using System;
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
                case MeetingKind.Request: return "초대";
                case MeetingKind.Accepted: return "수락";
                case MeetingKind.Declined: return "거절";
                case MeetingKind.Tentative: return "미정";
                case MeetingKind.Canceled: return "취소";
                case MeetingKind.Other: return "일정";
                default: return "";
            }
        }

        public static string Direction(MailDirection d) => d == MailDirection.Sent ? "보냄" : "받음";

        public static string Status(MailRow r)
        {
            if (r.Mail.IsError) return "오류";
            switch (r.Entry.Dup)
            {
                case DupStatus.Duplicate: return "중복";
                case DupStatus.Keeper: return "중복(원본)";
                case DupStatus.Similar: return "유사";
            }
            return r.Normalized ? "정규화됨" : "";
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("#,0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("#,0.0") + " MB";
        }
    }
}

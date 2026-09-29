using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Text;

namespace EmailIndexer.Core.View
{
    public enum SearchScope { All, Subject, People, Body, Attachments }

    public enum StatusFlag { Duplicate, Similar, Error, NotNormalized }

    public enum SortColumn { Time, Direction, Sender, Subject, Attach, Meeting, Size, Status, Folder }

    /// <summary>
    /// 검색 + 필터 조건 (명세 7장). 각 그룹은 아무것도 고르지 않으면 '전체'.
    /// 그룹 안은 OR, 그룹끼리는 AND.
    /// </summary>
    public sealed class MailFilter
    {
        /// <summary>현지 날짜 기준 시작일 (포함).</summary>
        public DateTime? DateFrom { get; set; }
        /// <summary>현지 날짜 기준 종료일 (그날 끝까지 포함).</summary>
        public DateTime? DateTo { get; set; }
        public HashSet<MailDirection> Directions { get; } = new HashSet<MailDirection>();
        /// <summary>null = 전체, true = 첨부 있음, false = 없음.</summary>
        public bool? HasAttachments { get; set; }
        public HashSet<MeetingKind> Meetings { get; } = new HashSet<MeetingKind>();
        public HashSet<StatusFlag> Statuses { get; } = new HashSet<StatusFlag>();
        public HashSet<MailFormat> Formats { get; } = new HashSet<MailFormat>();
        /// <summary>이 폴더와 하위 폴더만 ('' 또는 null = 전체). 구분자 '\'.</summary>
        public string? Folder { get; set; }
        public string? SenderEmail { get; set; }

        public string Search { get; set; } = "";
        public SearchScope Scope { get; set; } = SearchScope.All;

        public bool IsEmpty =>
            DateFrom == null && DateTo == null && Directions.Count == 0 && HasAttachments == null
            && Meetings.Count == 0 && Statuses.Count == 0 && Formats.Count == 0
            && string.IsNullOrEmpty(Folder) && string.IsNullOrEmpty(SenderEmail) && string.IsNullOrWhiteSpace(Search);

        public void Clear()
        {
            DateFrom = DateTo = null;
            Directions.Clear(); HasAttachments = null; Meetings.Clear(); Statuses.Clear(); Formats.Clear();
            Folder = null; SenderEmail = null; Search = ""; Scope = SearchScope.All;
        }

        public List<MailRow> Apply(IEnumerable<MailRow> rows)
        {
            var terms = Tokenize(Search);
            return rows.Where(r => Matches(r, terms)).ToList();
        }

        public bool Matches(MailRow r) => Matches(r, Tokenize(Search));

        private bool Matches(MailRow r, List<string> terms)
        {
            var m = r.Mail;
            if (DateFrom.HasValue && r.LocalTime < DateFrom.Value.Date) return false;
            if (DateTo.HasValue && r.LocalTime >= DateTo.Value.Date.AddDays(1)) return false;
            if (Directions.Count > 0 && !Directions.Contains(r.Direction)) return false;
            if (HasAttachments.HasValue && m.HasAttachments != HasAttachments.Value) return false;
            if (Meetings.Count > 0 && !Meetings.Contains(m.Meeting)) return false;
            if (Formats.Count > 0 && !Formats.Contains(m.Format)) return false;
            if (Statuses.Count > 0 && !Statuses.Any(s => HasStatus(r, s))) return false;
            if (!string.IsNullOrEmpty(Folder))
            {
                var f = Folder!.TrimEnd('\\');
                if (!(r.Folder.Equals(f, StringComparison.OrdinalIgnoreCase)
                      || r.Folder.StartsWith(f + "\\", StringComparison.OrdinalIgnoreCase))) return false;
            }
            if (!string.IsNullOrEmpty(SenderEmail)
                && !string.Equals(Sender(m), SenderEmail, StringComparison.OrdinalIgnoreCase)) return false;

            foreach (var t in terms)
                if (!ContainsTerm(r, t)) return false;
            return true;
        }

        public static bool HasStatus(MailRow r, StatusFlag s)
        {
            switch (s)
            {
                case StatusFlag.Duplicate: return r.Entry.Dup == DupStatus.Duplicate || r.Entry.Dup == DupStatus.Keeper;
                case StatusFlag.Similar: return r.Entry.Dup == DupStatus.Similar;
                case StatusFlag.Error: return r.Mail.IsError;
                case StatusFlag.NotNormalized: return !r.Normalized && !r.Mail.IsError;
                default: return false;
            }
        }

        /// <summary>발신자 필터·집계용 키 (주소가 없으면 이름).</summary>
        public static string Sender(MailInfo m) => string.IsNullOrEmpty(m.SenderEmail) ? m.SenderName : m.SenderEmail;

        private bool ContainsTerm(MailRow r, string t)
        {
            var m = r.Mail;
            bool all = Scope == SearchScope.All;
            if ((all || Scope == SearchScope.Subject) && Has(m.Subject, t)) return true;
            if (all || Scope == SearchScope.People)
            {
                if (Has(m.SenderName, t) || Has(m.SenderEmail, t)) return true;
                if (m.To.Any(x => Has(x, t)) || m.Cc.Any(x => Has(x, t))) return true;
            }
            if ((all || Scope == SearchScope.Attachments) && m.AttachmentNames.Any(x => Has(x, t))) return true;
            if (all && Has(r.FileName, t)) return true;
            if ((all || Scope == SearchScope.Body) && Has(m.BodyText, t)) return true;
            return false;
        }

        private static bool Has(string? s, string t) => s != null && s.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>공백으로 나눈 단어들(AND). "따옴표"는 한 구문으로.</summary>
        public static List<string> Tokenize(string? q)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(q)) return list;
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (var ch in q!)
            {
                if (ch == '"') { Flush(); quoted = !quoted; continue; }
                if (!quoted && char.IsWhiteSpace(ch)) { Flush(); continue; }
                sb.Append(ch);
            }
            Flush();
            return list;

            void Flush()
            {
                var s = sb.ToString().Trim();
                if (s.Length > 0) list.Add(s);
                sb.Clear();
            }
        }

        /// <summary>적용 중인 조건을 짧은 한국어 설명으로 (화면 '적용:' 줄).</summary>
        public List<string> Describe()
        {
            var d = new List<string>();
            if (DateFrom.HasValue || DateTo.HasValue)
                d.Add(L.F("filter.dateRange",
                    DateFrom?.ToString("d", L.Culture) ?? L.T("filter.dateStart"),
                    DateTo?.ToString("d", L.Culture) ?? L.T("filter.dateNow")));
            if (Directions.Count == 1) d.Add(Display.Direction(Directions.First()));
            if (HasAttachments.HasValue) d.Add(L.T(HasAttachments.Value ? "filter.attYes" : "filter.attNo"));
            if (Meetings.Count > 0) d.Add(string.Join("/", Meetings.Select(Display.Meeting)));
            if (Statuses.Count > 0) d.Add(string.Join("/", Statuses.Select(StatusName)));
            if (Formats.Count == 1) d.Add(Formats.First() == MailFormat.Msg ? "msg" : "eml");
            if (!string.IsNullOrEmpty(Folder)) d.Add(L.F("filter.folder", Folder));
            if (!string.IsNullOrEmpty(SenderEmail)) d.Add(L.F("filter.sender", SenderEmail));
            if (!string.IsNullOrWhiteSpace(Search))
                d.Add(Scope == SearchScope.All ? L.F("filter.search", Search.Trim()) : L.F("filter.searchScoped", Search.Trim(), ScopeName(Scope)));
            return d;
        }

        public static string StatusName(StatusFlag s)
            => L.T(s == StatusFlag.Duplicate ? "status.duplicate" : s == StatusFlag.Similar ? "status.similar"
                 : s == StatusFlag.Error ? "status.error" : "status.notNormalized");

        public static string ScopeName(SearchScope s)
            => L.T(s == SearchScope.Subject ? "scope.subject" : s == SearchScope.People ? "scope.people"
                 : s == SearchScope.Body ? "scope.body" : s == SearchScope.Attachments ? "scope.attachments" : "scope.all");
    }

    public static class MailSort
    {
        public static Comparison<MailRow> By(SortColumn col, bool ascending)
        {
            Comparison<MailRow> c;
            switch (col)
            {
                case SortColumn.Direction: c = (a, b) => a.Direction.CompareTo(b.Direction); break;
                case SortColumn.Sender: c = (a, b) => Cmp(a.Mail.SenderName, b.Mail.SenderName); break;
                case SortColumn.Subject: c = (a, b) => Cmp(a.Mail.Subject, b.Mail.Subject); break;
                case SortColumn.Attach: c = (a, b) => a.Mail.AttachmentNames.Count.CompareTo(b.Mail.AttachmentNames.Count); break;
                case SortColumn.Meeting: c = (a, b) => a.Mail.Meeting.CompareTo(b.Mail.Meeting); break;
                case SortColumn.Size: c = (a, b) => a.Mail.FileSize.CompareTo(b.Mail.FileSize); break;
                case SortColumn.Status: c = (a, b) => Cmp(Display.Status(a), Display.Status(b)); break;
                case SortColumn.Folder: c = (a, b) => Cmp(a.Folder, b.Folder); break;
                default: c = (a, b) => a.LocalTime.CompareTo(b.LocalTime); break;
            }
            // 같은 값이면 시각 최신순 → 경로로 안정 정렬
            Comparison<MailRow> tie = (a, b) =>
            {
                var t = b.LocalTime.CompareTo(a.LocalTime);
                return t != 0 ? t : Cmp(a.Entry.RelPath, b.Entry.RelPath);
            };
            return (a, b) =>
            {
                var r = c(a, b);
                if (!ascending) r = -r;
                return r != 0 ? r : tie(a, b);
            };
        }

        private static int Cmp(string? a, string? b) => string.Compare(a ?? "", b ?? "", StringComparison.CurrentCultureIgnoreCase);

        /// <summary>발신자별 메일 수 상위 N (필터 패널 'TOP 발신자').</summary>
        public static List<(string key, string name, int count)> TopSenders(IEnumerable<MailRow> rows, int n)
            => rows.Where(r => !r.Mail.IsError)
                   .GroupBy(r => MailFilter.Sender(r.Mail), StringComparer.OrdinalIgnoreCase)
                   .Select(g => (key: g.Key, name: g.First().Mail.SenderName, count: g.Count()))
                   .OrderByDescending(x => x.count).ThenBy(x => x.name)
                   .Take(n).ToList();
    }
}

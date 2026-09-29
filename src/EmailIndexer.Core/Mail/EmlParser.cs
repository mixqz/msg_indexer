using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EmailIndexer.Core.Text;
using MimeKit;
using MimeKit.Tnef;
using MimeKit.Utils;

namespace EmailIndexer.Core.Mail
{
    internal static class EmlParser
    {
        private static readonly ParserOptions Options = CreateOptions();

        private static ParserOptions CreateOptions()
        {
            var o = ParserOptions.Default.Clone();
            // 문자셋 표기 없이 8bit로 들어온 헤더·본문 대비: PC 지역 설정의 ANSI 코드 페이지
            // (한국어 949, 일본어 932, 중국어 간체 936, 서유럽 1252 …). UTF-8은 MimeKit이 먼저 시도한다.
            try { o.CharsetEncoding = Encoding.GetEncoding(FallbackCodePage(CultureInfo.CurrentCulture.Name)); }
            catch { /* 환경에 없으면 기본값 */ }
            return o;
        }

        /// <summary>
        /// 문자셋 표기가 없는 오래된 메일에 쓸 대체 코드 페이지 = 해당 지역 Windows의 ANSI 코드 페이지.
        /// 문화권 데이터가 없는 환경에서도 같게 동작하도록 표로 고정. 모르는 지역은 1252(서유럽).
        /// </summary>
        internal static int FallbackCodePage(string? cultureName)
        {
            var name = (cultureName ?? "").ToLowerInvariant();
            var lang = name.Split('-')[0];
            switch (lang)
            {
                case "ko": return 949;
                case "ja": return 932;
                case "zh":
                    return name.Contains("tw") || name.Contains("hk") || name.Contains("mo") || name.Contains("hant") ? 950 : 936;
                case "th": return 874;
                case "vi": return 1258;
                case "ru": case "uk": case "be": case "bg": case "mk": case "kk": return 1251;
                case "sr": return name.Contains("latn") ? 1250 : 1251;
                case "pl": case "cs": case "sk": case "hu": case "sl": case "hr": case "ro": case "sq": case "bs": return 1250;
                case "el": return 1253;
                case "tr": case "az": return 1254;
                case "he": return 1255;
                case "ar": case "fa": case "ur": return 1256;
                case "lt": case "lv": case "et": return 1257;
                default: return 1252;
            }
        }

        public static void Fill(string path, MailInfo info)
        {
            var msg = MimeMessage.Load(Options, path);
            if (msg.From.Count == 0 && msg.Sender == null && !msg.Headers.Contains(HeaderId.Date)
                && !msg.Headers.Contains(HeaderId.Subject))
                throw new System.IO.InvalidDataException("err.notmail");

            info.Subject = msg.Subject ?? "";
            info.MessageId = msg.MessageId ?? "";

            var from = msg.From.Mailboxes.FirstOrDefault() ?? msg.Sender;
            if (from != null)
            {
                info.SenderName = from.Name ?? "";
                info.SenderEmail = from.Address ?? "";
            }
            info.To = msg.To.Mailboxes.Select(Format).ToList();
            info.Cc = msg.Cc.Mailboxes.Select(Format).ToList();

            if (msg.Headers.Contains(HeaderId.Date)) info.SentTime = msg.Date;
            info.ReceivedTime = TopReceivedTime(msg.Headers);

            var html = msg.HtmlBody;
            var text = msg.TextBody;
            info.BodyText = !string.IsNullOrWhiteSpace(text) ? text! : HtmlText.ToPlainText(html);

            var cidRefs = HtmlText.CidReferences(html);
            foreach (var entity in msg.BodyParts)
                Classify(entity, cidRefs, info);
        }

        private static string Format(MailboxAddress m)
            => string.IsNullOrEmpty(m.Name) ? m.Address : $"{m.Name} <{m.Address}>";

        /// <summary>가장 위(마지막으로 거친 서버)의 Received 헤더에서 수신 시각을 얻는다.</summary>
        internal static DateTimeOffset? TopReceivedTime(HeaderList headers)
        {
            var top = headers.FirstOrDefault(h => h.Id == HeaderId.Received);
            if (top == null) return null;
            var value = top.Value ?? "";
            var semi = value.LastIndexOf(';');
            if (semi < 0) return null;
            return DateUtils.TryParse(value.Substring(semi + 1).Trim(), out var dt) ? dt : (DateTimeOffset?)null;
        }

        private static void Classify(MimeEntity entity, HashSet<string> cidRefs, MailInfo info)
        {
            if (entity is MessagePart mp)
            {
                var sub = mp.Message?.Subject;
                MailReader.AddUnique(info.AttachmentNames,
                    mp.ContentDisposition?.FileName ?? (string.IsNullOrEmpty(sub) ? "attached-message.eml" : sub + ".eml"));
                return;
            }
            if (!(entity is MimePart part)) return;

            if (part.ContentType.IsMimeType("text", "calendar"))
            {
                if (info.Meeting == MeetingKind.None) info.Meeting = MeetingFromCalendar(part);
                return; // 일정 데이터는 첨부로 세지 않음 (Outlook도 첨부로 표시하지 않음)
            }

            if (part is TnefPart tnef)
            {
                // Outlook이 보낸 winmail.dat: 안에 든 실제 첨부를 꺼내 판정
                foreach (var inner in tnef.ExtractAttachments())
                    if (inner is MimePart ip && IsRealAttachment(ip, cidRefs))
                        MailReader.AddUnique(info.AttachmentNames, ip.FileName ?? "attachment");
                return;
            }

            if (IsRealAttachment(part, cidRefs))
                MailReader.AddUnique(info.AttachmentNames, part.FileName ?? "attachment");
        }

        internal static bool IsRealAttachment(MimePart part, HashSet<string> cidRefs)
        {
            var cid = part.ContentId;
            if (!string.IsNullOrEmpty(cid) && cidRefs.Contains(cid!.Trim('<', '>'))) return false; // 본문 삽입 이미지

            bool isBodyText = (part.ContentType.IsMimeType("text", "plain") || part.ContentType.IsMimeType("text", "html"))
                              && string.IsNullOrEmpty(part.FileName);
            if (part.IsAttachment) return true; // Content-Disposition: attachment
            if (isBodyText) return false;
            return !string.IsNullOrEmpty(part.FileName);
        }

        internal static MeetingKind MeetingFromCalendar(MimePart part)
        {
            string ics;
            try
            {
                ics = part is TextPart tp ? tp.Text : ReadAll(part);
            }
            catch { ics = ""; }

            var method = part.ContentType.Parameters.TryGetValue("method", out string? m) ? m : null;
            if (string.IsNullOrEmpty(method))
            {
                var mm = Regex.Match(ics, @"^METHOD:(\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                method = mm.Success ? mm.Groups[1].Value : "";
            }
            return MeetingFromIcs(method!, ics);
        }

        internal static MeetingKind MeetingFromIcs(string method, string ics)
        {
            switch ((method ?? "").Trim().ToUpperInvariant())
            {
                case "REQUEST": return MeetingKind.Request;
                case "CANCEL": return MeetingKind.Canceled;
                case "REPLY":
                    var ps = Regex.Match(ics ?? "", @"PARTSTAT=([A-Z\-]+)", RegexOptions.IgnoreCase);
                    switch (ps.Success ? ps.Groups[1].Value.ToUpperInvariant() : "")
                    {
                        case "ACCEPTED": return MeetingKind.Accepted;
                        case "DECLINED": return MeetingKind.Declined;
                        case "TENTATIVE": return MeetingKind.Tentative;
                        default: return MeetingKind.Other;
                    }
                default: return MeetingKind.Other; // COUNTER, PUBLISH 등
            }
        }

        private static string ReadAll(MimePart part)
        {
            using var ms = new System.IO.MemoryStream();
            part.Content?.DecodeTo(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}

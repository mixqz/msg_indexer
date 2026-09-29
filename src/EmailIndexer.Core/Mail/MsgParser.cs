using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using EmailIndexer.Core.Text;
using MsgReader.Outlook;

namespace EmailIndexer.Core.Mail
{
    internal static class MsgParser
    {
        public static void Fill(string path, MailInfo info)
        {
            using var msg = new Storage.Message(path, FileAccess.Read);

            info.Subject = msg.Subject ?? "";
            info.SentTime = msg.SentOn;
            info.ReceivedTime = msg.ReceivedOn;
            info.Meeting = MeetingFromType(msg.Type);

            var headers = msg.TransportMessageHeaders ?? "";
            info.MessageId = !string.IsNullOrWhiteSpace(msg.Id) ? msg.Id : HeaderValue(headers, "Message-ID");

            info.SenderName = msg.Sender?.DisplayName ?? "";
            info.SenderEmail = msg.Sender?.Email ?? "";
            if (!info.SenderEmail.Contains("@"))
            {
                // 사내 Exchange 메일은 "/O=EXCHANGELABS/..." 형식 → 인터넷 헤더의 SMTP 주소로 보정
                var smtp = Regex.Match(HeaderValue(headers, "From"), @"[\w.+\-']+@[\w\-]+(\.[\w\-]+)+");
                if (smtp.Success) info.SenderEmail = smtp.Value;
            }

            foreach (var r in msg.Recipients)
            {
                var s = string.IsNullOrEmpty(r.DisplayName) || r.DisplayName == r.Email
                    ? r.Email ?? "" : $"{r.DisplayName} <{r.Email}>";
                if (r.Type == RecipientType.Cc) MailReader.AddUnique(info.Cc, s);
                else if (r.Type == RecipientType.To || r.Type == null) MailReader.AddUnique(info.To, s);
            }

            string? html = null;
            string Html() => html ??= SafeHtml(msg);

            info.BodyText = msg.BodyText ?? "";
            if (string.IsNullOrWhiteSpace(info.BodyText)) info.BodyText = HtmlText.ToPlainText(Html());

            HashSet<string>? cidRefs = null;
            foreach (var obj in msg.Attachments)
            {
                if (obj is Storage.Message embedded)
                {
                    var n = embedded.FileName;
                    MailReader.AddUnique(info.AttachmentNames,
                        !string.IsNullOrEmpty(n) ? n : (embedded.Subject ?? "attached-message") + ".msg");
                    continue;
                }
                if (!(obj is Storage.Attachment a)) continue;
                if (a.IsContactPhoto || a.Hidden || a.IsInline) continue;
                if (!string.IsNullOrEmpty(a.ContentId))
                {
                    cidRefs ??= HtmlText.CidReferences(Html());
                    if (cidRefs.Contains(a.ContentId.Trim('<', '>'))) continue; // 본문 삽입 이미지
                }
                MailReader.AddUnique(info.AttachmentNames, string.IsNullOrEmpty(a.FileName) ? "attachment" : a.FileName);
            }
        }

        private static string SafeHtml(Storage.Message msg)
        {
            try { return msg.BodyHtml ?? ""; }
            catch { return ""; } // 손상된 RTF 본문이어도 메타데이터는 살린다
        }

        internal static MeetingKind MeetingFromType(MessageType type)
        {
            switch (type)
            {
                case MessageType.AppointmentRequest: return MeetingKind.Request;
                case MessageType.AppointmentResponsePositive: return MeetingKind.Accepted;
                case MessageType.AppointmentResponseNegative: return MeetingKind.Declined;
                case MessageType.AppointmentResponseTentative: return MeetingKind.Tentative;
                case MessageType.AppointmentResponseCanceled: return MeetingKind.Canceled;
                case MessageType.AppointmentResponse:
                case MessageType.AppointmentNotification:
                case MessageType.AppointmentSchedule:
                case MessageType.Appointment:
                    return MeetingKind.Other;
                default: return MeetingKind.None;
            }
        }

        /// <summary>인터넷 헤더 문자열에서 헤더 값 1개 (접힌 줄 포함).</summary>
        internal static string HeaderValue(string headers, string name)
        {
            if (string.IsNullOrEmpty(headers)) return "";
            var m = Regex.Match(headers, "^" + Regex.Escape(name) + @":[ \t]*(.*(?:\r?\n[ \t].*)*)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return m.Success ? Regex.Replace(m.Groups[1].Value, @"\r?\n[ \t]+", " ").Trim() : "";
        }
    }
}

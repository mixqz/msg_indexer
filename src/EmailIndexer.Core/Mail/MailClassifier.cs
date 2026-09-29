using System;
using System.Collections.Generic;

namespace EmailIndexer.Core.Mail
{
    /// <summary>받은/보낸 판정과 기준 시각 (명세 3장).</summary>
    public static class MailClassifier
    {
        /// <param name="myAddresses">'내 주소' 목록 (대소문자 무시)</param>
        /// <param name="outlookFolderHint">앱이 Outlook에서 백업할 때 기록한 원본 폴더 방향</param>
        public static MailDirection GetDirection(MailInfo m, ICollection<string>? myAddresses, MailDirection? outlookFolderHint = null)
        {
            if (outlookFolderHint.HasValue) return outlookFolderHint.Value;
            if (myAddresses != null && !string.IsNullOrEmpty(m.SenderEmail))
                foreach (var a in myAddresses)
                    if (string.Equals(a?.Trim(), m.SenderEmail, StringComparison.OrdinalIgnoreCase))
                        return MailDirection.Sent;
            return MailDirection.Received;
        }

        /// <summary>받은 메일은 수신 시각, 보낸 메일은 발신 시각. 없으면 다른 쪽 → 파일 수정 시각.</summary>
        public static DateTimeOffset GetReferenceTime(MailInfo m, MailDirection direction)
        {
            var primary = direction == MailDirection.Received ? m.ReceivedTime : m.SentTime;
            var secondary = direction == MailDirection.Received ? m.SentTime : m.ReceivedTime;
            if (IsValid(primary)) return primary!.Value;
            if (IsValid(secondary)) return secondary!.Value;
            return new DateTimeOffset(DateTime.SpecifyKind(m.FileModifiedUtc, DateTimeKind.Utc));
        }

        // Outlook은 값이 없을 때 1601-01-01 또는 4501-01-01 같은 자리표시 날짜를 쓴다
        private static bool IsValid(DateTimeOffset? t)
            => t.HasValue && t.Value.Year > 1980 && t.Value.Year < 2200;
    }
}

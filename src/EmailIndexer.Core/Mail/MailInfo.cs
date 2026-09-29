using System;
using System.Collections.Generic;

namespace EmailIndexer.Core.Mail
{
    public enum MailFormat { Msg, Eml }

    /// <summary>일정 메일 종류 (명세 3장).</summary>
    public enum MeetingKind { None, Request, Accepted, Declined, Tentative, Canceled, Other }

    public enum MailDirection { Received, Sent }

    /// <summary>메일 파일 1개에서 읽어낸 정보. 읽기 실패 시 <see cref="ParseError"/>만 채워진다.</summary>
    public sealed class MailInfo
    {
        public string FilePath { get; set; } = "";
        public MailFormat Format { get; set; }
        public long FileSize { get; set; }
        public DateTime FileModifiedUtc { get; set; }

        /// <summary>발신 시각 (msg: PR_CLIENT_SUBMIT_TIME, eml: Date).</summary>
        public DateTimeOffset? SentTime { get; set; }
        /// <summary>수신 시각 (msg: PR_MESSAGE_DELIVERY_TIME, eml: 최상단 Received).</summary>
        public DateTimeOffset? ReceivedTime { get; set; }

        /// <summary>헤더 표시 이름 원문. 없으면 이메일 주소.</summary>
        public string SenderName { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public List<string> To { get; set; } = new List<string>();
        public List<string> Cc { get; set; } = new List<string>();

        public string Subject { get; set; } = "";
        public string MessageId { get; set; } = "";

        /// <summary>실제 첨부만 (본문 삽입 이미지 제외).</summary>
        public List<string> AttachmentNames { get; set; } = new List<string>();
        public bool HasAttachments => AttachmentNames.Count > 0;

        public MeetingKind Meeting { get; set; }

        /// <summary>검색·빠른 보기용 본문 텍스트.</summary>
        public string BodyText { get; set; } = "";

        public string? ParseError { get; set; }
        public bool IsError => ParseError != null;
    }
}

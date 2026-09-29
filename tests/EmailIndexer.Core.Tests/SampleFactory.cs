using System;
using System.IO;
using System.Text;
using MimeKit;
using MimeKit.Utils;

/// <summary>테스트용 합성 메일 파일 생성기 (eml: MimeKit, msg: MsgKit).</summary>
internal sealed class SampleFactory : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "eidx-" + Guid.NewGuid().ToString("N"));

    public SampleFactory() => Directory.CreateDirectory(Dir);

    public void Dispose()
    {
        try { Directory.Delete(Dir, true); } catch { }
    }

    public static readonly DateTimeOffset Sent = new DateTimeOffset(2026, 8, 23, 17, 50, 0, TimeSpan.FromHours(9));
    public static readonly DateTimeOffset Delivered = new DateTimeOffset(2026, 8, 23, 17, 54, 34, TimeSpan.FromHours(9));

    public MimeMessage NewEml(string subject, string? fromName = "Alex Kim [Sales Team]")
    {
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(fromName, "alex.kim@example.com"));
        m.To.Add(new MailboxAddress("김민수", "minsu@example.com"));
        m.Cc.Add(new MailboxAddress("Emma", "emma@example.com"));
        m.Subject = subject;
        m.Date = Sent;
        m.MessageId = "<abc123@example.com>";
        m.Headers.Add(HeaderId.Received, "from mx.example.com by mail.example.com; " + DateUtils.FormatDate(Delivered));
        m.Headers.Add(HeaderId.Received, "from client by mx.example.com; " + DateUtils.FormatDate(Sent.AddMinutes(1)));
        return m;
    }

    public string Save(MimeMessage m, string name)
    {
        var p = Path.Combine(Dir, name);
        m.WriteTo(p);
        return p;
    }

    public string SaveBytes(string name, byte[] data)
    {
        var p = Path.Combine(Dir, name);
        File.WriteAllBytes(p, data);
        return p;
    }

    public static MimePart Attachment(string fileName, string mime = "application/pdf", string disposition = ContentDisposition.Attachment, string? cid = null)
    {
        var part = new MimePart(mime)
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("dummy-" + fileName))),
            ContentDisposition = new ContentDisposition(disposition) { FileName = fileName },
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = fileName,
        };
        if (cid != null) part.ContentId = cid;
        return part;
    }

    public static TextPart Calendar(string method, string? partstat = null)
    {
        var ics = new StringBuilder()
            .AppendLine("BEGIN:VCALENDAR")
            .AppendLine("METHOD:" + method)
            .AppendLine("BEGIN:VEVENT")
            .AppendLine(partstat == null
                ? "ATTENDEE;CN=Kim:mailto:kim@example.com"
                : $"ATTENDEE;PARTSTAT={partstat};CN=Kim:mailto:kim@example.com")
            .AppendLine("SUMMARY:W35 team agenda")
            .AppendLine("END:VEVENT")
            .AppendLine("END:VCALENDAR")
            .ToString();
        var tp = new TextPart("calendar") { Text = ics };
        tp.ContentType.Parameters.Add("method", method);
        return tp;
    }

    // ---- msg (MsgKit) ----

    public string SaveMsg(string name, Action<MsgKit.Email> configure, string subject = "W35 team agenda")
    {
        var p = Path.Combine(Dir, name);
        using (var email = new MsgKit.Email(
                   new MsgKit.Sender("alex.kim@example.com", "Alex Kim [Sales Team]"),
                   subject))
        {
            email.Recipients.AddTo("minsu@example.com", "김민수");
            email.Recipients.AddCc("emma@example.com", "Emma");
            email.SentOn = Sent.UtcDateTime;
            email.ReceivedOn = Delivered.UtcDateTime;
            email.InternetMessageId = "<abc123@example.com>";
            email.BodyText = "안녕하세요. 본문입니다.";
            configure(email);
            email.Save(p);
        }
        return p;
    }
}

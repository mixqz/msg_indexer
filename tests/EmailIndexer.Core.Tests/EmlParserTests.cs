using System;
using System.IO;
using System.Text;
using EmailIndexer.Core.Mail;
using MimeKit;
using Xunit;

public class EmlParserTests : IDisposable
{
    private readonly SampleFactory _f = new SampleFactory();
    public void Dispose() => _f.Dispose();

    [Fact]
    public void Reads_basic_metadata_in_korean()
    {
        var m = _f.NewEml("RE: [긴급] 한빛마트 직구 수입 3월분 재전송 요청");
        m.Body = new TextPart("plain") { Text = "안녕하세요. 한빛물류 홍길동입니다." };
        var info = MailReader.Read(_f.Save(m, "a.eml"));

        Assert.Null(info.ParseError);
        Assert.Equal(MailFormat.Eml, info.Format);
        Assert.Equal("RE: [긴급] 한빛마트 직구 수입 3월분 재전송 요청", info.Subject);
        Assert.Equal("Alex Kim [Sales Team]", info.SenderName);
        Assert.Equal("alex.kim@example.com", info.SenderEmail);
        Assert.Equal(SampleFactory.Sent, info.SentTime);
        Assert.Equal(SampleFactory.Delivered, info.ReceivedTime); // 최상단 Received
        Assert.Equal("abc123@example.com", info.MessageId);
        Assert.Contains("김민수 <minsu@example.com>", info.To);
        Assert.Contains("Emma <emma@example.com>", info.Cc);
        Assert.Contains("홍길동", info.BodyText);
        Assert.False(info.HasAttachments);
        Assert.Equal(MeetingKind.None, info.Meeting);
        Assert.True(info.FileSize > 0);
    }

    [Fact]
    public void Sender_without_display_name_falls_back_to_address()
    {
        var m = _f.NewEml("no name", fromName: null);
        m.Body = new TextPart("plain") { Text = "x" };
        var info = MailReader.Read(_f.Save(m, "b.eml"));
        Assert.Equal("alex.kim@example.com", info.SenderName);
    }

    [Fact]
    public void Counts_real_attachment_only_not_inline_images()
    {
        var m = _f.NewEml("첨부 테스트");
        var related = new MultipartRelated
        {
            new TextPart("html") { Text = "<p>본문<img src=\"cid:logo1\"></p>" },
            // Outlook은 서명 이미지도 disposition=attachment로 넣는 경우가 있음 → cid 참조로 제외
            SampleFactory.Attachment("image001.png", "image/png", ContentDisposition.Attachment, cid: "logo1"),
        };
        var mixed = new Multipart("mixed")
        {
            related,
            SampleFactory.Attachment("한빛마트 0301-0315.xlsx", "application/vnd.ms-excel"),
            SampleFactory.Attachment("report.pdf"),
        };
        m.Body = mixed;
        var info = MailReader.Read(_f.Save(m, "c.eml"));

        Assert.Null(info.ParseError);
        Assert.Equal(new[] { "한빛마트 0301-0315.xlsx", "report.pdf" }, info.AttachmentNames);
        Assert.Contains("본문", info.BodyText); // HTML만 있는 메일도 텍스트 추출
        Assert.DoesNotContain("<p>", info.BodyText);
    }

    [Fact]
    public void Inline_image_without_filename_is_not_attachment()
    {
        var m = _f.NewEml("inline");
        var img = new MimePart("image", "png")
        {
            Content = new MimeContent(new MemoryStream(new byte[] { 1, 2, 3 })),
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
        };
        m.Body = new MultipartRelated { new TextPart("html") { Text = "<b>hi</b>" }, img };
        Assert.False(MailReader.Read(_f.Save(m, "d.eml")).HasAttachments);
    }

    [Fact]
    public void Attached_email_counts_as_attachment()
    {
        var inner = _f.NewEml("원본 메일");
        inner.Body = new TextPart("plain") { Text = "inner" };
        var m = _f.NewEml("FW: 원본 메일");
        m.Body = new Multipart("mixed") { new TextPart("plain") { Text = "fw" }, new MessagePart { Message = inner } };
        var info = MailReader.Read(_f.Save(m, "e.eml"));
        Assert.Single(info.AttachmentNames);
    }

    [Theory]
    [InlineData("REQUEST", null, MeetingKind.Request)]
    [InlineData("CANCEL", null, MeetingKind.Canceled)]
    [InlineData("REPLY", "ACCEPTED", MeetingKind.Accepted)]
    [InlineData("REPLY", "DECLINED", MeetingKind.Declined)]
    [InlineData("REPLY", "TENTATIVE", MeetingKind.Tentative)]
    [InlineData("COUNTER", null, MeetingKind.Other)]
    public void Detects_meeting_kind_and_ics_is_not_attachment(string method, string? partstat, MeetingKind expected)
    {
        var m = _f.NewEml("회의: W35 team agenda");
        var alt = new MultipartAlternative
        {
            new TextPart("plain") { Text = "회의 초대" },
            SampleFactory.Calendar(method, partstat),
        };
        var ics = SampleFactory.Attachment("invite.ics", "text/calendar");
        ((MimePart)ics).Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\nMETHOD:" + method + "\nEND:VCALENDAR")));
        m.Body = new Multipart("mixed") { alt, ics };

        var info = MailReader.Read(_f.Save(m, $"m-{method}-{partstat}.eml"));
        Assert.Null(info.ParseError);
        Assert.Equal(expected, info.Meeting);
        Assert.False(info.HasAttachments);
    }

    [Fact]
    public void Missing_received_header_leaves_received_time_empty()
    {
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress("나", "me@example.com"));
        m.Subject = "보낸 메일";
        m.Date = SampleFactory.Sent;
        m.Body = new TextPart("plain") { Text = "x" };
        var info = MailReader.Read(_f.Save(m, "sent.eml"));
        Assert.Null(info.ReceivedTime);
        Assert.Equal(SampleFactory.Sent, info.SentTime);
    }

    [Fact]
    public void Empty_file_is_reported_as_error()
    {
        var info = MailReader.Read(_f.SaveBytes("empty.eml", new byte[0]));
        Assert.NotNull(info.ParseError);
    }

    [Fact]
    public void Garbage_file_is_reported_as_error_without_throwing()
    {
        var rnd = new Random(1);
        var data = new byte[4096];
        rnd.NextBytes(data);
        var info = MailReader.Read(_f.SaveBytes("junk.eml", data));
        Assert.NotNull(info.ParseError);
    }

    [Fact]
    public void Missing_file_is_reported_as_error()
    {
        var info = MailReader.Read(Path.Combine(_f.Dir, "nope.eml"));
        Assert.NotNull(info.ParseError);
    }
}

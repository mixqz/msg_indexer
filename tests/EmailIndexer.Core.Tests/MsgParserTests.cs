using System;
using System.IO;
using System.Text;
using EmailIndexer.Core.Mail;
using MsgReader.Outlook;
using Xunit;

public class MsgParserTests : IDisposable
{
    private readonly SampleFactory _f = new SampleFactory();
    public void Dispose() => _f.Dispose();

    private static Stream Bytes(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Fact]
    public void Reads_basic_metadata()
    {
        var p = _f.SaveMsg("a.msg", _ => { }, subject: "RE: W35 team agenda / 일정 공유");
        var info = MailReader.Read(p);

        Assert.Null(info.ParseError);
        Assert.Equal(MailFormat.Msg, info.Format);
        Assert.Equal("RE: W35 team agenda / 일정 공유", info.Subject);
        Assert.Equal("Alex Kim [Sales Team]", info.SenderName);
        Assert.Equal("alex.kim@example.com", info.SenderEmail);
        Assert.Equal(SampleFactory.Sent.UtcDateTime, info.SentTime!.Value.UtcDateTime);
        Assert.Equal(SampleFactory.Delivered.UtcDateTime, info.ReceivedTime!.Value.UtcDateTime);
        Assert.Equal("abc123@example.com", info.MessageId);
        Assert.Contains(info.To, t => t.Contains("minsu@example.com"));
        Assert.Contains(info.Cc, t => t.Contains("emma@example.com"));
        Assert.Contains("본문", info.BodyText);
        Assert.False(info.HasAttachments);
        Assert.Equal(MeetingKind.None, info.Meeting);
    }

    [Fact]
    public void Counts_real_attachments_but_not_inline_images()
    {
        var p = _f.SaveMsg("b.msg", e =>
        {
            e.BodyHtml = "<html><body><p>본문</p><img src=\"cid:image001.png@01DB\"></body></html>";
            e.Attachments.Add(Bytes("png"), "image001.png", -1, true, "image001.png@01DB");
            e.Attachments.Add(Bytes("xlsx"), "한빛마트 0301-0315.xlsx", -1, false, "");
            e.Attachments.Add(Bytes("pdf"), "report.pdf", -1, false, "");
        });
        var info = MailReader.Read(p);

        Assert.Null(info.ParseError);
        Assert.Equal(new[] { "한빛마트 0301-0315.xlsx", "report.pdf" }, info.AttachmentNames);
    }

    [Fact]
    public void Html_only_body_is_converted_to_text()
    {
        var p = _f.SaveMsg("c.msg", e =>
        {
            e.BodyText = null;
            e.BodyHtml = "<html><head><style>p{color:red}</style></head><body><p>안녕하세요&nbsp;한빛물류</p></body></html>";
        });
        var info = MailReader.Read(p);
        Assert.Contains("한빛물류", info.BodyText);
        Assert.DoesNotContain("color", info.BodyText);
    }

    [Theory]
    [InlineData(MessageType.Email, MeetingKind.None)]
    [InlineData(MessageType.AppointmentRequest, MeetingKind.Request)]
    [InlineData(MessageType.AppointmentResponsePositive, MeetingKind.Accepted)]
    [InlineData(MessageType.AppointmentResponseNegative, MeetingKind.Declined)]
    [InlineData(MessageType.AppointmentResponseTentative, MeetingKind.Tentative)]
    [InlineData(MessageType.AppointmentResponseCanceled, MeetingKind.Canceled)]
    public void Maps_outlook_message_class_to_meeting_kind(MessageType type, MeetingKind expected)
        => Assert.Equal(expected, MsgParser.MeetingFromType(type));

    [Fact]
    public void Exchange_sender_address_is_resolved_from_internet_headers()
    {
        const string headers = "Received: from x; Sun, 23 Aug 2026 17:54:34 +0900\r\n" +
                               "From: \"Alex Kim\" <alex.kim@example.com>\r\n" +
                               "Message-ID:\r\n <folded-id@example.com>\r\n";
        Assert.Contains("alex.kim@example.com", MsgParser.HeaderValue(headers, "From"));
        Assert.Equal("<folded-id@example.com>", MsgParser.HeaderValue(headers, "Message-ID"));
    }

    [Fact]
    public void Garbage_msg_is_reported_as_error()
    {
        var data = new byte[8192];
        new Random(2).NextBytes(data);
        var info = MailReader.Read(_f.SaveBytes("junk.msg", data));
        Assert.NotNull(info.ParseError);
    }

    [Fact]
    public void Eml_renamed_to_msg_is_reported_as_error()
    {
        var info = MailReader.Read(_f.SaveBytes("fake.msg", Encoding.ASCII.GetBytes("From: a@b.c\r\nSubject: x\r\n\r\nbody")));
        Assert.NotNull(info.ParseError);
    }
}

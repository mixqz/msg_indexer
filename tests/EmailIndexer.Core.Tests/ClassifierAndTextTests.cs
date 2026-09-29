using System;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Text;
using Xunit;

public class ClassifierAndTextTests
{
    private static MailInfo Mail(string sender) => new MailInfo
    {
        SenderEmail = sender,
        SentTime = SampleFactory.Sent,
        ReceivedTime = SampleFactory.Delivered,
        FileModifiedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void Received_mail_uses_delivery_time()
    {
        var m = Mail("alex.kim@example.com");
        var dir = MailClassifier.GetDirection(m, new[] { "me@example.com" });
        Assert.Equal(MailDirection.Received, dir);
        Assert.Equal(SampleFactory.Delivered, MailClassifier.GetReferenceTime(m, dir));
    }

    [Fact]
    public void Mail_from_my_address_is_sent_and_uses_sent_time()
    {
        var m = Mail("ME@example.com");
        var dir = MailClassifier.GetDirection(m, new[] { "me@example.com" });
        Assert.Equal(MailDirection.Sent, dir);
        Assert.Equal(SampleFactory.Sent, MailClassifier.GetReferenceTime(m, dir));
    }

    [Fact]
    public void Outlook_folder_hint_wins()
        => Assert.Equal(MailDirection.Sent,
            MailClassifier.GetDirection(Mail("alex.kim@example.com"), null, MailDirection.Sent));

    [Fact]
    public void Falls_back_to_other_time_then_file_time()
    {
        var m = Mail("x@example.com");
        m.ReceivedTime = null;
        Assert.Equal(SampleFactory.Sent, MailClassifier.GetReferenceTime(m, MailDirection.Received));

        m.SentTime = new DateTimeOffset(4501, 1, 1, 0, 0, 0, TimeSpan.Zero); // Outlook 자리표시 날짜
        Assert.Equal(2026, MailClassifier.GetReferenceTime(m, MailDirection.Received).Year);
        Assert.Equal(9, MailClassifier.GetReferenceTime(m, MailDirection.Received).Month);
    }

    [Fact]
    public void Html_to_text_keeps_lines_and_decodes_entities()
    {
        var t = HtmlText.ToPlainText("<div>첫 줄</div><div>A&amp;B&nbsp;C</div><script>alert(1)</script><!-- c -->");
        Assert.Equal("첫 줄\nA&B C", t);
    }

    [Fact]
    public void Collects_cid_references()
    {
        var set = HtmlText.CidReferences("<img src=\"cid:image001.png@01DB\"><img src='cid:logo'>");
        Assert.Contains("image001.png@01DB", set);
        Assert.Contains("logo", set);
    }
}

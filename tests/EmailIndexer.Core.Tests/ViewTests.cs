using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.View;
using Xunit;

public class ViewTests
{
    private static MailRow Row(string rel, string subject, string sender = "alex@x.com", string senderName = "Alex",
        DateTimeOffset? time = null, bool attach = false, MeetingKind meeting = MeetingKind.None,
        DupStatus dup = DupStatus.None, string body = "", string? error = null, MailFormat fmt = MailFormat.Eml,
        string[]? to = null)
    {
        var m = new MailInfo
        {
            Subject = subject, SenderEmail = sender, SenderName = senderName,
            ReceivedTime = time ?? new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(9)),
            SentTime = time ?? new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(9)),
            Meeting = meeting, BodyText = body, ParseError = error, Format = fmt,
            To = (to ?? new string[0]).ToList(),
        };
        if (attach) m.AttachmentNames.Add("견적서_최종.xlsx");
        return new MailRow(new IndexEntry { RelPath = rel, Mail = m, Dup = dup }, new[] { "me@x.com" });
    }

    private static readonly List<MailRow> Rows = new List<MailRow>
    {
        Row(@"Inbox\2026-09\a.eml", "W35 team agenda", attach: true, body: "회의 자료입니다"),
        Row(@"Inbox\2026-08\b.eml", "한빛마트 직구 수입 3월분", sender: "gildong@hb.com", senderName: "홍길동",
            time: new DateTimeOffset(2026, 8, 24, 15, 6, 0, TimeSpan.FromHours(9)), to: new[] { "김민수 <minsu@x.com>" }),
        Row(@"Sent\c.msg", "RE: 한빛마트 직구 수입 3월분", sender: "me@x.com", senderName: "나", fmt: MailFormat.Msg),
        Row(@"Inbox\2026-09\d.eml", "회의: 주간", meeting: MeetingKind.Accepted, dup: DupStatus.Duplicate),
        Row(@"broken.msg", "", error: "손상", fmt: MailFormat.Msg),
        Row(@"260823_175434_Alex_W35_첨부X.eml", "W35", dup: DupStatus.Keeper),
    };

    private static List<string> Names(MailFilter f) => f.Apply(Rows).Select(r => r.FileName).ToList();

    [Fact]
    public void Empty_filter_returns_all()
    {
        var f = new MailFilter();
        Assert.True(f.IsEmpty);
        Assert.Equal(Rows.Count, f.Apply(Rows).Count);
    }

    [Fact]
    public void Search_is_AND_of_terms_and_supports_quotes()
    {
        Assert.Equal(new[] { "b.eml", "c.msg" }, Names(new MailFilter { Search = "한빛마트 3월분" }));
        Assert.Equal(new[] { "c.msg" }, Names(new MailFilter { Search = "\"RE: 한빛마트\"" }));
        Assert.Empty(Names(new MailFilter { Search = "한빛마트 agenda" }));
    }

    [Fact]
    public void Search_scopes()
    {
        Assert.Equal(new[] { "a.eml" }, Names(new MailFilter { Search = "회의 자료", Scope = SearchScope.Body }));
        Assert.Equal(new[] { "d.eml" }, Names(new MailFilter { Search = "회의", Scope = SearchScope.Subject }));
        Assert.Equal(new[] { "b.eml" }, Names(new MailFilter { Search = "minsu", Scope = SearchScope.People }));
        Assert.Equal(new[] { "b.eml" }, Names(new MailFilter { Search = "홍길동", Scope = SearchScope.People }));
        Assert.Equal(new[] { "a.eml" }, Names(new MailFilter { Search = "견적서", Scope = SearchScope.Attachments }));
        Assert.Empty(Names(new MailFilter { Search = "견적서", Scope = SearchScope.Subject }));
        Assert.Equal(new[] { "a.eml" }, Names(new MailFilter { Search = "w35 TEAM" })); // 대소문자 무시
    }

    [Fact]
    public void Filters_combine_with_AND()
    {
        var f = new MailFilter { HasAttachments = true };
        Assert.Equal(new[] { "a.eml" }, Names(f));

        f = new MailFilter();
        f.Directions.Add(MailDirection.Sent);
        Assert.Equal(new[] { "c.msg" }, Names(f)); // '내 주소' 발신

        f = new MailFilter();
        f.Meetings.Add(MeetingKind.Accepted);
        Assert.Equal(new[] { "d.eml" }, Names(f));

        f = new MailFilter();
        f.Statuses.Add(StatusFlag.Duplicate);
        Assert.Equal(new[] { "d.eml", "260823_175434_Alex_W35_첨부X.eml" }, Names(f)); // 원본+중복 모두

        f = new MailFilter();
        f.Statuses.Add(StatusFlag.Error);
        f.Formats.Add(MailFormat.Msg);
        Assert.Equal(new[] { "broken.msg" }, Names(f));

        f = new MailFilter { Folder = "Inbox" };
        Assert.Equal(3, Names(f).Count); // 하위 폴더 포함
        f = new MailFilter { Folder = @"Inbox\2026-08" };
        Assert.Equal(new[] { "b.eml" }, Names(f));

        f = new MailFilter { SenderEmail = "GILDONG@hb.com" };
        Assert.Equal(new[] { "b.eml" }, Names(f));
    }

    [Fact]
    public void Date_range_is_inclusive_of_end_day()
    {
        var f = new MailFilter { DateFrom = new DateTime(2026, 8, 24), DateTo = new DateTime(2026, 8, 24) };
        // 현지 시간대 기준 — 테스트 환경 TZ와 무관하도록 행의 LocalTime으로 기대값 계산
        var expected = Rows.Where(r => r.LocalTime.Date == new DateTime(2026, 8, 24)).Select(r => r.FileName);
        Assert.Equal(expected, Names(f));
    }

    [Fact]
    public void Not_normalized_status()
    {
        var f = new MailFilter();
        f.Statuses.Add(StatusFlag.NotNormalized);
        Assert.DoesNotContain("260823_175434_Alex_W35_첨부X.eml", Names(f));
        Assert.DoesNotContain("broken.msg", Names(f));
        Assert.Contains("a.eml", Names(f));
    }

    [Fact]
    public void Sorting_by_columns()
    {
        var list = Rows.ToList();
        list.Sort(MailSort.By(SortColumn.Time, ascending: false));
        Assert.Equal("b.eml", list.Last().FileName);
        list.Sort(MailSort.By(SortColumn.Time, ascending: true));
        Assert.Equal("b.eml", list.First().FileName);

        list.Sort(MailSort.By(SortColumn.Attach, ascending: false));
        Assert.Equal("a.eml", list[0].FileName);

        list.Sort(MailSort.By(SortColumn.Size, ascending: true));
        Assert.Equal(Rows.Count, list.Distinct().Count());
    }

    [Fact]
    public void Top_senders_counts()
    {
        var top = MailSort.TopSenders(Rows, 2);
        Assert.Equal("alex@x.com", top[0].key);
        Assert.Equal(3, top[0].count);
    }

    [Fact]
    public void Tokenize_handles_quotes_and_spaces()
        => Assert.Equal(new[] { "a b", "c", "d" }, MailFilter.Tokenize("  \"a b\"  c\td "));

    [Fact]
    public void Describe_lists_active_conditions()
    {
        var f = new MailFilter { HasAttachments = true, Search = "한빛마트", Scope = SearchScope.Subject };
        f.Meetings.Add(MeetingKind.Request);
        var d = f.Describe();
        Assert.Contains("With attachments", d);
        Assert.Contains("Invite", d);
        Assert.Contains("Search: 한빛마트 (Subject)", d);
    }

    [Fact]
    public void Display_strings()
    {
        Assert.Equal("142 KB", Display.Size(145408));
        Assert.Equal("Duplicate", Display.Status(Rows[3]));
        Assert.Equal("Error", Display.Status(Rows[4]));
        Assert.Equal("Renamed", Display.Status(Row("260823_175434_Alex_W35_첨부O.msg", "x")));
        Assert.Equal("Accepted", Display.Meeting(MeetingKind.Accepted));
    }

    // ---- 미리보기 정리 ----

    [Fact]
    public void Banner_markers_are_removed_from_text()
    {
        var t = Preview.CleanText("ZjQcmQRYFpfptBannerStart\nThis Message Is From an External Sender\nZjQcmQRYFpfptBannerEnd\n\n\n\n\n안녕하세요");
        Assert.DoesNotContain("ZjQcmQRYFpfpt", t);
        Assert.Contains("External Sender", t);
        Assert.DoesNotContain("\n\n\n", t.Replace("\r", ""));
    }

    [Fact]
    public void Sanitize_removes_scripts_events_and_external_content()
    {
        var html = "<html><head><meta http-equiv=\"refresh\" content=\"0;url=http://evil\"></head><body onload=\"x()\">" +
                   "<script>alert(1)</script><iframe src=\"http://evil\"></iframe>" +
                   "<img src=\"https://tracker.example.com/p.gif\"><a href=\"javascript:alert(1)\">x</a>" +
                   "<div style=\"background:url(http://evil/bg.png)\">본문</div><form action=\"http://x\"><input></form></body></html>";
        var s = Preview.Sanitize(html);
        Assert.DoesNotContain("<script", s, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iframe", s, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", s, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tracker.example.com", s);
        Assert.DoesNotContain("javascript:", s);
        Assert.DoesNotContain("refresh", s);
        Assert.DoesNotContain("evil", s);
        Assert.Contains("본문", s);
        Assert.Contains("IE=edge", s);
    }

    [Fact]
    public void Sanitize_embeds_cid_images()
    {
        var imgs = new Dictionary<string, (string, byte[])> { ["image001.png@01DB"] = ("image/png", new byte[] { 1, 2, 3 }) };
        var s = Preview.Sanitize("<p><img src=\"cid:image001.png@01DB\"><img src='cid:missing'></p>", imgs);
        Assert.Contains("data:image/png;base64,AQID", s);
        Assert.Contains("about:blank", s);
        Assert.StartsWith("<html><head>", s);
    }

    [Fact]
    public void Preview_loads_eml_and_msg_bodies()
    {
        using var f = new SampleFactory();
        var m = f.NewEml("미리보기");
        m.Body = new MimeKit.MultipartRelated
        {
            new MimeKit.TextPart("html") { Text = "<p>안녕하세요<img src=\"cid:logo\"></p>" },
            SampleFactory.Attachment("logo.png", "image/png", MimeKit.ContentDisposition.Inline, "logo"),
        };
        var p = f.Save(m, "p.eml");
        var pc = Preview.Load(MailReader.Read(p));
        Assert.Null(pc.Error);
        Assert.Contains("안녕하세요", pc.Text);
        Assert.Contains("data:image/png;base64,", pc.Html);

        var msgPath = f.SaveMsg("p.msg", e => e.BodyHtml = "<p>msg 본문</p>");
        var pm = Preview.Load(MailReader.Read(msgPath));
        Assert.Null(pm.Error);
        Assert.Contains("msg 본문", pm.Html);
    }

    [Fact]
    public void Preview_of_broken_file_falls_back_to_cached_text()
    {
        var pc = Preview.Load(new MailInfo { FilePath = "/nonexistent.msg", Format = MailFormat.Msg, BodyText = "캐시 본문" });
        Assert.NotNull(pc.Error);
        Assert.Equal("캐시 본문", pc.Text);
    }

    [Fact]
    public void Settings_roundtrip_and_corrupt_file()
    {
        using var f = new SampleFactory();
        var path = Path.Combine(f.Dir, "s", "settings.json");
        var s = new AppSettings { BackupRoot = @"C:\Users\me\이메일 백업", MyAddresses = { "me@x.com" } };
        s.ColumnWidths["제목"] = 420;
        s.Save(path);
        var back = AppSettings.Load(path);
        Assert.Equal(s.BackupRoot, back.BackupRoot);
        Assert.Equal(new[] { "me@x.com" }, back.MyAddresses);
        Assert.Equal(420, back.ColumnWidths["제목"]);

        File.WriteAllText(path, "{broken");
        Assert.Equal("", AppSettings.Load(path).BackupRoot);
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using EmailIndexer.Core.Files;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.View;
using Xunit;

/// <summary>파일명의 언어별 표기와, 언어를 바꿔도 이미 정리한 파일을 다시 바꾸지 않는지.</summary>
public class FileNameLanguageTests : IDisposable
{
    private readonly SampleFactory _f = new SampleFactory();
    public FileNameLanguageTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // .NET 8 테스트용 (net48은 기본 제공)
    public void Dispose() => _f.Dispose();

    private static MailRow Row(string subject, string sender = "Alex Kim", bool attach = false, string rel = "x.msg")
    {
        var t = new DateTimeOffset(2026, 8, 23, 17, 54, 34, TimeSpan.Zero).ToLocalTime();
        var m = new MailInfo { Subject = subject, SenderName = sender, SenderEmail = "", ReceivedTime = t, SentTime = t, Format = MailFormat.Msg };
        if (attach) m.AttachmentNames.Add("a.pdf");
        return new MailRow(new IndexEntry { RelPath = rel, Mail = m }, null);
    }

    [Theory]
    [InlineData("en", "_AttY", "_AttN", "(no sender)", "(no subject)")]
    [InlineData("es", "_AttY", "_AttN", "(sin remitente)", "(sin asunto)")]
    [InlineData("fr", "_AttY", "_AttN", "(sans expéditeur)", "(sans objet)")]
    [InlineData("ko", "_첨부O", "_첨부X", "(발신자 없음)", "(제목 없음)")]
    [InlineData("ja", "_添付有", "_添付無", "(差出人なし)", "(件名なし)")]
    [InlineData("zh", "_有附件", "_无附件", "(无发件人)", "(无主题)")]
    public void Markers_follow_language(string lang, string yes, string no, string noSender, string noSubject)
    {
        var w = FileNameRule.For(lang);
        Assert.EndsWith(yes + ".msg", FileNameRule.Build(Row("s", attach: true), w));
        Assert.EndsWith(no + ".msg", FileNameRule.Build(Row("s"), w));
        Assert.Contains($"_{noSender}_{noSubject}{no}.msg", FileNameRule.Build(Row("", sender: ""), w));
        // 어떤 언어 표기든 '정규화됨'으로 인식
        Assert.True(Duplicates.LooksNormalized(FileNameRule.Build(Row("s", attach: true), w)));
        Assert.True(Duplicates.LooksNormalized(FileNameRule.WithNumber(FileNameRule.Build(Row("s"), w), 2)));
    }

    [Fact]
    public void Unknown_language_and_default_are_english()
    {
        Assert.Same(FileNameRule.For("en"), FileNameRule.For("xx"));
        Assert.Same(FileNameRule.For("en"), FileNameRule.For(null));
        Assert.Same(FileNameRule.For("en"), FileNameRule.Current); // 테스트는 기본 언어(영어)
    }

    [Theory]
    [InlineData("260823_175434_Alex_W35_Att.msg")]
    [InlineData("260823_175434_Alex_W35_첨부.msg")]
    [InlineData("260823_175434_Alex_W35_AttY.txt")]
    [InlineData("W35_AttY.msg")]
    public void Similar_but_wrong_names_are_not_normalized(string name)
        => Assert.False(Duplicates.LooksNormalized(name));

    [Fact]
    public void Name_made_in_other_language_counts_as_already_normalized()
    {
        var r = Row("W35 team agenda", attach: true);
        var ko = FileNameRule.BuildForDirectory(r, @"C:\Mail", FileNameRule.For("ko"))!;
        var ja = FileNameRule.BuildForDirectory(r, @"C:\Mail", FileNameRule.For("ja"))!;
        Assert.True(FileNameRule.IsAlreadyAnyLanguage(ko, r, @"C:\Mail"));
        Assert.True(FileNameRule.IsAlreadyAnyLanguage(FileNameRule.WithNumber(ja, 3), r, @"C:\Mail"));
        // 첨부 여부가 틀린 이름은 규칙대로가 아님
        var wrong = FileNameRule.BuildForDirectory(Row("W35 team agenda"), @"C:\Mail", FileNameRule.For("ko"))!;
        Assert.False(FileNameRule.IsAlreadyAnyLanguage(wrong, r, @"C:\Mail"));
    }

    [Fact]
    public void Rename_plan_skips_files_normalized_in_korean_while_app_is_english()
    {
        var root = Path.Combine(_f.Dir, "b");
        Directory.CreateDirectory(root);
        var m = _f.NewEml("주간 회의");
        m.MessageId = "<ko@x>";
        var tmp = Path.Combine(root, "a.eml");
        m.WriteTo(tmp);

        var scanner = new Scanner(new FakeTrash(Path.Combine(_f.Dir, "t")));
        var row = new MailRow(scanner.Scan(root).Entries.Single(), null);
        var koName = FileNameRule.BuildForDirectory(row, root, FileNameRule.For("ko"))!;
        Assert.EndsWith("_첨부X.eml", koName);
        File.Move(tmp, Path.Combine(root, koName));

        var rows = new Scanner(new FakeTrash(Path.Combine(_f.Dir, "t2"))).Scan(root).Entries.Select(e => new MailRow(e, null));
        var plan = FileOps.PlanRenames(rows).Single();
        Assert.Equal(PlanStatus.Skip, plan.Status);
    }

    [Theory]
    [InlineData("ko-KR", 949)]
    [InlineData("ja-JP", 932)]
    [InlineData("zh-CN", 936)]
    [InlineData("zh-TW", 950)]
    [InlineData("fr-FR", 1252)]
    [InlineData("es-ES", 1252)]
    [InlineData("en-US", 1252)]
    [InlineData("ru-RU", 1251)]
    [InlineData("", 1252)]
    [InlineData(null, 1252)]
    public void Fallback_code_page_follows_pc_region(string? culture, int expected)
    {
        var cp = EmlParser.FallbackCodePage(culture);
        Assert.Equal(expected, cp);
        Assert.NotNull(Encoding.GetEncoding(cp)); // 해당 인코딩을 실제로 쓸 수 있는지
    }

    [Fact]
    public void Eml_without_charset_decodes_with_fallback()
    {
        // 문자셋 표기 없는 8bit 헤더: 대체 코드 페이지로 읽혀야 한다 (여기서는 PC 지역에 따라 달라지므로
        // 테스트 환경의 대체 코드 페이지로 인코딩해 넣고 원문이 복원되는지 본다)
        var cp = EmlParser.FallbackCodePage(CultureInfo.CurrentCulture.Name);
        var subject = cp == 1252 ? "Réunion équipe" : "Test";
        var enc = Encoding.GetEncoding(cp);
        var bytes = enc.GetBytes("From: a@x.com\r\nSubject: " + subject + "\r\nDate: Mon, 24 Aug 2026 10:00:00 +0900\r\n" +
                                 "Content-Type: text/plain\r\n\r\nbody\r\n");
        var path = Path.Combine(_f.Dir, "nocharset.eml");
        File.WriteAllBytes(path, bytes);
        Assert.Equal(subject, MailReader.Read(path).Subject);
    }
}

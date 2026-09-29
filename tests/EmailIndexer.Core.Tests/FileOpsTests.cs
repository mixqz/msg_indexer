using System;
using System.IO;
using System.Linq;
using EmailIndexer.Core.Files;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Text;
using EmailIndexer.Core.View;
using MimeKit;
using Xunit;

public class FileNameRuleTests
{
    // 받은 메일: 수신 시각이 기준. 테스트 TZ와 무관하게 LocalTime을 직접 확인하기 위해 행에서 읽는다.
    private static MailRow Row(string subject, string sender = "Alex Kim [Sales Team]", bool attach = false,
        MailFormat fmt = MailFormat.Msg, string email = "alex@x.com")
    {
        var t = new DateTimeOffset(2026, 8, 23, 17, 54, 34, TimeSpan.Zero).ToLocalTime();
        var m = new MailInfo { Subject = subject, SenderName = sender, SenderEmail = email, ReceivedTime = t, SentTime = t.AddMinutes(-3), Format = fmt };
        if (attach) m.AttachmentNames.Add("a.pdf");
        return new MailRow(new IndexEntry { RelPath = "x" + (fmt == MailFormat.Msg ? ".msg" : ".eml"), Mail = m }, null);
    }

    private static string Stamp(MailRow r) => r.LocalTime.ToString("yyMMdd_HHmmss");

    [Fact]
    public void Matches_example_format()
    {
        var r = Row("W35 team agenda");
        Assert.Equal($"{Stamp(r)}_Alex Kim [Sales Team]_W35 team agenda_첨부X.msg", FileNameRule.Build(r));
        Assert.EndsWith("_첨부O.eml", FileNameRule.Build(Row("x", attach: true, fmt: MailFormat.Eml)));
    }

    [Fact]
    public void Uses_sent_time_for_sent_mail()
    {
        var r = Row("보냄");
        var sent = new MailRow(r.Entry, new[] { "alex@x.com" });
        Assert.Equal(MailDirection.Sent, sent.Direction);
        Assert.StartsWith(sent.Mail.SentTime!.Value.LocalDateTime.ToString("yyMMdd_HHmmss"), FileNameRule.Build(sent));
    }

    [Fact]
    public void Keeps_prefixes_and_replaces_forbidden_chars_with_full_width()
    {
        var name = FileNameRule.Build(Row("RE: FW: 결정/보류? \"긴급\" <A|B> *\r\n다음 줄", sender: "홍길동(GILDONG HONG)/물류운영팀"));
        Assert.Contains("_홍길동(GILDONG HONG)／물류운영팀_", name);
        Assert.Contains("RE： FW： 결정／보류？ ＂긴급＂ ＜A｜B＞ ＊ 다음 줄", name);
        Assert.Equal(-1, name.IndexOfAny(Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).ToArray()));
    }

    [Fact]
    public void Missing_values_get_placeholders()
    {
        var n = FileNameRule.Build(Row("", sender: "", email: ""));
        Assert.Contains("_(발신자 없음)_(제목 없음)_첨부X", n);
        Assert.Contains("_bob@x.com_", FileNameRule.Build(Row("s", sender: "", email: "bob@x.com")));
    }

    [Fact]
    public void Trims_subject_only_when_path_too_long()
    {
        var subject = string.Concat(Enumerable.Repeat("아주긴제목", 60)); // 300자
        var r = Row(subject);
        var dir = @"C:\Users\me\Desktop\이메일 백업\Inbox\2026-08";
        var n = FileNameRule.BuildForDirectory(r, dir)!;
        Assert.True(dir.Length + 1 + n.Length <= FileNameRule.MaxPath);
        Assert.Contains("…_첨부X.msg", n);
        Assert.StartsWith(Stamp(r) + "_Alex Kim", n);

        var shortR = Row("짧은 제목");
        Assert.Equal(FileNameRule.Build(shortR), FileNameRule.BuildForDirectory(shortR, dir));
    }

    [Fact]
    public void Does_not_split_surrogate_pairs()
    {
        var r = Row(string.Concat(Enumerable.Repeat("😀", 200)));
        var n = FileNameRule.BuildForDirectory(r, @"C:\a")!;
        foreach (var (c, i) in n.Select((c, i) => (c, i)))
            if (char.IsHighSurrogate(c)) Assert.True(i + 1 < n.Length && char.IsLowSurrogate(n[i + 1]));
    }

    [Fact]
    public void Deep_directory_returns_null_when_nothing_fits()
        => Assert.Null(FileNameRule.BuildForDirectory(Row("x"), "C:\\" + new string('d', 250)));

    [Fact]
    public void Already_normalized_detection()
    {
        Assert.True(FileNameRule.IsAlready("a_첨부X.msg", "a_첨부X.msg"));
        Assert.True(FileNameRule.IsAlready("a_첨부X (2).msg", "a_첨부X.msg"));
        Assert.False(FileNameRule.IsAlready("b.msg", "a_첨부X.msg"));
        Assert.True(Duplicates.LooksNormalized("260823_175434_Alex_W35_첨부X (3).msg"));
    }
}

public class FileOpsTests : IDisposable
{
    private readonly SampleFactory _f = new SampleFactory();
    private readonly string _root;
    public FileOpsTests()
    {
        _root = Path.Combine(_f.Dir, "backup");
        Directory.CreateDirectory(Path.Combine(_root, "Inbox"));
    }
    public void Dispose() => _f.Dispose();

    private string Eml(string rel, string subject, string id, bool attach = false)
    {
        var m = _f.NewEml(subject);
        m.MessageId = id;
        m.Body = attach
            ? new Multipart("mixed") { new TextPart("plain") { Text = "b" }, SampleFactory.Attachment("r.pdf") }
            : (MimeEntity)new TextPart("plain") { Text = "본문 " + id };
        var p = Path.Combine(_root, rel);
        m.WriteTo(p);
        return p;
    }

    private MailRow[] Scan() => new Scanner(new FakeTrash(Path.Combine(_f.Dir, "trash")))
        .Scan(_root).Entries.Select(e => new MailRow(e, null)).ToArray();

    [Fact]
    public void Rename_preview_apply_rescan_and_undo()
    {
        var a = Eml("Inbox/a.eml", "W35 team agenda", "<a@x>", attach: true);
        Eml("Inbox/b.eml", "W35 team agenda", "<b@x>", attach: true);   // 같은 규칙 이름 → (2)
        File.WriteAllBytes(Path.Combine(_root, "bad.msg"), new byte[] { 1 });
        var rows = Scan();

        var plans = FileOps.PlanRenames(rows);
        Assert.Equal(2, plans.Count(p => p.Status == PlanStatus.Rename));
        Assert.Single(plans, p => p.Status == PlanStatus.Skip && p.Note == L.T("plan.skip.error"));
        var names = plans.Where(p => p.Status == PlanStatus.Rename).Select(p => p.NewName).ToList();
        Assert.Contains(names, n => n.EndsWith("_W35 team agenda_첨부O.eml"));
        Assert.Contains(names, n => n.EndsWith("_W35 team agenda_첨부O (2).eml"));

        var res = FileOps.ApplyRenames(_root, plans);
        Assert.Equal(2, res.Done);
        Assert.False(File.Exists(a));

        // 증분 스캔은 '이동'으로 인식 → 중복 자동 삭제 없음
        var r2 = new Scanner(new FakeTrash(Path.Combine(_f.Dir, "trash2"))).Scan(_root);
        Assert.Equal(2, r2.Moved);
        Assert.Empty(r2.AutoTrashed);

        // 다시 계획하면 모두 '이미 규칙대로'
        var again = FileOps.PlanRenames(r2.Entries.Select(e => new MailRow(e, null)));
        Assert.DoesNotContain(again, p => p.Status == PlanStatus.Rename);

        var undo = FileOps.LatestUndo(_root)!;
        Assert.Equal(2, FileOps.UndoCount(undo));
        var u = FileOps.UndoRenames(_root, undo);
        Assert.Equal(2, u.Done);
        Assert.True(File.Exists(a));
        Assert.Null(FileOps.LatestUndo(_root)); // 두 번 되돌리지 않음
    }

    [Fact]
    public void Rename_avoids_existing_file_with_target_name()
    {
        Eml("Inbox/a.eml", "주간 보고", "<a@x>");
        var row = Scan().Single();
        var target = FileNameRule.Build(row);
        File.WriteAllText(Path.Combine(_root, "Inbox", target), "다른 파일");
        var plan = FileOps.PlanRenames(new[] { row }).Single();
        Assert.Equal(FileNameRule.WithNumber(target, 2), plan.NewName);
    }

    [Fact]
    public void Undo_skips_files_moved_away()
    {
        Eml("Inbox/a.eml", "x", "<a@x>");
        var plans = FileOps.PlanRenames(Scan());
        FileOps.ApplyRenames(_root, plans);
        File.Delete(plans[0].NewPath);
        var u = FileOps.UndoRenames(_root, FileOps.LatestUndo(_root)!);
        Assert.Equal(0, u.Done);
        Assert.Single(u.Failures);
    }

    [Fact]
    public void Move_with_name_collision()
    {
        Eml("Inbox/a.eml", "x", "<a@x>");
        var dest = Path.Combine(_root, "보관");
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, "a.eml"), "기존");
        var res = FileOps.Move(_root, Scan(), dest);
        Assert.Equal(1, res.Done);
        Assert.True(File.Exists(Path.Combine(dest, "a (2).eml")));
        Assert.Contains("MOVE", File.ReadAllText(Path.Combine(IndexStore.IndexDir(_root), ActionLog.FileName)));
    }

    [Fact]
    public void Trash_and_dedupe_keep_originals()
    {
        var a = Eml("Inbox/a.eml", "x", "<a@x>");
        File.Copy(a, Path.Combine(_root, "copy1.eml"));
        File.Copy(a, Path.Combine(_root, "copy2.eml"));
        Eml("Inbox/other.eml", "y", "<o@x>");
        var rows = Scan(); // 첫 스캔: 표시만

        var dups = FileOps.DuplicatesToRemove(rows);
        Assert.Equal(2, dups.Count);
        var trash = new FakeTrash(Path.Combine(_f.Dir, "t"));
        var res = FileOps.Trash(_root, dups, trash, "TRASH_DUPLICATE");
        Assert.Equal(2, res.Done);
        Assert.Equal(2, Directory.GetFiles(_root, "*.eml", SearchOption.AllDirectories).Length);

        trash.Fail = true;
        var fail = FileOps.Trash(_root, rows.Where(r => r.FileName == "other.eml"), trash);
        Assert.Single(fail.Failures);
    }
}

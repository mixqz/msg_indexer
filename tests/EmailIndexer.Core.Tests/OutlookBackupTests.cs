using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Outlook;
using EmailIndexer.Core.View;
using Xunit;

/// <summary>Outlook 없이 백업 로직을 검증하기 위한 가짜 폴더/항목.</summary>
internal sealed class FakeItem : IOutlookItem
{
    private readonly SampleFactory _f;
    public string EntryId { get; set; } = Guid.NewGuid().ToString("N");
    public string MessageId { get; set; } = "";
    /// <summary>msg 안에 들어갈 Message-ID (Outlook 속성과 다를 수 있음을 흉내).</summary>
    public string FileMessageId { get; set; } = "";
    public DateTime SortTime { get; set; }
    public string Subject { get; set; } = "";
    public string Sender { get; set; } = "Alex Kim [ST]";
    public bool Attach { get; set; }
    public Exception? Throw { get; set; }
    public bool WriteNothing { get; set; }
    public bool Disposed { get; private set; }
    public FakeItem(SampleFactory f) => _f = f;

    public void SaveAsMsg(string path)
    {
        if (Throw != null) throw Throw;
        if (WriteNothing) return;
        var src = _f.SaveMsg(Guid.NewGuid().ToString("N") + ".msg", e =>
        {
            e.InternetMessageId = FileMessageId.Length > 0 ? FileMessageId : MessageId;
            e.SentOn = SortTime.AddMinutes(-1).ToUniversalTime();
            e.ReceivedOn = SortTime.ToUniversalTime();
            if (Attach) e.Attachments.Add(new MemoryStream(new byte[] { 1, 2 }), "견적.pdf", -1, false, "");
        }, Subject);
        File.Copy(src, path);
    }

    public void Dispose() => Disposed = true;
}

internal sealed class FakeFolder : IOutlookFolder
{
    public string DisplayName { get; set; } = "me@x.com / 받은편지함";
    public string Key { get; set; } = "me@x.com|inbox";
    public MailDirection Direction { get; set; } = MailDirection.Received;
    public List<FakeItem> Items { get; } = new List<FakeItem>();
    public int Count => Items.Count;
    public int Enumerated { get; private set; }
    public IEnumerable<IOutlookItem> ItemsNewestFirst()
    {
        foreach (var i in Items.OrderByDescending(x => x.SortTime)) { Enumerated++; yield return i; }
    }
}

public class OutlookBackupTests : IDisposable
{
    private readonly SampleFactory _f = new SampleFactory();
    private readonly string _root;
    public OutlookBackupTests() { _root = Path.Combine(_f.Dir, "backup"); Directory.CreateDirectory(_root); }
    public void Dispose() => _f.Dispose();

    private FakeItem Item(string subject, string id, DateTime t, bool attach = false)
        => new FakeItem(_f) { Subject = subject, MessageId = id, SortTime = t, Attach = attach };

    private static readonly DateTime T = new DateTime(2026, 9, 21, 13, 5, 0, DateTimeKind.Local);

    private BackupReport Run(IEnumerable<IOutlookFolder> folders, BackupOptions? o = null, IEnumerable<string>? known = null)
        => new OutlookBackup(_root, known ?? Enumerable.Empty<string>()).Run(folders, o ?? new BackupOptions { Range = BackupRange.All });

    [Fact]
    public void Saves_new_mail_with_normalized_name_directly_in_backup_folder()
    {
        var inbox = new FakeFolder();
        inbox.Items.Add(Item("W35 team agenda: 논의/결정?", "<a@x>", T, attach: true));
        var sent = new FakeFolder { DisplayName = "me@x.com / 보낸편지함", Key = "me@x.com|sent", Direction = MailDirection.Sent };
        sent.Items.Add(Item("RE: 회신", "<b@x>", T.AddDays(-40)));

        var r = Run(new[] { inbox, sent });

        Assert.Equal(2, r.Saved);
        Assert.Equal(0, r.Failed);
        Assert.Empty(Directory.GetDirectories(_root).Where(d => Path.GetFileName(d) != IndexStore.FolderName)); // 하위 폴더 없음
        var files = Directory.GetFiles(_root, "*.msg");
        Assert.Equal(2, files.Length);
        var inboxFile = files.Single(f => f.Contains("W35"));
        Assert.EndsWith("_W35 team agenda： 논의／결정？_첨부O.msg", inboxFile);
        Assert.StartsWith("260921_", Path.GetFileName(inboxFile)); // 받은 메일 = 수신 시각
        Assert.Single(files, f => f.Contains("RE： 회신"));
        Assert.True(inbox.Items.All(i => i.Disposed));
        Assert.Empty(Directory.GetFiles(_root, "~eidx_*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Scanner_applies_folder_direction_from_backup()
    {
        var sent = new FakeFolder { Key = "k|sent", Direction = MailDirection.Sent };
        sent.Items.Add(Item("보낸 메일", "<s@x>", T));
        Run(new[] { sent });

        var e = new Scanner(new FakeTrash(Path.Combine(_f.Dir, "t"))).Scan(_root).Entries.Single();
        Assert.Equal(MailDirection.Sent, e.FolderHint);
        // 발신자가 '내 주소'에 없어도 보낸 메일로 판정
        Assert.Equal(MailDirection.Sent, new MailRow(e, null).Direction);
        Assert.StartsWith("260921_130400_", Path.GetFileName(e.RelPath)); // 보낸 메일 = 발신 시각(1분 전)
    }

    [Fact]
    public void Skips_mail_already_in_backup_folder_and_on_second_run()
    {
        var inbox = new FakeFolder();
        inbox.Items.Add(Item("기존", "<OLD@x>", T));
        inbox.Items.Add(Item("신규", "<new@x>", T.AddHours(-1)));

        var r1 = Run(new[] { inbox }, known: new[] { "old@x" });
        Assert.Equal(1, r1.Saved);
        Assert.Equal(1, r1.Skipped);

        var r2 = Run(new[] { inbox }, known: new[] { "old@x" }); // 신규는 저장 기록으로 건너뜀
        Assert.Equal(0, r2.Saved);
        Assert.Equal(2, r2.Skipped);
    }

    [Fact]
    public void Skips_when_file_reveals_known_message_id()
    {
        var inbox = new FakeFolder();
        inbox.Items.Add(new FakeItem(_f) { Subject = "x", MessageId = "", FileMessageId = "<known@x>", SortTime = T });
        var r = Run(new[] { inbox }, known: new[] { "known@x" });
        Assert.Equal(0, r.Saved);
        Assert.Equal(1, r.Skipped);
        Assert.Empty(Directory.GetFiles(_root, "*.msg", SearchOption.AllDirectories));
    }

    [Fact]
    public void Failures_are_counted_with_reasons_and_leave_no_temp_files()
    {
        var inbox = new FakeFolder();
        inbox.Items.Add(new FakeItem(_f) { Subject = "권한", SortTime = T, Throw = new UnauthorizedAccessException() });
        inbox.Items.Add(new FakeItem(_f) { Subject = "빈 저장", SortTime = T.AddMinutes(-1), WriteNothing = true });
        inbox.Items.Add(Item("정상", "<ok@x>", T.AddMinutes(-2)));

        var r = Run(new[] { inbox });
        Assert.Equal(1, r.Saved);
        Assert.Equal(2, r.Failed);
        Assert.Contains(r.Failures, f => f.Contains("No permission to write"));
        Assert.Contains(r.Failures, f => f.Contains("no file was created"));
        Assert.Empty(Directory.GetFiles(_root, "~eidx_*", SearchOption.AllDirectories));
        Assert.Contains("FAILED", File.ReadAllText(Path.Combine(IndexStore.IndexDir(_root), OutlookBackup.LogName)));
    }

    [Fact]
    public void Since_last_backup_stops_at_older_items()
    {
        var inbox = new FakeFolder();
        for (int i = 0; i < 5; i++) inbox.Items.Add(Item($"m{i}", $"<m{i}@x>", DateTime.Now.AddDays(-i * 10)));

        var first = Run(new[] { inbox }, new BackupOptions { Range = BackupRange.SinceLastBackup }); // 기록 없음 → 전체
        Assert.Equal(5, first.Saved);

        inbox.Items.Add(Item("새 메일", "<fresh@x>", DateTime.Now.AddMinutes(1)));
        var before = inbox.Enumerated;
        var second = Run(new[] { inbox }, new BackupOptions { Range = BackupRange.SinceLastBackup });
        Assert.Equal(1, second.Saved);
        Assert.True(inbox.Enumerated - before <= 3); // 오래된 메일까지 다 훑지 않음
    }

    [Fact]
    public void Last_days_range()
    {
        var inbox = new FakeFolder();
        inbox.Items.Add(Item("오늘", "<t@x>", DateTime.Now));
        inbox.Items.Add(Item("옛날", "<o@x>", DateTime.Now.AddDays(-100)));
        var r = Run(new[] { inbox }, new BackupOptions { Range = BackupRange.LastDays, Days = 30 });
        Assert.Equal(1, r.Saved);
    }

    [Fact]
    public void Same_name_gets_number_and_cancel_stops()
    {
        var inbox = new FakeFolder();
        inbox.Items.Add(Item("같은 제목", "<1@x>", T));
        inbox.Items.Add(Item("같은 제목", "<2@x>", T));
        Run(new[] { inbox });
        var names = Directory.GetFiles(_root, "*.msg").Select(Path.GetFileName).ToList();
        Assert.Contains(names, n => n!.EndsWith("_같은 제목_첨부X (2).msg"));

        var more = new FakeFolder();
        for (int i = 0; i < 5; i++) more.Items.Add(Item($"c{i}", $"<c{i}@x>", T.AddMinutes(-i)));
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var r = new OutlookBackup(_root, new string[0]).Run(new[] { more }, new BackupOptions { Range = BackupRange.All }, null, cts.Token);
        Assert.True(r.Cancelled);
        Assert.Equal(0, r.Saved);
    }

    [Fact]
    public void Cleans_leftover_temp_files_including_old_subfolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Inbox", "2026-09"));
        File.WriteAllText(Path.Combine(_root, "~eidx_aaa.msg"), "x");
        File.WriteAllText(Path.Combine(_root, "Inbox", "2026-09", "~eidx_bbb.msg"), "x");
        File.WriteAllText(Path.Combine(_root, "keep.msg"), "x");
        Assert.Equal(2, OutlookBackup.CleanupTempFiles(_root));
        Assert.True(File.Exists(Path.Combine(_root, "keep.msg")));
    }

    [Fact]
    public void Explain_common_errors()
    {
        Assert.Contains("permission", OutlookBackup.Explain(new UnauthorizedAccessException()));
        Assert.Contains("path", OutlookBackup.Explain(new PathTooLongException()));
    }
}

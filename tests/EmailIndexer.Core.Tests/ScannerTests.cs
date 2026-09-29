using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using EmailIndexer.Core.Index;
using EmailIndexer.Core.Mail;
using MimeKit;
using Xunit;
using Xunit.Abstractions;

/// <summary>휴지통 대신 별도 폴더로 옮기는 테스트용 구현.</summary>
internal sealed class FakeTrash : ITrash
{
    public string Dir { get; }
    public bool Fail { get; set; }
    public List<string> Sent { get; } = new List<string>();
    public FakeTrash(string dir) { Dir = dir; Directory.CreateDirectory(dir); }

    public bool SendToRecycleBin(string path, out string? error)
    {
        if (Fail) { error = "사용 중"; return false; }
        var dest = Path.Combine(Dir, Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(path));
        File.Move(path, dest);
        Sent.Add(dest);
        error = null;
        return true;
    }
}

public class ScannerTests : IDisposable
{
    private readonly SampleFactory _f = new SampleFactory();
    private readonly string _root;
    private readonly FakeTrash _trash;
    private readonly Scanner _scanner;

    public ScannerTests()
    {
        _root = Path.Combine(_f.Dir, "backup");
        Directory.CreateDirectory(Path.Combine(_root, "Inbox", "2026-08"));
        _trash = new FakeTrash(Path.Combine(_f.Dir, "trash"));
        _scanner = new Scanner(_trash);
    }

    public void Dispose() => _f.Dispose();

    private string Eml(string rel, string subject, string messageId, DateTimeOffset? date = null, string body = "본문")
    {
        var m = _f.NewEml(subject);
        m.MessageId = messageId;
        if (date.HasValue) m.Date = date.Value;
        m.Body = new TextPart("plain") { Text = body };
        var p = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        m.WriteTo(p);
        return p;
    }

    private static IndexEntry E(ScanResult r, string rel) => r.Entries.Single(e => e.RelPath == rel.Replace('\\', Path.DirectorySeparatorChar));

    [Fact]
    public void First_scan_indexes_and_only_flags_duplicates()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        File.Copy(a, Path.Combine(_root, "Inbox", "2026-08", "a-copy.eml"));
        Eml("b.eml", "제목B", "<b@x>");

        var r = _scanner.Scan(_root);

        Assert.True(r.FirstScan);
        Assert.Equal(3, r.Total);
        Assert.Equal(3, r.Added);
        Assert.Empty(r.AutoTrashed);                    // 첫 스캔은 자동 삭제 없음
        Assert.Equal(2, r.Entries.Count(e => e.DupGroup > 0));
        Assert.Single(r.Entries, e => e.Dup == DupStatus.Duplicate);
        Assert.Single(r.Entries, e => e.Dup == DupStatus.Keeper);
        Assert.True(File.Exists(IndexStore.IndexPath(_root)));
    }

    [Fact]
    public void Second_scan_reads_nothing_when_unchanged()
    {
        Eml("a.eml", "제목A", "<a@x>");
        Eml("Inbox/2026-08/b.eml", "제목B", "<b@x>");
        _scanner.Scan(_root);

        var r = _scanner.Scan(_root);
        Assert.False(r.FirstScan);
        Assert.Equal(2, r.Unchanged);
        Assert.Equal(0, r.Added + r.Changed + r.Removed + r.Moved);
        Assert.Equal("제목B", E(r, "Inbox/2026-08/b.eml").Mail.Subject); // 캐시에서 복원된 값
    }

    [Fact]
    public void Detects_changed_and_removed_files()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        var b = Eml("b.eml", "제목B", "<b@x>");
        _scanner.Scan(_root);

        Eml("a.eml", "제목A-수정", "<a@x>", body: "바뀐 본문입니다");
        File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddMinutes(1));
        File.Delete(b);

        var r = _scanner.Scan(_root);
        Assert.Equal(1, r.Changed);
        Assert.Equal(1, r.Removed);
        Assert.Equal("제목A-수정", E(r, "a.eml").Mail.Subject);
    }

    [Fact]
    public void Renamed_or_moved_file_is_not_treated_as_duplicate()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        _scanner.Scan(_root);

        File.Move(a, Path.Combine(_root, "Inbox", "2026-08", "renamed.eml"));
        var r = _scanner.Scan(_root);

        Assert.Equal(1, r.Moved);
        Assert.Equal(0, r.Removed);
        Assert.Empty(r.AutoTrashed);
        Assert.Equal(1, r.Total);
    }

    [Fact]
    public void New_identical_copy_is_sent_to_trash_and_original_kept()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        _scanner.Scan(_root);

        var copy = Path.Combine(_root, "Inbox", "2026-08", "a (1).eml");
        File.Copy(a, copy);
        var r = _scanner.Scan(_root);

        Assert.Single(r.AutoTrashed);
        Assert.False(File.Exists(copy));
        Assert.True(File.Exists(a));
        Assert.Equal(1, r.Total);
        Assert.Equal(0, r.Added);
        Assert.Contains("TRASH_DUPLICATE", File.ReadAllText(Path.Combine(IndexStore.IndexDir(_root), ActionLog.FileName)));
    }

    [Fact]
    public void New_file_with_same_message_id_is_trashed_even_if_bytes_differ()
    {
        Eml("a.eml", "제목A", "<same@x>");
        _scanner.Scan(_root);

        var other = Eml("Inbox/2026-08/a-다시수집.eml", "제목A", "<SAME@x>", body: "헤더가 조금 다른 같은 메일");
        var r = _scanner.Scan(_root);

        Assert.Single(r.AutoTrashed);
        Assert.False(File.Exists(other));
    }

    [Fact]
    public void Two_new_copies_without_existing_original_are_only_flagged()
    {
        Eml("x.eml", "기존", "<x@x>");
        _scanner.Scan(_root);

        var n1 = Eml("n1.eml", "새메일", "<new@x>");
        File.Copy(n1, Path.Combine(_root, "n2.eml"));
        var r = _scanner.Scan(_root);

        Assert.Empty(r.AutoTrashed);
        Assert.Single(r.Entries, e => e.Dup == DupStatus.Duplicate);
    }

    [Fact]
    public void Restored_file_is_not_trashed_again()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        _scanner.Scan(_root);
        var copy = Path.Combine(_root, "copy.eml");
        File.Copy(a, copy);
        _scanner.Scan(_root);                       // 휴지통으로 감
        File.Move(_trash.Sent.Single(), copy);       // 사용자가 복원

        var r = _scanner.Scan(_root);
        Assert.Empty(r.AutoTrashed);
        Assert.True(File.Exists(copy));
        Assert.Equal(DupStatus.Duplicate, E(r, "copy.eml").Dup);
    }

    [Fact]
    public void Trash_failure_keeps_file_and_reports()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        _scanner.Scan(_root);
        File.Copy(a, Path.Combine(_root, "copy.eml"));
        _trash.Fail = true;

        var r = _scanner.Scan(_root);
        Assert.Single(r.TrashFailures);
        Assert.Equal(DupStatus.Duplicate, E(r, "copy.eml").Dup);
    }

    [Fact]
    public void Similar_mails_within_one_minute_are_flagged_only()
    {
        var t = SampleFactory.Sent;
        Eml("s1.eml", "주간 보고", "<s1@x>", t, "버전 1");
        Eml("s2.eml", "주간 보고", "<s2@x>", t.AddSeconds(40), "버전 2");
        Eml("s3.eml", "주간 보고", "<s3@x>", t.AddMinutes(10), "버전 3");

        var r = _scanner.Scan(_root);
        Assert.Equal(DupStatus.Similar, E(r, "s1.eml").Dup);
        Assert.Equal(DupStatus.Similar, E(r, "s2.eml").Dup);
        Assert.Equal(DupStatus.None, E(r, "s3.eml").Dup);
    }

    [Fact]
    public void Corrupt_cache_is_rebuilt_without_auto_trash()
    {
        var a = Eml("a.eml", "제목A", "<a@x>");
        _scanner.Scan(_root);
        File.WriteAllText(IndexStore.IndexPath(_root), "garbage");
        File.Copy(a, Path.Combine(_root, "copy.eml"));

        var r = _scanner.Scan(_root);
        Assert.True(r.CacheWasCorrupt);
        Assert.Empty(r.AutoTrashed);
        Assert.Equal(2, r.Total);
    }

    [Fact]
    public void Fingerprint_only_match_is_flagged_not_trashed()
    {
        // Message-ID 없는 메일: 발신자·시각·제목·본문이 같아도 자동 삭제하지 않음 (표시만)
        var m1 = _f.NewEml("지문 테스트"); m1.Headers.Remove(MimeKit.HeaderId.MessageId);
        m1.Body = new TextPart("plain") { Text = "같은 본문" };
        m1.WriteTo(Path.Combine(_root, "a.eml"));
        _scanner.Scan(_root);

        var m2 = _f.NewEml("지문 테스트"); m2.Headers.Remove(MimeKit.HeaderId.MessageId);
        m2.Headers.Add("X-Other", "바이트가 다른 사본");
        m2.Body = new TextPart("plain") { Text = "같은 본문" };
        var p2 = Path.Combine(_root, "b.eml");
        m2.WriteTo(p2);

        var r = _scanner.Scan(_root);
        Assert.Empty(r.AutoTrashed);
        Assert.True(File.Exists(p2));
        Assert.Contains(r.Entries, e => e.Dup == DupStatus.Duplicate); // 중복 '표시'는 유지
    }

    [Fact]
    public void Nearly_empty_mails_are_never_grouped()
    {
        File.WriteAllText(Path.Combine(_root, "x1.eml"), "X-Only: a\r\nContent-Type: text/plain\r\n\r\n");
        File.WriteAllText(Path.Combine(_root, "x2.eml"), "X-Only: b\r\nContent-Type: text/plain\r\n\r\n");
        var r = _scanner.Scan(_root);
        Assert.All(r.Entries, e => Assert.Equal(DupStatus.None, e.Dup));
    }

    [Fact]
    public void Restored_message_id_duplicate_with_different_bytes_is_not_trashed_again()
    {
        Eml("a.eml", "제목A", "<same@x>");
        _scanner.Scan(_root);
        var other = Eml("b.eml", "제목A", "<same@x>", body: "바이트가 다른 같은 메일");
        _scanner.Scan(_root);                              // 휴지통으로 감
        Assert.False(File.Exists(other));
        File.Move(_trash.Sent.Single(), other);             // 복원
        // 복원 후 사용자가 파일을 조금 고쳐 해시가 달라져도 Message-ID로 기억
        File.AppendAllText(other, "\r\n");

        var r = _scanner.Scan(_root);
        Assert.Empty(r.AutoTrashed);
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Error_files_are_indexed_but_excluded_from_duplicates()
    {
        File.WriteAllBytes(Path.Combine(_root, "bad1.msg"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_root, "bad2.msg"), new byte[] { 1, 2, 3 });
        var r = _scanner.Scan(_root);
        Assert.Equal(2, r.Errors);
        Assert.All(r.Entries, e => Assert.Equal(DupStatus.None, e.Dup));
    }

    [Fact]
    public void Store_roundtrip_preserves_fields()
    {
        var p = _f.SaveMsg("x.msg", e => e.Attachments.Add(new MemoryStream(new byte[] { 1 }), "r.pdf", -1, false, ""));
        File.Move(p, Path.Combine(_root, "x.msg"));
        var first = _scanner.Scan(_root).Entries.Single();
        first.FolderHint = MailDirection.Sent;
        IndexStore.Save(_root, new[] { first });

        var back = IndexStore.Load(_root).Entries.Single();
        Assert.Equal(first.Mail.Subject, back.Mail.Subject);
        Assert.Equal(first.Mail.SenderName, back.Mail.SenderName);
        Assert.Equal(first.Mail.SentTime, back.Mail.SentTime);
        Assert.Equal(first.Mail.ReceivedTime, back.Mail.ReceivedTime);
        Assert.Equal(first.Mail.AttachmentNames, back.Mail.AttachmentNames);
        Assert.Equal(first.Mail.To, back.Mail.To);
        Assert.Equal(first.ContentHash, back.ContentHash);
        Assert.Equal(MailDirection.Sent, back.FolderHint);
        Assert.Equal(first.Mail.FileModifiedUtc, back.Mail.FileModifiedUtc);
    }
}

/// <summary>EIDX_PERF=1 일 때만 실행: 1만 건 성능 측정.</summary>
public class ScannerPerfTests
{
    private readonly ITestOutputHelper _out;
    public ScannerPerfTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void TenThousand_files()
    {
        if (Environment.GetEnvironmentVariable("EIDX_PERF") != "1") return;
        using var f = new SampleFactory();
        var root = Path.Combine(f.Dir, "big");
        const int N = 10000;
        var body = string.Concat(Enumerable.Repeat("안녕하세요. 주간 회의 자료 공유드립니다. ", 60)); // 약 2KB 본문
        for (int i = 0; i < N; i++)
        {
            var dir = Path.Combine(root, "Inbox", $"2026-{i % 12 + 1:00}", $"sub{i % 5}");
            Directory.CreateDirectory(dir);
            var m = f.NewEml($"[{i}] RE: 주간 회의 자료 {i % 300}");
            m.MessageId = $"<perf-{i}@x>";
            m.Date = SampleFactory.Sent.AddMinutes(i);
            m.Body = i % 10 == 0
                ? new Multipart("mixed") { new TextPart("plain") { Text = body }, SampleFactory.Attachment($"첨부{i}.pdf") }
                : (MimeEntity)new TextPart("plain") { Text = body };
            m.WriteTo(Path.Combine(dir, $"mail{i}.eml"));
        }
        var msgSrc = f.SaveMsg("m.msg", e => { e.BodyHtml = "<p>" + body + "</p>"; });
        for (int i = 0; i < 500; i++)
        {
            // msg는 제목·ID를 바꿔 생성할 수 없어 복사본(=중복)으로 파싱 속도만 확인
            File.Copy(msgSrc, Path.Combine(root, $"m{i}.msg"));
        }

        var scanner = new Scanner(new FakeTrash(Path.Combine(f.Dir, "trash")));
        var sw = Stopwatch.StartNew();
        var r1 = scanner.Scan(root);
        var first = sw.Elapsed; sw.Restart();
        var load = IndexStore.Load(root);
        var loadTime = sw.Elapsed; sw.Restart();
        var r2 = scanner.Scan(root);
        var second = sw.Elapsed;

        var size = new FileInfo(IndexStore.IndexPath(root)).Length;
        _out.WriteLine($"PERF files={r1.Total} first={first.TotalSeconds:F1}s cacheLoad={loadTime.TotalMilliseconds:F0}ms " +
                       $"incremental={second.TotalSeconds:F2}s cache={size / 1024}KB cores={Environment.ProcessorCount} dupFlags={r1.Entries.Count(e => e.Dup != DupStatus.None)}");
        Assert.Equal(N + 500, r1.Total);
        Assert.Equal(N + 500, r2.Unchanged);
        Assert.Equal(N + 500, load.Entries.Count);
    }
}

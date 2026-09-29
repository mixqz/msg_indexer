using System;
using System.IO;
using System.Text;
using MimeKit;
using Xunit;

/// <summary>
/// EIDX_EXPORT_SAMPLES=경로 가 설정되면 합성 샘플 세트를 그 폴더에 저장한다
/// (exe 스모크 테스트·UI 개발용). 평소에는 아무것도 하지 않는다.
/// </summary>
public class SampleExport
{
    [Fact]
    public void Export_samples_when_requested()
    {
        var target = Environment.GetEnvironmentVariable("EIDX_EXPORT_SAMPLES");
        if (string.IsNullOrEmpty(target)) return;
        Directory.CreateDirectory(target);
        using var f = new SampleFactory();

        void Eml(MimeMessage m, string name) => File.Copy(f.Save(m, name), Path.Combine(target, name), true);
        void Msg(string name, Action<MsgKit.Email> cfg, string subject) => File.Copy(f.SaveMsg(name, cfg, subject), Path.Combine(target, name), true);

        var a = f.NewEml("RE: 한빛마트 직구 수입 3월분 재전송 요청");
        a.Body = new Multipart("mixed") { new TextPart("plain") { Text = "안녕하세요." }, SampleFactory.Attachment("한빛마트 0301-0315.xlsx") };
        Eml(a, "01_첨부있음.eml");

        var b = f.NewEml("W35 team agenda: 논의/결정 사항?");
        b.MessageId = "<agenda-w35@example.com>";
        b.Body = new MultipartRelated { new TextPart("html") { Text = "<p>본문<img src=\"cid:logo\"></p>" },
            SampleFactory.Attachment("image001.png", "image/png", ContentDisposition.Inline, "logo") };
        Eml(b, "02_서명이미지만.eml");

        foreach (var (method, ps, n) in new[] { ("REQUEST", (string?)null, "초대"), ("REPLY", "ACCEPTED", "수락"),
                     ("REPLY", "DECLINED", "거절"), ("REPLY", "TENTATIVE", "미정"), ("CANCEL", null, "취소") })
        {
            var c = f.NewEml($"{n}: 주간 회의");
            c.MessageId = $"<meeting-{n}@example.com>";
            c.Body = new MultipartAlternative { new TextPart("plain") { Text = n }, SampleFactory.Calendar(method, ps) };
            Eml(c, $"03_일정_{n}.eml");
        }

        Msg("04_msg_첨부.msg", e => { e.InternetMessageId = "<monthly-report@example.com>"; e.Attachments.Add(new MemoryStream(Encoding.UTF8.GetBytes("pdf")), "report.pdf", -1, false, ""); },
            "FW: 월간 보고서");
        Msg("05_msg_중복.msg", _ => { }, "RE: 한빛마트 직구 수입 3월분 재전송 요청"); // 01과 같은 Message-ID
        var sub = Path.Combine(target, "하위폴더", "2단계");
        Directory.CreateDirectory(sub);
        File.Copy(Path.Combine(target, "01_첨부있음.eml"), Path.Combine(sub, "01_복사본.eml"), true);
        File.WriteAllBytes(Path.Combine(target, "06_깨진파일.msg"), new byte[] { 1, 2, 3, 4, 5 });
    }
}

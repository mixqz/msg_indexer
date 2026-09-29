using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Text;
using MimeKit;
using MsgReader.Outlook;

namespace EmailIndexer.Core.View
{
    /// <summary>미리보기 창 내용 (명세 8장).</summary>
    public sealed class PreviewContent
    {
        /// <summary>안전하게 정리된 HTML (외부 이미지·스크립트·자동 이동 제거, 본문 삽입 이미지는 내장).</summary>
        public string Html { get; set; } = "";
        public string Text { get; set; } = "";
        public string? Error { get; set; }
    }

    public static class Preview
    {
        /// <summary>파일에서 본문을 다시 읽는다 (캐시에는 텍스트만 있으므로).</summary>
        public static PreviewContent Load(MailInfo info)
        {
            var pc = new PreviewContent();
            try
            {
                string? html; string? text;
                var images = new Dictionary<string, (string mime, byte[] data)>(StringComparer.OrdinalIgnoreCase);
                if (info.Format == MailFormat.Msg) ReadMsg(info.FilePath, images, out html, out text);
                else ReadEml(info.FilePath, images, out html, out text);

                if (string.IsNullOrWhiteSpace(text)) text = HtmlText.ToPlainText(html);
                pc.Text = CleanText(text);
                pc.Html = string.IsNullOrWhiteSpace(html) ? TextAsHtml(pc.Text) : Sanitize(html!, images);
            }
            catch (Exception ex)
            {
                pc.Error = "본문을 읽지 못했습니다: " + ex.Message;
                pc.Text = CleanText(info.BodyText);
                pc.Html = TextAsHtml(pc.Text);
            }
            return pc;
        }

        private static void ReadMsg(string path, Dictionary<string, (string, byte[])> images, out string? html, out string? text)
        {
            using var msg = new Storage.Message(path, FileAccess.Read);
            text = msg.BodyText;
            try { html = msg.BodyHtml; } catch { html = null; }
            foreach (var a in msg.Attachments.OfType<Storage.Attachment>())
            {
                if (string.IsNullOrEmpty(a.ContentId) || a.Data == null) continue;
                images[a.ContentId.Trim('<', '>')] = (string.IsNullOrEmpty(a.MimeType) ? GuessMime(a.FileName) : a.MimeType, a.Data);
            }
        }

        private static void ReadEml(string path, Dictionary<string, (string, byte[])> images, out string? html, out string? text)
        {
            var msg = MimeMessage.Load(path);
            html = msg.HtmlBody;
            text = msg.TextBody;
            foreach (var part in msg.BodyParts.OfType<MimePart>())
            {
                if (string.IsNullOrEmpty(part.ContentId) || !part.ContentType.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase)) continue;
                using var ms = new MemoryStream();
                part.Content?.DecodeTo(ms);
                images[part.ContentId!.Trim('<', '>')] = (part.ContentType.MimeType, ms.ToArray());
            }
        }

        private static string GuessMime(string? name)
        {
            var ext = (Path.GetExtension(name ?? "") ?? "").ToLowerInvariant();
            return ext == ".png" ? "image/png" : ext == ".gif" ? "image/gif" : ext == ".bmp" ? "image/bmp" : "image/jpeg";
        }

        // ---- 정리 규칙 ----

        private static readonly Regex Banner = new Regex(@"ZjQcmQRYFpfpt\w*", RegexOptions.Compiled);
        private static readonly Regex ManyBlank = new Regex(@"(\r?\n[ \t]*){3,}", RegexOptions.Compiled);

        /// <summary>보안 배너 표식 문구 제거, 과도한 빈 줄 정리.</summary>
        public static string CleanText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var s = Banner.Replace(text, "");
            s = ManyBlank.Replace(s, Environment.NewLine + Environment.NewLine);
            return s.Trim();
        }

        private static readonly RegexOptions RO = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;
        private static readonly Regex DangerBlocks = new Regex(@"<(script|iframe|object|embed|applet|form|frameset|frame)\b.*?(</\1\s*>|/>)", RO);
        private static readonly Regex DangerOpen = new Regex(@"<(script|iframe|object|embed|applet|form|frameset|frame|base|link)\b[^>]*>", RO);
        private static readonly Regex MetaRefresh = new Regex(@"<meta\b[^>]*http-equiv\s*=\s*[""']?refresh[^>]*>", RO);
        private static readonly Regex EventAttr = new Regex(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RO);
        private static readonly Regex JsUrl = new Regex(@"(href|src|action)\s*=\s*([""']?)\s*(javascript|vbscript):[^""'>\s]*\2", RO);
        private static readonly Regex ExternalSrc = new Regex(@"(\s(?:src|background)\s*=\s*)([""']?)(https?:|//|file:)[^""'>\s]*\2", RO);
        private static readonly Regex CssUrl = new Regex(@"url\(\s*([""']?)(https?:|//|file:)[^)]*\)", RO);
        private static readonly Regex CidSrc = new Regex(@"(\s(?:src|background)\s*=\s*)([""']?)cid:([^""'>\s]+)\2", RO);
        private static readonly Regex HeadOpen = new Regex(@"<head\b[^>]*>", RO);

        /// <summary>
        /// 미리보기용 HTML 정리: 스크립트·프레임·폼·이벤트 속성·자동 이동 제거,
        /// 외부 이미지 차단(추적 방지), 본문 삽입 이미지(cid)는 data URI로 내장.
        /// </summary>
        public static string Sanitize(string html, IDictionary<string, (string mime, byte[] data)>? images = null)
        {
            var s = DangerBlocks.Replace(html, "");
            s = DangerOpen.Replace(s, "");
            s = MetaRefresh.Replace(s, "");
            s = EventAttr.Replace(s, "");
            s = JsUrl.Replace(s, "$1=\"#\"");
            s = ExternalSrc.Replace(s, "$1\"about:blank\"");
            s = CssUrl.Replace(s, "none");
            s = CidSrc.Replace(s, m =>
            {
                var key = WebUtility.UrlDecode(m.Groups[3].Value).Trim('<', '>');
                if (images != null && images.TryGetValue(key, out var img))
                    return $"{m.Groups[1].Value}\"data:{img.mime};base64,{Convert.ToBase64String(img.data)}\"";
                return m.Groups[1].Value + "\"about:blank\"";
            });

            const string head = "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\">";
            if (HeadOpen.IsMatch(s)) s = HeadOpen.Replace(s, m => m.Value + head, 1);
            else s = "<html><head>" + head + "</head><body>" + s + "</body></html>";
            return s;
        }

        public static string TextAsHtml(string text)
            => "<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\"></head>" +
               "<body style=\"font-family:'Malgun Gothic',sans-serif;font-size:10pt;white-space:pre-wrap\">" +
               WebUtility.HtmlEncode(text ?? "") + "</body></html>";
    }
}

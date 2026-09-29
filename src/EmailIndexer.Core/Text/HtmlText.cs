using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace EmailIndexer.Core.Text
{
    /// <summary>HTML 본문 → 검색용 텍스트 변환 등 가벼운 도우미.</summary>
    public static class HtmlText
    {
        private static readonly Regex DropBlocks = new Regex(@"<(script|style|head|title)\b[^>]*>.*?</\1\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex Comments = new Regex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex LineBreaks = new Regex(@"<(br|/p|/div|/tr|/li|/h[1-6]|/table)\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Cells = new Regex(@"</t[dh]\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Tags = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"[ \t\u00A0]+", RegexOptions.Compiled);
        private static readonly Regex BlankLines = new Regex(@"(\s*\n){3,}", RegexOptions.Compiled);
        private static readonly Regex Cid = new Regex(@"cid:([^""'\s>)]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ToPlainText(string? html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var s = Comments.Replace(html, "");
            s = DropBlocks.Replace(s, "");
            s = LineBreaks.Replace(s, "\n");
            s = Cells.Replace(s, "\t");
            s = Tags.Replace(s, "");
            s = WebUtility.HtmlDecode(s);
            s = s.Replace("\r\n", "\n");
            s = Spaces.Replace(s, " ");
            s = BlankLines.Replace(s, "\n\n");
            return s.Trim();
        }

        /// <summary>HTML 본문이 참조하는 cid 값 목록 (본문 삽입 이미지 판정용).</summary>
        public static HashSet<string> CidReferences(string? html)
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(html)) return set;
            foreach (Match m in Cid.Matches(html))
                set.Add(WebUtility.UrlDecode(m.Groups[1].Value).Trim('<', '>'));
            return set;
        }
    }
}

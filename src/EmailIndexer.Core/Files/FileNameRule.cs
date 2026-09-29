using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EmailIndexer.Core.Text;
using EmailIndexer.Core.View;

namespace EmailIndexer.Core.Files
{
    /// <summary>
    /// 파일명 정규화 규칙 (명세 4장):
    /// <c>YYMMDD_HHMMSS_발신자_제목_첨부표기.확장자</c> (첨부 표기는 화면 언어별, <see cref="Words"/>)
    /// </summary>
    public static class FileNameRule
    {
        /// <summary>Windows 기본 경로 한도(260) - 끝 NUL = 259자.</summary>
        public const int MaxPath = 259;
        private const string Ellipsis = "…";

        private static readonly Dictionary<char, char> FullWidth = new Dictionary<char, char>
        {
            ['\\'] = '＼', ['/'] = '／', [':'] = '：', ['*'] = '＊', ['?'] = '？',
            ['"'] = '＂', ['<'] = '＜', ['>'] = '＞', ['|'] = '｜',
        };
        /// <summary>
        /// 파일명에 들어가는 언어별 표기. 파일명은 한 번 붙으면 오래 남는 데이터라 번역 파일(.lang)이 아닌
        /// 코드에 고정한다 (번역을 고쳐도 기존 파일이 '미정규화'로 바뀌지 않게).
        /// </summary>
        public sealed class Words
        {
            public string AttYes { get; }
            public string AttNo { get; }
            public string NoSender { get; }
            public string NoSubject { get; }
            public string Unreadable { get; }
            public Words(string attYes, string attNo, string noSender, string noSubject, string unreadable)
            { AttYes = attYes; AttNo = attNo; NoSender = noSender; NoSubject = noSubject; Unreadable = unreadable; }
        }

        private static readonly Words En = new Words("_AttY", "_AttN", "(no sender)", "(no subject)", "(unreadable)");
        private static readonly Dictionary<string, Words> ByLanguage = new Dictionary<string, Words>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = En,
            ["es"] = new Words("_AttY", "_AttN", "(sin remitente)", "(sin asunto)", "(ilegible)"),
            ["fr"] = new Words("_AttY", "_AttN", "(sans expéditeur)", "(sans objet)", "(illisible)"),
            ["ko"] = new Words("_첨부O", "_첨부X", "(발신자 없음)", "(제목 없음)", "(읽기 실패)"),
            ["ja"] = new Words("_添付有", "_添付無", "(差出人なし)", "(件名なし)", "(読み取り失敗)"),
            ["zh"] = new Words("_有附件", "_无附件", "(无发件人)", "(无主题)", "(读取失败)"),
        };

        /// <summary>언어별 표기 (모르는 언어는 영어).</summary>
        public static Words For(string? language)
            => language != null && ByLanguage.TryGetValue(language, out var w) ? w : En;

        /// <summary>현재 화면 언어의 표기.</summary>
        public static Words Current => For(L.Language);

        /// <summary>어느 언어로 붙였든 인식하는 첨부 표기 정규식 조각 (앞의 '_' 포함).</summary>
        public static readonly string AnyAttachmentMarker =
            "(?:" + string.Join("|", ByLanguage.Values.SelectMany(w => new[] { w.AttYes, w.AttNo })
                .Distinct().Select(Regex.Escape)) + ")";

        private static IEnumerable<Words> AllWords => ByLanguage.Values.Distinct();

        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>파일명에 쓸 수 없는 문자 → 모양이 같은 전각 문자, 제어문자·줄바꿈 → 공백 1칸.</summary>
        public static string Clean(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s!.Length);
            foreach (var ch in s)
            {
                if (FullWidth.TryGetValue(ch, out var fw)) sb.Append(fw);
                else if (char.IsControl(ch)) sb.Append(' ');
                else sb.Append(ch);
            }
            return Spaces.Replace(sb.ToString(), " ").Trim();
        }

        /// <summary>규칙에 따른 파일명 (경로 길이 한도 전). 확장자 포함. 표기는 현재 화면 언어.</summary>
        public static string Build(MailRow r) => Build(r, Current);

        public static string Build(MailRow r, Words w)
            => Compose(Prefix(r, w), SubjectPart(r, w), Suffix(r, w), Ext(r));

        /// <summary>
        /// 폴더 경로까지 고려한 최종 파일명. 전체 경로가 259자를 넘을 때만 제목 끝을 줄이고 '…'를 붙인다.
        /// 폴더 경로만으로도 한도를 넘으면 null. 표기는 현재 화면 언어.
        /// </summary>
        public static string? BuildForDirectory(MailRow r, string directory) => BuildForDirectory(r, directory, Current);

        public static string? BuildForDirectory(MailRow r, string directory, Words w)
        {
            var budget = MaxPath - directory.TrimEnd('\\', '/').Length - 1;
            string prefix = Prefix(r, w), subject = SubjectPart(r, w), suffix = Suffix(r, w), ext = Ext(r);
            var full = Compose(prefix, subject, suffix, ext);
            if (full.Length <= budget) return full;

            var fixedLen = Compose(prefix, "", suffix, ext).Length + Ellipsis.Length;
            var room = budget - fixedLen;
            if (room >= 1) return Compose(prefix, CutEnd(subject, room) + Ellipsis, suffix, ext);

            // 폴더가 매우 깊음: 발신자도 줄여 본다 (날짜_시간_ 과 _첨부X.ext는 유지)
            var datePart = prefix.Substring(0, 14); // "YYMMDD_HHMMSS_"
            var sender = prefix.Substring(14);
            var minimal = datePart + Ellipsis + "_" + Ellipsis + suffix + ext;
            if (minimal.Length > budget) return null;
            var roomSender = budget - minimal.Length + Ellipsis.Length;
            return datePart + CutEnd(sender, Math.Max(1, roomSender)) + Ellipsis + "_" + Ellipsis + suffix + ext;
        }

        private static string Prefix(MailRow r, Words w)
        {
            var sender = Clean(r.Mail.SenderName);
            if (sender.Length == 0) sender = Clean(r.Mail.SenderEmail);
            if (sender.Length == 0) sender = w.NoSender;
            return $"{r.LocalTime:yyMMdd}_{r.LocalTime:HHmmss}_{sender}_";
        }

        private static string SubjectPart(MailRow r, Words w)
        {
            var s = Clean(r.Mail.Subject);
            return s.Length == 0 ? w.NoSubject : s;
        }

        private static string Suffix(MailRow r, Words w) => r.Mail.HasAttachments ? w.AttYes : w.AttNo;

        private static string Ext(MailRow r) => r.Mail.Format == Mail.MailFormat.Msg ? ".msg" : ".eml";

        private static string Compose(string prefix, string subject, string suffix, string ext)
        {
            var baseName = (prefix + subject + suffix).TrimEnd('.', ' ');
            var stem = baseName.Split('.')[0];
            if (Reserved.Contains(stem)) baseName += "_";
            return baseName + ext;
        }

        /// <summary>앞에서부터 n자 (서로게이트 쌍을 자르지 않음), 끝 공백·점 제거.</summary>
        private static string CutEnd(string s, int n)
        {
            if (s.Length <= n) return s;
            if (n > 0 && char.IsHighSurrogate(s[n - 1])) n--;
            return s.Substring(0, Math.Max(0, n)).TrimEnd(' ', '.');
        }

        /// <summary>"이름 (2).msg" 형태의 번호 붙은 이름.</summary>
        public static string WithNumber(string fileName, int n)
        {
            var ext = Path.GetExtension(fileName);
            return fileName.Substring(0, fileName.Length - ext.Length) + $" ({n})" + ext;
        }

        /// <summary>현재 이름이 목표 이름(또는 번호 붙은 변형)과 같으면 이미 규칙대로인 것.</summary>
        public static bool IsAlready(string currentName, string target)
        {
            if (currentName.Equals(target, StringComparison.OrdinalIgnoreCase)) return true;
            var ext = Path.GetExtension(target);
            var stem = Regex.Escape(target.Substring(0, target.Length - ext.Length));
            return Regex.IsMatch(currentName, "^" + stem + @" \(\d+\)" + Regex.Escape(ext) + "$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 어느 언어 표기로 붙였든 이 메일의 규칙대로 된 이름이면 true.
        /// 화면 언어를 바꿔도 이미 정리한 파일을 다시 바꾸지 않기 위함.
        /// </summary>
        public static bool IsAlreadyAnyLanguage(string currentName, MailRow r, string directory)
        {
            foreach (var w in AllWords)
            {
                var t = BuildForDirectory(r, directory, w);
                if (t != null && IsAlready(currentName, t)) return true;
            }
            return false;
        }
    }
}

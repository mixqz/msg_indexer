using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EmailIndexer.Core.View;

namespace EmailIndexer.Core.Files
{
    /// <summary>
    /// 파일명 정규화 규칙 (명세 4장):
    /// <c>YYMMDD_HHMMSS_발신자_제목_첨부O|X.확장자</c>
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

        /// <summary>규칙에 따른 파일명 (경로 길이 한도 전). 확장자 포함.</summary>
        public static string Build(MailRow r)
            => Compose(Prefix(r), SubjectPart(r), Suffix(r), Ext(r));

        /// <summary>
        /// 폴더 경로까지 고려한 최종 파일명. 전체 경로가 259자를 넘을 때만 제목 끝을 줄이고 '…'를 붙인다.
        /// 폴더 경로만으로도 한도를 넘으면 null.
        /// </summary>
        public static string? BuildForDirectory(MailRow r, string directory)
        {
            var budget = MaxPath - directory.TrimEnd('\\', '/').Length - 1;
            string prefix = Prefix(r), subject = SubjectPart(r), suffix = Suffix(r), ext = Ext(r);
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

        private static string Prefix(MailRow r)
        {
            var sender = Clean(r.Mail.SenderName);
            if (sender.Length == 0) sender = Clean(r.Mail.SenderEmail);
            if (sender.Length == 0) sender = "(발신자 없음)";
            return $"{r.LocalTime:yyMMdd}_{r.LocalTime:HHmmss}_{sender}_";
        }

        private static string SubjectPart(MailRow r)
        {
            var s = Clean(r.Mail.Subject);
            return s.Length == 0 ? "(제목 없음)" : s;
        }

        private static string Suffix(MailRow r) => r.Mail.HasAttachments ? "_첨부O" : "_첨부X";

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
    }
}

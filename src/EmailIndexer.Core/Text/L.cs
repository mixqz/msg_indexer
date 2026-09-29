using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EmailIndexer.Core.Text
{
    /// <summary>
    /// 다국어 문자열 (exe 안에 내장된 <c>Lang/*.lang</c>).
    /// 형식: <c>키 = 값</c> 한 줄에 하나, <c>#</c> 주석, 값 안의 <c>\n</c>은 줄바꿈.
    /// 개수에 따라 달라지는 문구는 <c>키.one</c> / <c>키.other</c> (<see cref="P"/>).
    /// 없는 키는 영어 → 키 이름 순으로 대신 보여준다 (빈칸이 생기지 않게).
    /// </summary>
    public static class L
    {
        public const string DefaultLanguage = "en";

        /// <summary>지원 언어 (코드, 그 언어로 쓴 이름). 설정 화면 목록 순서.</summary>
        public static readonly IReadOnlyList<(string Code, string NativeName)> Languages = new[]
        {
            ("en", "English"), ("ko", "한국어"), ("es", "Español"), ("fr", "Français"), ("ja", "日本語"), ("zh", "简体中文"),
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Cache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new object();

        private static Dictionary<string, string> _current = Load(DefaultLanguage);
        private static readonly Dictionary<string, string> English = Load(DefaultLanguage);

        /// <summary>현재 언어 코드.</summary>
        public static string Language { get; private set; } = DefaultLanguage;

        /// <summary>현재 언어의 문화권 (날짜·숫자 형식).</summary>
        public static CultureInfo Culture { get; private set; } = CultureFor(DefaultLanguage);

        /// <summary>언어 변경 (앱 시작 시 1회). 모르는 코드면 영어.</summary>
        public static void SetLanguage(string? code)
        {
            code = Normalize(code);
            _current = Load(code);
            Language = code;
            Culture = CultureFor(code);
        }

        public static string Normalize(string? code)
        {
            var c = (code ?? "").Trim().ToLowerInvariant();
            return Languages.Any(l => l.Code == c) ? c : DefaultLanguage;
        }

        /// <summary>번역 문자열.</summary>
        public static string T(string key) => Lookup(_current, key);

        /// <summary>자리표시 <c>{0}</c>… 를 채운 번역 문자열.</summary>
        public static string F(string key, params object?[] args) => Format(T(key), args);

        /// <summary>개수 문구: <c>키.one</c>/<c>키.other</c> 중 언어 규칙에 맞는 것 + <c>{0}</c>=개수.</summary>
        public static string P(string key, long count, params object?[] more)
        {
            var form = PluralForm(Language, count);
            var text = TryGet(_current, $"{key}.{form}") ?? TryGet(_current, key + ".other")
                       ?? TryGet(English, $"{key}.{PluralForm(DefaultLanguage, count)}") ?? TryGet(English, key + ".other") ?? key;
            var args = new object?[] { count.ToString("#,0", Culture) }.Concat(more).ToArray();
            return Format(text, args);
        }

        /// <summary>테스트·설정 화면용: 특정 언어로 조회 (현재 언어를 바꾸지 않음).</summary>
        public static string TIn(string language, string key) => Lookup(Load(Normalize(language)), key);

        /// <summary>언어 파일 원본 (검증 테스트용).</summary>
        public static IReadOnlyDictionary<string, string> Raw(string language) => Load(language);

        // 영어·스페인어·프랑스어: 단수/복수 구분. 프랑스어는 0과 1이 단수. 한·중·일은 구분 없음.
        internal static string PluralForm(string lang, long n)
        {
            switch (lang)
            {
                case "en": case "es": return n == 1 ? "one" : "other";
                case "fr": return n == 0 || n == 1 ? "one" : "other";
                default: return "other";
            }
        }

        private static string Lookup(Dictionary<string, string> d, string key)
            => TryGet(d, key) ?? TryGet(English, key) ?? key;

        /// <summary>
        /// 화면 점검용: 환경 변수 EIDX_PSEUDO=1이면 모든 문구를 35% 늘려 표시
        /// (스페인어·프랑스어처럼 긴 번역에서 잘리는 곳을 미리 찾기 위함).
        /// </summary>
        internal static bool Pseudo = Environment.GetEnvironmentVariable("EIDX_PSEUDO") == "1";

        private static string? TryGet(Dictionary<string, string> d, string key)
        {
            if (!d.TryGetValue(key, out var v) || v.Length == 0) return null;
            if (!Pseudo) return v;
            var pad = (int)Math.Ceiling(v.Length * 0.35);
            return "[" + v + new string('~', Math.Max(2, pad)) + "]";
        }

        private static string Format(string text, object?[] args)
        {
            if (args == null || args.Length == 0) return text;
            try { return string.Format(Culture, text, args); }
            catch (FormatException) { return text; } // 번역 실수로 자리표시가 깨져도 앱이 멈추지 않게
        }

        private static CultureInfo CultureFor(string code)
        {
            var name = code == "zh" ? "zh-CN" : code;
            try { return CultureInfo.GetCultureInfo(name); } catch { return CultureInfo.InvariantCulture; }
        }

        private static Dictionary<string, string> Load(string code)
        {
            lock (Gate)
            {
                if (Cache.TryGetValue(code, out var cached)) return cached;
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                // 한 언어가 여러 파일로 나뉠 수 있음: Lang/en.lang, Lang/en.main.lang, Lang/en.help.lang …
                var asm = typeof(L).Assembly;
                var names = asm.GetManifestResourceNames()
                    .Where(n => n == "lang." + code || n.StartsWith("lang." + code + ".", StringComparison.Ordinal))
                    .OrderBy(n => n, StringComparer.Ordinal);
                foreach (var name in names)
                    using (var s = asm.GetManifestResourceStream(name))
                        if (s != null) Parse(new StreamReader(s, Encoding.UTF8).ReadToEnd(), d);
                Cache[code] = d;
                return d;
            }
        }

        internal static void Parse(string text, Dictionary<string, string> into)
        {
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                into[key] = Unescape(value);
            }
        }

        private static string Unescape(string v)
        {
            var sb = new StringBuilder(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] == '\\' && i + 1 < v.Length)
                {
                    var n = v[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                }
                else sb.Append(v[i]);
            }
            return sb.ToString();
        }

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.Compiled);

        /// <summary>문자열 안의 자리표시 번호 집합 (검증용).</summary>
        public static SortedSet<int> Placeholders(string s)
            => new SortedSet<int>(Placeholder.Matches(s).Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)));
    }
}

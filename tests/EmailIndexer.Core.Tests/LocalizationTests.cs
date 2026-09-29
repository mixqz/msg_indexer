using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EmailIndexer.Core.Mail;
using EmailIndexer.Core.Text;
using EmailIndexer.Core.View;
using Xunit;

/// <summary>번역 파일 검증: 키 누락, 자리표시 불일치, 빈 값, 복수형, 대체 동작, 코드 안의 하드코딩 문구.</summary>
public class LocalizationTests
{
    private static readonly IReadOnlyDictionary<string, string> En = L.Raw("en");

    /// <summary>내장된 번역 파일이 있는 언어 (영어 제외).</summary>
    public static IEnumerable<object[]> TranslatedLanguages()
        => L.Languages.Select(l => l.Code).Where(c => c != "en" && L.Raw(c).Count > 0).Select(c => new object[] { c });

    [Fact]
    public void English_source_is_loaded_and_is_the_default()
    {
        Assert.True(En.Count > 50);
        Assert.Equal("en", L.DefaultLanguage);
        Assert.Equal("en", L.Normalize(null));
        Assert.Equal("en", L.Normalize("xx"));
        Assert.Equal("ko", L.Normalize(" KO "));
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_language_has_exactly_the_english_keys(string lang)
    {
        var d = L.Raw(lang);
        var missing = En.Keys.Except(d.Keys).OrderBy(k => k).ToList();
        var extra = d.Keys.Except(En.Keys).Where(k => !IsPluralVariant(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"{lang}: missing keys: {string.Join(", ", missing)}");
        Assert.True(extra.Count == 0, $"{lang}: unknown keys: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Placeholders_match_english(string lang)
    {
        var bad = new List<string>();
        foreach (var kv in L.Raw(lang))
        {
            var enKey = En.ContainsKey(kv.Key) ? kv.Key : PluralOther(kv.Key);
            if (enKey == null || !En.TryGetValue(enKey, out var en)) continue;
            if (!L.Placeholders(en).SetEquals(L.Placeholders(kv.Value))) bad.Add($"{kv.Key}: en={en} | {lang}={kv.Value}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void No_empty_values_and_valid_format_strings(string lang)
    {
        foreach (var kv in L.Raw(lang))
        {
            Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}:{kv.Key} is empty");
            // string.Format으로 실제 채워 봐서 중괄호 실수를 잡는다
            var args = Enumerable.Range(0, 10).Select(i => (object)("#" + i)).ToArray();
            var ex = Record.Exception(() => string.Format(kv.Value, args));
            Assert.True(ex == null, $"{lang}:{kv.Key} is not a valid format string: {kv.Value}");
        }
    }

    [Fact]
    public void English_plural_keys_come_in_pairs()
    {
        foreach (var k in En.Keys.Where(k => k.EndsWith(".one")))
            Assert.True(En.ContainsKey(k.Substring(0, k.Length - 4) + ".other"), k);
    }

    [Fact]
    public void Missing_translation_falls_back_to_english_then_key()
    {
        Assert.Equal("Sent", L.TIn("zz", "dir.sent"));         // 모르는 언어 → 영어
        Assert.Equal("no.such.key", L.T("no.such.key"));        // 없는 키 → 키 이름
        Assert.Equal("보냄", L.TIn("ko", "dir.sent"));
    }

    [Fact]
    public void Plural_rules_per_language()
    {
        Assert.Equal("one", L.PluralForm("en", 1));
        Assert.Equal("other", L.PluralForm("en", 0));
        Assert.Equal("one", L.PluralForm("fr", 0));
        Assert.Equal("one", L.PluralForm("fr", 1));
        Assert.Equal("other", L.PluralForm("fr", 2));
        Assert.Equal("other", L.PluralForm("ja", 1));
    }

    [Fact]
    public void Parser_handles_escapes_comments_and_equals_in_value()
    {
        var d = new Dictionary<string, string>();
        L.Parse("# comment\n a.b = x = y \\n next\\\\\n\nbad line\n", d);
        Assert.Equal("x = y \n next\\", d["a.b"]);
        Assert.Single(d);
    }

    [Fact]
    public void Error_codes_are_localized_at_display_time()
    {
        Assert.Equal("Empty file", Display.Error(new MailInfo { ParseError = "err.empty\t" }));
        Assert.Contains("EndOfStream", Display.Error(new MailInfo { ParseError = "err.corrupt\tEndOfStream: x" }));
        Assert.Equal("오래된 문장", Display.Error(new MailInfo { ParseError = "오래된 문장" })); // 이전 캐시
    }

    [Fact]
    public void Reader_stores_language_neutral_error_codes()
    {
        using var f = new SampleFactory();
        var info = MailReader.Read(f.SaveBytes("e.eml", new byte[0]));
        Assert.StartsWith("err.empty", info.ParseError);
    }

    /// <summary>
    /// Core 코드에 번역되지 않은 한글 문구가 새로 들어오지 않게 막는다.
    /// (파일명 규칙의 표기는 Stage 3에서 언어별로 바뀌므로 잠시 허용)
    /// </summary>
    [Fact]
    public void Core_has_no_hardcoded_korean_ui_text()
    {
        var src = FindRepoDir("src/EmailIndexer.Core");
        var allowed = new[] { "\"(발신자 없음)\"", "\"(제목 없음)\"", "\"_첨부O\"", "\"_첨부X\"", "첨부[OX]", "(읽기 실패)", "\"한국어\"" };
        var hits = new List<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//")) continue;
                foreach (Match m in Regex.Matches(line, "\"(?:[^\"\\\\]|\\\\.)*\""))
                    if (Regex.IsMatch(m.Value, "[가-힣]") && !allowed.Any(a => line.Contains(a)))
                        hits.Add($"{Path.GetFileName(file)}:{i + 1}: {m.Value}");
            }
        }
        Assert.True(hits.Count == 0, "Hard-coded Korean text (use L.T):\n" + string.Join("\n", hits));
    }

    /// <summary>화면(App) 코드도 같은 검사. 언어 설정 라벨만 일부러 두 언어로 표시.</summary>
    [Fact]
    public void App_has_no_hardcoded_korean_ui_text()
    {
        var src = FindRepoDir("src/EmailIndexer.App");
        var hits = new List<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("//")) continue;
                foreach (Match m in Regex.Matches(lines[i], "\"(?:[^\"\\\\]|\\\\.)*\""))
                    if (Regex.IsMatch(m.Value, "[가-힣]") && m.Value != "\"Language / 언어\"")
                        hits.Add($"{Path.GetFileName(file)}:{i + 1}: {m.Value}");
            }
        }
        Assert.True(hits.Count == 0, "Hard-coded Korean text (use L.T):\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Every_key_used_in_code_exists_in_english()
    {
        var used = new SortedSet<string>();
        foreach (var dir in new[] { "src/EmailIndexer.App", "src/EmailIndexer.Core" })
            foreach (var file in Directory.GetFiles(FindRepoDir(dir), "*.cs", SearchOption.AllDirectories))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "L\\.(?:T|F|TIn\\([^,]+,)\\s*\\(?\\s*\"([a-zA-Z][\\w.]+)\""))
                    used.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(string.Join("\n", Directory.GetFiles(FindRepoDir("src"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)), "L\\.P\\(\\s*\"([\\w.]+)\""))
            used.Add(m.Groups[1].Value + ".other");
        var missing = used.Where(k => !En.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Keys used in code but missing from en: " + string.Join(", ", missing));
    }

    private static bool IsPluralVariant(string key) => key.EndsWith(".one") || key.EndsWith(".other");

    private static string? PluralOther(string key)
        => key.EndsWith(".one") ? key.Substring(0, key.Length - 4) + ".other" : null;

    private static string FindRepoDir(string rel)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, rel))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, rel);
    }
}

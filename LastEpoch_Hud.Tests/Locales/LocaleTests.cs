using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Tests.Support;

namespace LastEpoch_Hud.Tests.Locales;

/// <summary>Menu text: every key in base.json is translated, and every locale file ships.</summary>
public sealed partial class LocaleTests
{
    private static readonly string _localesDir = Path.Combine(
        GameEnvironment.ModProjectDir,
        "LastEpoch_Hud",
        "Locales"
    );
    private static readonly string[] _translatedLanguages = ["fr", "ko", "zh"];
    private static readonly string[] _languages = ["en", .. _translatedLanguages];
    private static readonly HashSet<string> _keepEnglish = ListFile.Load("Locales/KeepEnglish.txt");

    public static TheoryData<string> LanguageData => new(_languages);
    public static TheoryData<string> TranslatedLanguageData => new(_translatedLanguages);
    public static TheoryData<string> ShippedNonEnglishLanguageData =>
        new(
            Directory
                .GetFiles(_localesDir, "*.json")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => name != "base" && name != "en")
                .Order()
        );

    [Theory]
    [MemberData(nameof(LanguageData))]
    public void Language_TranslatesEveryBaseKey(string language)
    {
        Dictionary<string, string> translations = Read(language);
        var missing = Read("base")
            .Keys.Where(key => string.IsNullOrWhiteSpace(translations.GetValueOrDefault(key)))
            .ToList();

        Assert.True(missing.Count == 0, $"{language}.json misses: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(LanguageData))]
    public void Language_HasExactlyTheBaseKeys(string language)
    {
        HashSet<string> expected = Read("base").Keys.ToHashSet();
        HashSet<string> actual = Read(language).Keys.ToHashSet();

        Assert.True(
            expected.SetEquals(actual),
            $"{language}.json key mismatch. Missing: {string.Join(", ", expected.Except(actual))}; "
                + $"extra: {string.Join(", ", actual.Except(expected))}"
        );
    }

    [Fact]
    public void LocaleAliases_PointToBaseKeys()
    {
        HashSet<string> keys = Read("base").Keys.ToHashSet();
        var missing = HudLocaleKey
            .KnownAliases.Where(alias => !keys.Contains(alias.Value))
            .Select(alias => $"{alias.Key} -> {alias.Value}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Locale aliases point to missing base keys: " + string.Join(", ", missing)
        );
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguageData))]
    public void Language_HasNoEnglishLeftovers(string language)
    {
        Dictionary<string, string> english = Read("en");
        var leftovers = Read(language)
            .Where(pair => SameAsEnglish(english.GetValueOrDefault(pair.Key), pair.Value))
            .Select(pair => pair.Key)
            .Where(key => !_keepEnglish.Contains($"{language}:{key}"))
            .ToList();

        Assert.True(
            leftovers.Count == 0,
            $"{language}.json still English ({leftovers.Count}): {string.Join(", ", leftovers)}"
        );
    }

    [Theory]
    [MemberData(nameof(ShippedNonEnglishLanguageData))]
    public void Language_KeepsTemplatePlaceholders(string language)
    {
        Dictionary<string, string> english = Read("en");
        var changed = Read(language)
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .Where(pair => english.ContainsKey(pair.Key))
            .Where(pair => Placeholders(english[pair.Key]) != Placeholders(pair.Value))
            .Select(pair => pair.Key)
            .ToList();

        Assert.True(
            changed.Count == 0,
            $"{language}.json changes {{n}} placeholders of: {string.Join(", ", changed)}"
        );
    }

    [Fact]
    public void LocalesFolder_ShipsOnlyBaseEnFrKoZh()
    {
        string[] shipped = Directory
            .GetFiles(_localesDir, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order()
            .ToArray();

        Assert.Equal(["base", "en", "fr", "ko", "zh"], shipped);
    }

    [Fact]
    public void KeepEnglish_EntriesAreStillEnglishAndExist()
    {
        var baseKeys = Read("base").Keys.ToHashSet();
        Dictionary<string, string> english = Read("en");
        var translations = _translatedLanguages.ToDictionary(language => language, Read);
        var broken = _keepEnglish
            .Where(entry => !IsValidKeepEnglish(entry, baseKeys, english, translations))
            .ToList();

        Assert.True(
            broken.Count == 0,
            "Malformed, unknown or stale KeepEnglish.txt entries (<lang>:<key>); fix or remove them:\n"
                + string.Join("\n", broken)
        );
    }

    [Fact]
    public void SettingLabels_HaveBaseKeys()
    {
        string source = File.ReadAllText(
            SourceLayout.Find("ModSettings.cs")
        );
        var keys = Read("base").Keys.ToHashSet();
        var missing = LabelPattern()
            .Matches(source)
            .Select(m => m.Groups[1].Value)
            .Where(label => !keys.Contains(HudLocaleKey.Canonicalize(label)))
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, $"base.json misses labels: {string.Join(", ", missing)}");
    }

    [Fact]
    public void LocaleFiles_AreCopiedToBuildOutput()
    {
        HashSet<string> copied = CopiedFiles();
        var missing = Directory
            .GetFiles(_localesDir, "*.json")
            .Select(path => $@"LastEpoch_Hud\Locales\{Path.GetFileName(path)}")
            .Where(file => !copied.Contains(file))
            .Where(file => !KnownIssues.Contains($"locale-copy:{Path.GetFileName(file)}"))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Not copied by LastEpoch_Hud.csproj: {string.Join(", ", missing)}"
        );
    }

    [Fact]
    public void KnownUncopiedLocales_AreStillUncopied()
    {
        HashSet<string> copied = CopiedFiles();
        var fixedOnes = KnownIssues
            .WithPrefix("locale-copy:")
            .Where(id => copied.Contains($@"LastEpoch_Hud\Locales\{id["locale-copy:".Length..]}"))
            .ToList();

        Assert.True(
            fixedOnes.Count == 0,
            "Fixed; remove from KnownIssues.txt:\n" + string.Join("\n", fixedOnes)
        );
    }

    private static string Placeholders(string text) =>
        string.Join(
            ",",
            PlaceholderPattern().Matches(text).Select(m => m.Value).Distinct().Order()
        );

    private static bool SameAsEnglish(string english, string translated) =>
        string.Equals(english, translated, StringComparison.OrdinalIgnoreCase);

    private static bool IsValidKeepEnglish(
        string entry,
        HashSet<string> baseKeys,
        Dictionary<string, string> english,
        Dictionary<string, Dictionary<string, string>> translations
    )
    {
        string[] parts = entry.Split(':', 2);
        if (
            parts.Length != 2
            || !translations.TryGetValue(parts[0], out Dictionary<string, string> language)
        )
        {
            return false;
        }

        return baseKeys.Contains(parts[1])
            && SameAsEnglish(
                english.GetValueOrDefault(parts[1]),
                language.GetValueOrDefault(parts[1])
            );
    }

    private static Dictionary<string, string> Read(string language) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(_localesDir, $"{language}.json"))
        );

    private static HashSet<string> CopiedFiles() =>
        XDocument
            .Load(Path.Combine(GameEnvironment.ModProjectDir, "LastEpoch_Hud.csproj"))
            .Descendants()
            .Where(e =>
                e.Name.LocalName == "None"
                && e.Elements().Any(c => c.Name.LocalName == "CopyToOutputDirectory")
            )
            .Select(e => (string)e.Attribute("Update") ?? (string)e.Attribute("Include") ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex("label:\\s*\"([^\"]+)\"")]
    private static partial Regex LabelPattern();
}

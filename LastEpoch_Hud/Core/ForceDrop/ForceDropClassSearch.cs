using System;
using System.Collections.Generic;
using System.Text;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

// Masteries inherit their base class's specificity. Item compatibility belongs
// to Legal mode validation; the search mask must never depend on the selected item.
public static class ForceDropClassSearch
{
    // Order matches the Force Drop picker masks: Acolyte, Mage, Primalist,
    // Rogue, Sentinel. A fully generic affix belongs to every class search.
    public const int AllClasses = 31;

    static readonly Dictionary<string, int> ClassIndices = BuildAliases();

    static Dictionary<string, int> BuildAliases()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[][] names =
        {
            new[] { "Acolyte", "Aco", "Lich", "Necromancer", "Warlock" },
            new[] { "Mage", "Sorcerer", "Spellblade", "Rune Master", "Runemaster" },
            new[] { "Primalist", "Beastmaster", "Beast Master", "Shaman", "Druid" },
            new[] { "Rogue", "Bladedancer", "Blade Dancer", "Marksman", "Falconer" },
            new[]
            {
                "Sentinel",
                "Forge Guard",
                "Forgeguard",
                "Void Knight",
                "Voidknight",
                "Paladin",
            },
        };
        for (int i = 0; i < names.Length; i++)
            foreach (string alias in names[i])
                result[Compact(alias)] = i;
        return result;
    }

    static string Compact(string value)
    {
        var result = new StringBuilder();
        foreach (char c in value ?? "")
            if (char.IsLetterOrDigit(c))
                result.Append(char.ToLowerInvariant(c));
        return result.ToString();
    }

    public static bool TryBaseClass(string mastery, out int classIndex) =>
        ClassIndices.TryGetValue(Compact(mastery), out classIndex);

    // Il2Cpp AffixList.Affix.classSpecificity is an enum, passed in as its
    // untranslated name. If metadata is missing or unfamiliar, preserve the
    // affix as generic rather than excluding it from all class searches.
    public static int MaskFromSpecificity(string specificity)
    {
        if (string.IsNullOrWhiteSpace(specificity))
            return AllClasses;
        string normalized = Compact(specificity);
        if (
            normalized == "none"
            || normalized == "any"
            || normalized == "all"
            || normalized.Contains("nonspecific")
        )
            return AllClasses;

        int mask = 0;
        foreach (
            string name in specificity.Split(
                new[] { ',', '|', '+' },
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            if (!TryBaseClass(name, out int index))
                return AllClasses;
            mask |= 1 << index;
        }
        return mask == 0 ? AllClasses : mask;
    }

    public static bool Matches(string query, int classMask, Func<string, bool> textMatches)
    {
        if (textMatches == null)
            throw new ArgumentNullException(nameof(textMatches));
        if (classMask < 0)
            return textMatches(query);
        int included = 0,
            excluded = 0;
        var textTerms = new List<string>();
        string[] words = (query ?? "").Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries
        );
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            bool exclude = word.StartsWith("-", StringComparison.Ordinal);
            string part = exclude ? word.Substring(1) : word;
            if (string.Equals(part, "class:all", StringComparison.OrdinalIgnoreCase))
                continue;
            bool explicitlyClass = part.StartsWith("class:", StringComparison.OrdinalIgnoreCase);
            string name = explicitlyClass ? part.Substring(6) : part;
            // Support natural two-word mastery names as well as class:forgeguard.
            bool recognized = TryBaseClass(name, out int index);
            if (!recognized && i + 1 < words.Length)
            {
                recognized = TryBaseClass(name + words[i + 1], out index);
                if (recognized)
                    i++;
            }
            if (!recognized)
            {
                textTerms.Add(word);
                continue;
            }
            if (exclude)
                excluded |= 1 << index;
            else
                included |= 1 << index;
        }
        bool generic = classMask == AllClasses;
        if (!generic && included != 0 && (classMask & included) == 0)
            return false;
        if (!generic && (classMask & excluded) != 0)
            return false;
        return textMatches(string.Join(" ", textTerms));
    }
}

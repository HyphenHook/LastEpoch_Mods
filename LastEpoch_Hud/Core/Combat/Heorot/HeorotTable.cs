using System;
using System.Collections.Generic;
using System.Globalization;
using LastEpoch_Hud.Scripts.Core.IO;

namespace LastEpoch_Hud.Scripts.Core.Heorot;

/// <summary>One native stat a Heorot buff grants, as written in HeorotStats.csv.</summary>
public sealed class HeorotStatRow
{
    public string Buff = "";
    /// <summary>"Added", "Increased", or "AilmentChance" (Property is then an ailment name such as Chill).</summary>
    public string Kind = "";
    public string Property = "";
    public string Tags = "";
    public float Value;
}

/// <summary>Reads HeorotStats.csv and HeorotValues.csv. Engine-independent, so it can be tested without the game.</summary>
public static class HeorotTable
{
    public static readonly string[] StatColumns = { "Buff", "Kind", "Property", "Tags", "Value" };
    public static readonly string[] ValueColumns = { "Name", "Value", "Note" };

    public static List<HeorotStatRow> ParseStats(string text, List<string> errors)
    {
        var rows = new List<HeorotStatRow>();
        bool header = false;
        foreach (var (number, line) in CsvText.DataLines(text))
        {
            var f = CsvText.SplitFields(line);
            if (!header)
            {
                header = true;
                if (f.Count != StatColumns.Length || !string.Equals(f[0], "Buff", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("HeorotStats.csv line " + number + ": the header row is missing or has the wrong columns.");
                    return rows;
                }
                continue;
            }
            if (f.Count != StatColumns.Length) { errors?.Add("HeorotStats.csv line " + number + ": expected " + StatColumns.Length + " columns."); continue; }
            string kind = f[1].Trim();
            if (kind != "Added" && kind != "Increased" && kind != "AilmentChance")
            {
                errors?.Add("HeorotStats.csv line " + number + ": Kind must be Added, Increased or AilmentChance.");
                continue;
            }
            if (!float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
            {
                errors?.Add("HeorotStats.csv line " + number + ": Value is not a number.");
                continue;
            }
            rows.Add(new HeorotStatRow { Buff = f[0].Trim(), Kind = kind, Property = f[2].Trim(), Tags = f[3].Trim(), Value = value });
        }
        return rows;
    }

    /// <summary>Name,Value,Note rows. Unknown names are kept, so a new buff can add its own numbers without code changes here.</summary>
    public static Dictionary<string, float> ParseValues(string text, List<string> errors)
    {
        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        bool header = false;
        foreach (var (number, line) in CsvText.DataLines(text))
        {
            var f = CsvText.SplitFields(line);
            if (!header)
            {
                header = true;
                if (f.Count != ValueColumns.Length || !string.Equals(f[0], "Name", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("HeorotValues.csv line " + number + ": the header row is missing or has the wrong columns.");
                    return values;
                }
                continue;
            }
            if (f.Count != ValueColumns.Length) { errors?.Add("HeorotValues.csv line " + number + ": expected " + ValueColumns.Length + " columns."); continue; }
            string name = f[0].Trim();
            if (name.Length == 0 || !float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
            {
                errors?.Add("HeorotValues.csv line " + number + ": needs a Name and a numeric Value.");
                continue;
            }
            if (!values.TryAdd(name, value)) errors?.Add("HeorotValues.csv line " + number + ": " + name + " appears twice; the first one is used.");
        }
        return values;
    }
}

/// <summary>The named numbers from HeorotValues.csv. Anything not in the file keeps its built-in default.</summary>
public static class HeorotValues
{
    public const string IncreasedColdPerChillName = "IncreasedColdPerChill";
    public const string AdditionalChillStacksName = "AdditionalChillStacks";

    private static IReadOnlyDictionary<string, float> current;

    public static void Set(IReadOnlyDictionary<string, float> values) => current = values;

    public static float Get(string name, float fallback) =>
        current != null && current.TryGetValue(name, out float value) ? value : fallback;
}

/// <summary>The named numbers from VolcanicValues.csv (Bhuldar’s Wrath, Logi’s Hunger and Herkir’s Vessel). Anything missing keeps its default.</summary>
public static class VolcanicValues
{
    public const string IncreasedFirePerIgniteName = "IncreasedFirePerIgnite";
    public const string FireLeechName = "FireLeech";
    public const string NecroticLeechName = "NecroticLeech";
    public const string IgniteAttackersChanceName = "IgniteAttackersChance";
    public const string IgniteDamagePerResistanceName = "IgniteDamagePerResistance";

    private static IReadOnlyDictionary<string, float> current;

    public static void Set(IReadOnlyDictionary<string, float> values) => current = values;

    public static float Get(string name, float fallback) =>
        current != null && current.TryGetValue(name, out float value) ? value : fallback;

    public static float IncreasedFirePerIgnite => Math.Max(0f, Get(IncreasedFirePerIgniteName, .25f));
    public static float FireLeech => Math.Max(0f, Get(FireLeechName, .02f));
    public static float NecroticLeech => Math.Max(0f, Get(NecroticLeechName, .02f));
    public static float IgniteAttackersChance => Math.Max(0f, Get(IgniteAttackersChanceName, 1f));
    /// <summary>Fire damage each Ignite on an attacker carries, per 1% of the wearer's uncapped Fire Resistance.</summary>
    public static float IgniteDamagePerResistance => Math.Max(0f, Get(IgniteDamagePerResistanceName, 10f));

    /// <summary>The Fire damage of one Ignite on an attacker, for a wearer with this uncapped Fire Resistance (0.75 = 75%, 1.2 = 120%).</summary>
    public static float IgniteDamage(float fireResistance) =>
        float.IsFinite(fireResistance) && fireResistance > 0 ? fireResistance * 100f * IgniteDamagePerResistance : 0f;

    /// <summary>Wrath’s increased Fire Damage for a target carrying <paramref name="igniteStacks" /> stacks of Ignite.</summary>
    public static float WrathIncreasedFire(float igniteStacks, float increasedEffect = 0f) =>
        float.IsFinite(igniteStacks) && igniteStacks > 0
            ? IncreasedFirePerIgnite * igniteStacks * (1f + Math.Max(0f, increasedEffect)) : 0f;

    /// <summary>Health returned for a hit with the given Fire and Necrotic damage.</summary>
    public static float LeechedHealth(float fireDamage, float necroticDamage) =>
        (float.IsFinite(fireDamage) ? Math.Max(0f, fireDamage) * FireLeech : 0f)
        + (float.IsFinite(necroticDamage) ? Math.Max(0f, necroticDamage) * NecroticLeech : 0f);

    /// <summary>How many Ignite stacks an attacker gets: the whole part of the chance, plus one more with the chance’s remainder.</summary>
    public static int IgniteStacks(float chance, double roll)
    {
        if (!float.IsFinite(chance) || chance <= 0) return 0;
        int whole = (int)Math.Floor(chance);
        return whole + (roll < chance - whole ? 1 : 0);
    }
}

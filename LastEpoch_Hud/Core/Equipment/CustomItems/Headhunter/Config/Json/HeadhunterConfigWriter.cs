using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>Turns a config into indented JSON text.</summary>
public static class HeadhunterConfigWriter
{
    public static string Write(HeadhunterConfig config)
    {
        var root = new JObject
        {
            [HeadhunterConfigKeys.Version] = config.Version,
            [HeadhunterConfigKeys.DefaultsVersion] = HeadhunterConfigDefaults.DefaultsVersion,
            [HeadhunterConfigKeys.DurationSeconds] = config.DurationSeconds,
            [HeadhunterConfigKeys.MaxStacks] = config.MaxStacks,
            [HeadhunterConfigKeys.Triggers] = BuildTriggers(config.Triggers),
            [HeadhunterConfigKeys.Stats] = BuildStats(config.Stats),
            [HeadhunterConfigKeys.AffixMap] = BuildAffixMap(config.AffixMap),
            [HeadhunterConfigKeys.ModelSize] = BuildModelSize(config.ModelSize),
            [HeadhunterConfigKeys.Bar] = BuildBar(config.Bar),
            [HeadhunterConfigKeys.Aura] = BuildAura(config.Aura),
        };
        return root.ToString(Formatting.Indented);
    }

    internal static JObject BuildStat(HeadhunterStatEntry entry)
    {
        var row = new JObject { [HeadhunterConfigKeys.Stat] = entry.Stat };
        if (!string.IsNullOrEmpty(entry.Tag))
        {
            row[HeadhunterConfigKeys.Tag] = entry.Tag;
        }

        row[HeadhunterConfigKeys.Added] = entry.Added;
        row[HeadhunterConfigKeys.Increased] = entry.Increased;
        row[HeadhunterConfigKeys.Enabled] = entry.Enabled;
        return row;
    }

    internal static JObject BuildAffix(HeadhunterAffixEntry entry)
    {
        var row = new JObject { [HeadhunterConfigKeys.ModKey] = entry.ModKey };
        if (!string.IsNullOrEmpty(entry.Note))
        {
            row[HeadhunterConfigKeys.Note] = entry.Note;
        }

        row[HeadhunterConfigKeys.Rows] = new JArray(entry.Rows);
        return row;
    }

    internal static JObject BuildModelSize(HeadhunterSizeCurve curve)
    {
        return new JObject
        {
            [HeadhunterConfigKeys.PerBuff] = curve.PerBuffPercent,
            [HeadhunterConfigKeys.Cap] = curve.CapPercent,
        };
    }

    internal static JObject BuildBar(HeadhunterBarSettings bar)
    {
        return new JObject
        {
            [HeadhunterConfigKeys.OffsetX] = bar.OffsetX,
            [HeadhunterConfigKeys.OffsetY] = bar.OffsetY,
            [HeadhunterConfigKeys.IconSize] = bar.IconSize,
            [HeadhunterConfigKeys.PerRow] = bar.PerRow,
        };
    }

    internal static JObject BuildAura(HeadhunterAuraCurve aura)
    {
        return new JObject
        {
            [HeadhunterConfigKeys.Enabled] = aura.Enabled,
            [HeadhunterConfigKeys.PerBuff] = ShortDouble(aura.PerBuff),
            [HeadhunterConfigKeys.Cap] = ShortDouble(aura.Cap),
        };
    }

    /// <summary>The double a parsed file holds for this float's text, so equal values compare equal.</summary>
    private static double ShortDouble(float value)
    {
        return (double)(decimal)value;
    }

    private static JObject BuildTriggers(HeadhunterTriggers triggers)
    {
        return new JObject
        {
            [HeadhunterConfigKeys.Magic] = triggers.Magic,
            [HeadhunterConfigKeys.Rare] = triggers.Rare,
            [HeadhunterConfigKeys.Boss] = triggers.Boss,
            [HeadhunterConfigKeys.Miniboss] = triggers.Miniboss,
            [HeadhunterConfigKeys.MinionKills] = triggers.MinionKills,
        };
    }

    private static JArray BuildAffixMap(IReadOnlyList<HeadhunterAffixEntry> map)
    {
        var array = new JArray();
        foreach (HeadhunterAffixEntry entry in map)
        {
            array.Add(BuildAffix(entry));
        }
        return array;
    }

    private static JArray BuildStats(IReadOnlyList<HeadhunterStatEntry> stats)
    {
        var array = new JArray();
        foreach (HeadhunterStatEntry entry in stats)
        {
            array.Add(BuildStat(entry));
        }
        return array;
    }
}

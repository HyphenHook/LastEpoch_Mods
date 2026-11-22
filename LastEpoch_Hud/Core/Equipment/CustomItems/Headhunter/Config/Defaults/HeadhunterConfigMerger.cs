using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;

/// <summary>Adds default rows and fields newer than the file's defaults version, keeping everything else.</summary>
public static class HeadhunterConfigMerger
{
    public static HeadhunterMergeResult Merge(string json, HeadhunterMergeDefaults defaults)
    {
        JObject root = TryParseRoot(json);
        if (root == null)
        {
            return new HeadhunterMergeResult(json, false, 0);
        }
        if (HasUnmergeableLists(root))
        {
            return new HeadhunterMergeResult(json, false, 0);
        }
        int fileVersion = ReadStamp(root);
        if (fileVersion >= defaults.Version)
        {
            return new HeadhunterMergeResult(json, false, 0);
        }

        int added =
            AddStats(root, defaults.Stats, fileVersion)
            + AddFields(root, defaults.Fields, fileVersion)
            + AddAffixes(root, defaults.Affixes, fileVersion)
            + AddAffixRows(root, defaults.AffixRows, fileVersion);
        root[HeadhunterConfigKeys.DefaultsVersion] = defaults.Version;
        return new HeadhunterMergeResult(root.ToString(Formatting.Indented), true, added);
    }

    private static JObject TryParseRoot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JToken.Parse(json) as JObject;
        }
        catch (JsonReaderException)
        {
            return null;
        }
    }

    private static int ReadStamp(JObject root)
    {
        return HeadhunterConfigParser.TryGetInt(
            root[HeadhunterConfigKeys.DefaultsVersion],
            out int version
        )
            ? version
            : HeadhunterConfigDefaults.UnstampedDefaultsVersion;
    }

    private static bool HasUnmergeableLists(JObject root)
    {
        return IsNotList(root[HeadhunterConfigKeys.Stats])
            || IsNotList(root[HeadhunterConfigKeys.AffixMap]);
    }

    private static bool IsNotList(JToken token)
    {
        return token != null && token is not JArray;
    }

    private static int AddStats(
        JObject root,
        IReadOnlyList<HeadhunterVersionedStat> stats,
        int fileVersion
    )
    {
        if (root[HeadhunterConfigKeys.Stats] is not JArray rows)
        {
            return 0;
        }
        int added = 0;
        foreach (HeadhunterVersionedStat row in stats)
        {
            if (row.Since <= fileVersion || ContainsRow(rows, row.Entry))
            {
                continue;
            }
            rows.Add(HeadhunterConfigWriter.BuildStat(row.Entry));
            added++;
        }
        return added;
    }

    private static bool ContainsRow(JArray rows, HeadhunterStatEntry entry)
    {
        foreach (JToken row in rows)
        {
            if (row is not JObject obj)
            {
                continue;
            }
            if (obj[HeadhunterConfigKeys.Stat] is not JValue { Value: string name })
            {
                continue;
            }
            if (
                string.Equals(name, entry.Stat, StringComparison.Ordinal)
                && string.Equals(ReadTag(obj), entry.Tag, StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int AddAffixes(
        JObject root,
        IReadOnlyList<HeadhunterVersionedAffix> affixes,
        int fileVersion
    )
    {
        if (affixes == null)
        {
            return 0;
        }
        var entries = root[HeadhunterConfigKeys.AffixMap] as JArray;
        int added = 0;
        foreach (HeadhunterVersionedAffix affix in affixes)
        {
            if (affix.Since <= fileVersion || ContainsModKey(entries, affix.Entry.ModKey))
            {
                continue;
            }
            if (entries == null)
            {
                entries = new JArray();
                root[HeadhunterConfigKeys.AffixMap] = entries;
            }
            entries.Add(HeadhunterConfigWriter.BuildAffix(affix.Entry));
            added++;
        }
        return added;
    }

    private static int AddAffixRows(
        JObject root,
        IReadOnlyList<HeadhunterVersionedAffixRow> rows,
        int fileVersion
    )
    {
        if (rows == null || root[HeadhunterConfigKeys.AffixMap] is not JArray entries)
        {
            return 0;
        }
        int added = 0;
        foreach (HeadhunterVersionedAffixRow row in rows)
        {
            if (row.Since <= fileVersion)
            {
                continue;
            }
            if (FindAffix(entries, row.ModKey)?[HeadhunterConfigKeys.Rows] is not JArray texts)
            {
                continue;
            }
            if (ContainsText(texts, row.Row))
            {
                continue;
            }
            texts.Add(row.Row);
            added++;
        }
        return added;
    }

    private static bool ContainsText(JArray rows, string text)
    {
        foreach (JToken item in rows)
        {
            if (
                item is JValue { Value: string value }
                && string.Equals(value, text, StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsModKey(JArray entries, int modKey)
    {
        return FindAffix(entries, modKey) != null;
    }

    private static JObject FindAffix(JArray entries, int modKey)
    {
        if (entries == null)
        {
            return null;
        }
        foreach (JToken entry in entries)
        {
            if (
                entry is JObject obj
                && obj[HeadhunterConfigKeys.ModKey] is JToken token
                && HeadhunterConfigParser.TryGetInt(token, out int key)
                && key == modKey
            )
            {
                return obj;
            }
        }
        return null;
    }

    private static string ReadTag(JObject row)
    {
        return row[HeadhunterConfigKeys.Tag] is JValue { Value: string tag } ? tag : null;
    }

    private static int AddFields(
        JObject root,
        IReadOnlyList<HeadhunterVersionedField> fields,
        int fileVersion
    )
    {
        int added = 0;
        foreach (HeadhunterVersionedField field in fields)
        {
            if (field.Since <= fileVersion)
            {
                continue;
            }
            JObject parent = ResolveParent(root, field.Parent);
            if (parent == null || parent.ContainsKey(field.Key))
            {
                continue;
            }
            parent[field.Key] = field.Value.DeepClone();
            added++;
        }
        return added;
    }

    private static JObject ResolveParent(JObject root, string parent)
    {
        return parent.Length == 0 ? root : root[parent] as JObject;
    }
}

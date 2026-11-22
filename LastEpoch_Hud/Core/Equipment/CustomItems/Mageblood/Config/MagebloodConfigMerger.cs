using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>Appends default flasks newer than the file's defaults version, keeping everything else.</summary>
public static class MagebloodConfigMerger
{
    public static MagebloodMergeResult Merge(
        string json,
        IReadOnlyList<MagebloodVersionedFlask> flasks,
        MagebloodVersionedValue maxResistances,
        int defaultsVersion
    )
    {
        if (!ConfigJson.TryParseObject(json, out JObject root))
        {
            return new MagebloodMergeResult(json, false, 0);
        }
        if (IsNotList(root[MagebloodConfigKeys.Flasks]))
        {
            return new MagebloodMergeResult(json, false, 0);
        }
        int stamp = ReadStamp(root);
        if (stamp >= defaultsVersion)
        {
            return new MagebloodMergeResult(json, false, 0);
        }

        int added = AddFlasks(root, flasks, stamp) + AddMaxResistances(root, maxResistances, stamp);
        root[MagebloodConfigKeys.DefaultsVersion] = defaultsVersion;
        return new MagebloodMergeResult(root.ToString(Formatting.Indented), true, added);
    }

    private static int ReadStamp(JObject root)
    {
        return ConfigJson.TryGetInt(root[MagebloodConfigKeys.DefaultsVersion], out int stamp)
            ? stamp
            : MagebloodConfigDefaults.UnstampedDefaultsVersion;
    }

    private static bool IsNotList(JToken token)
    {
        return token != null && token is not JArray;
    }

    private static int AddFlasks(
        JObject root,
        IReadOnlyList<MagebloodVersionedFlask> flasks,
        int stamp
    )
    {
        if (root[MagebloodConfigKeys.Flasks] is not JArray list)
        {
            return 0;
        }
        int added = 0;
        foreach (MagebloodVersionedFlask flask in flasks)
        {
            if (flask.Since <= stamp || ContainsName(list, flask.Entry.Name))
            {
                continue;
            }
            list.Add(MagebloodConfigWriter.BuildFlask(flask.Entry));
            added++;
        }
        return added;
    }

    private static int AddMaxResistances(JObject root, MagebloodVersionedValue value, int stamp)
    {
        if (value.Since <= stamp || root[MagebloodConfigKeys.MaxResistances] != null)
        {
            return 0;
        }
        root[MagebloodConfigKeys.MaxResistances] = value.Value;
        return 1;
    }

    private static bool ContainsName(JArray list, string name)
    {
        foreach (JToken item in list)
        {
            if (
                item is JObject obj
                && obj[MagebloodConfigKeys.Name] is JValue { Value: string existing }
                && string.Equals(existing, name, StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }
}

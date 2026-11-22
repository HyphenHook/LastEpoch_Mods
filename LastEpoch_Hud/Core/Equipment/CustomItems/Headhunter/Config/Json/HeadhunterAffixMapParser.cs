using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>Reads the affixMap section. Bad entries are reported and skipped.</summary>
internal static class HeadhunterAffixMapParser
{
    public static IReadOnlyList<HeadhunterAffixEntry> Read(
        JObject root,
        List<HeadhunterConfigProblem> problems
    )
    {
        JToken token = root[HeadhunterConfigKeys.AffixMap];
        if (token == null)
        {
            return HeadhunterAffixDefaults.AffixMap;
        }
        if (token is not JArray array)
        {
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.NotList,
                HeadhunterConfigKeys.AffixMap,
                "Must be a list."
            );
            return HeadhunterAffixDefaults.AffixMap;
        }

        var map = new List<HeadhunterAffixEntry>();
        var seen = new HashSet<int>();
        for (int index = 0; index < array.Count; index++)
        {
            string path = HeadhunterConfigKeys.AffixMap + "[" + index + "]";
            if (!TryReadEntry(array[index], path, problems, out HeadhunterAffixEntry entry))
            {
                continue;
            }
            if (!seen.Add(entry.ModKey))
            {
                HeadhunterConfigParser.Report(
                    problems,
                    HeadhunterConfigProblemCode.DuplicateModKey,
                    path + "." + HeadhunterConfigKeys.ModKey,
                    "Duplicate mod key."
                );
                continue;
            }
            map.Add(entry);
        }
        return map;
    }

    private static bool TryReadEntry(
        JToken token,
        string path,
        List<HeadhunterConfigProblem> problems,
        out HeadhunterAffixEntry entry
    )
    {
        entry = default;
        if (token is not JObject obj)
        {
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.NotObject,
                path,
                "Must be an object."
            );
            return false;
        }
        if (!TryReadModKey(obj, path, problems, out int modKey))
        {
            return false;
        }
        if (!TryReadRows(obj, path, problems, out List<string> rows))
        {
            return false;
        }
        entry = new HeadhunterAffixEntry(modKey, ReadNote(obj), rows);
        return true;
    }

    private static bool TryReadModKey(
        JObject obj,
        string path,
        List<HeadhunterConfigProblem> problems,
        out int modKey
    )
    {
        modKey = 0;
        JToken token = obj[HeadhunterConfigKeys.ModKey];
        if (token != null && HeadhunterConfigParser.TryGetInt(token, out modKey))
        {
            return true;
        }
        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotWholeNumber,
            path + "." + HeadhunterConfigKeys.ModKey,
            "Must be a whole number."
        );
        return false;
    }

    private static bool TryReadRows(
        JObject obj,
        string path,
        List<HeadhunterConfigProblem> problems,
        out List<string> rows
    )
    {
        rows = null;
        string rowsPath = path + "." + HeadhunterConfigKeys.Rows;
        if (obj[HeadhunterConfigKeys.Rows] is not JArray array)
        {
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.NotList,
                rowsPath,
                "Must be a list."
            );
            return false;
        }

        rows = new List<string>();
        for (int index = 0; index < array.Count; index++)
        {
            if (array[index] is JValue { Value: string text } && text.Length > 0)
            {
                rows.Add(text);
                continue;
            }
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.EmptyOrNotText,
                rowsPath + "[" + index + "]",
                "Must be a non-empty text."
            );
        }
        return true;
    }

    private static string ReadNote(JObject obj)
    {
        return obj[HeadhunterConfigKeys.Note] is JValue { Value: string note } ? note : null;
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>Reads the config JSON by shape only. Bad parts are skipped and reported, never thrown.</summary>
public static class MagebloodConfigParser
{
    public static MagebloodConfigParseResult Parse(string json)
    {
        var problems = new List<MagebloodConfigProblem>();
        JObject root = ParseRoot(json, problems);
        if (root == null)
        {
            return Result(MagebloodConfigDefaults.Config, problems, false);
        }

        var config = new MagebloodConfig
        {
            Version = ReadVersion(root, problems),
            Flasks = ReadFlasks(root, problems),
            MaxResistances = ReadMaxResistances(root, problems),
        };
        return Result(config, problems, true);
    }

    /// <summary>The flask object the parser keeps for this name, or null.</summary>
    internal static JObject FindFlask(JArray flasks, string name)
    {
        var problems = new List<MagebloodConfigProblem>();
        foreach ((int index, MagebloodFlaskEntry flask) in KeptFlasks(flasks, problems))
        {
            if (string.Equals(flask.Name, name, StringComparison.Ordinal))
            {
                return (JObject)flasks[index];
            }
        }
        return null;
    }

    /// <summary>The row object the parser keeps for this row text, or null.</summary>
    internal static JObject FindRow(JArray stats, string rowText)
    {
        var problems = new List<MagebloodConfigProblem>();
        foreach ((int index, MagebloodStatEntry row) in KeptRows(stats, "", problems))
        {
            if (string.Equals(row.RowText, rowText, StringComparison.Ordinal))
            {
                return (JObject)stats[index];
            }
        }
        return null;
    }

    private static MagebloodConfigParseResult Result(
        MagebloodConfig config,
        List<MagebloodConfigProblem> problems,
        bool readable
    )
    {
        return new MagebloodConfigParseResult
        {
            Config = config,
            Problems = problems,
            IsReadable = readable,
        };
    }

    private static void Report(
        List<MagebloodConfigProblem> problems,
        MagebloodConfigProblemCode code,
        string path,
        string message
    )
    {
        problems.Add(new MagebloodConfigProblem(code, path, message));
    }

    private static JObject ParseRoot(string json, List<MagebloodConfigProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Report(problems, MagebloodConfigProblemCode.EmptyFile, "", "File is empty.");
            return null;
        }
        try
        {
            var token = JToken.Parse(json);
            if (token is JObject root)
            {
                return root;
            }
            Report(
                problems,
                MagebloodConfigProblemCode.RootNotObject,
                "",
                "Root must be a JSON object."
            );
            return null;
        }
        catch (JsonReaderException ex)
        {
            Report(
                problems,
                MagebloodConfigProblemCode.InvalidJson,
                "",
                "Invalid JSON: " + ex.Message
            );
            return null;
        }
    }

    private static int ReadVersion(JObject root, List<MagebloodConfigProblem> problems)
    {
        JToken token = root[MagebloodConfigKeys.Version];
        if (token == null)
        {
            return MagebloodConfigDefaults.CurrentVersion;
        }
        if (!ConfigJson.TryGetInt(token, out int version))
        {
            Report(
                problems,
                MagebloodConfigProblemCode.NotWholeNumber,
                MagebloodConfigKeys.Version,
                "Must be a whole number."
            );
            return MagebloodConfigDefaults.CurrentVersion;
        }
        if (version != MagebloodConfigDefaults.CurrentVersion)
        {
            Report(
                problems,
                MagebloodConfigProblemCode.UnsupportedVersion,
                MagebloodConfigKeys.Version,
                "Unsupported version " + version + "."
            );
        }
        return version;
    }

    private static float ReadMaxResistances(JObject root, List<MagebloodConfigProblem> problems)
    {
        JToken token = root[MagebloodConfigKeys.MaxResistances];
        if (token == null)
        {
            return MagebloodConfigDefaults.MaxResistances.Value;
        }
        if (!ConfigJson.TryReadNumber(token, out float value))
        {
            Report(
                problems,
                MagebloodConfigProblemCode.NotFiniteNumber,
                MagebloodConfigKeys.MaxResistances,
                "Must be a finite number."
            );
            return MagebloodConfigDefaults.MaxResistances.Value;
        }
        if (value < 0f)
        {
            Report(
                problems,
                MagebloodConfigProblemCode.Negative,
                MagebloodConfigKeys.MaxResistances,
                "Must not be negative."
            );
            return MagebloodConfigDefaults.MaxResistances.Value;
        }
        return value;
    }

    private static IReadOnlyList<MagebloodFlaskEntry> ReadFlasks(
        JObject root,
        List<MagebloodConfigProblem> problems
    )
    {
        JToken token = root[MagebloodConfigKeys.Flasks];
        if (token == null)
        {
            return MagebloodConfigDefaults.Flasks;
        }
        if (token is not JArray list)
        {
            Report(
                problems,
                MagebloodConfigProblemCode.NotList,
                MagebloodConfigKeys.Flasks,
                "Must be a list."
            );
            return MagebloodConfigDefaults.Flasks;
        }

        var flasks = new List<MagebloodFlaskEntry>();
        foreach ((int _, MagebloodFlaskEntry flask) in KeptFlasks(list, problems))
        {
            flasks.Add(flask);
        }
        return flasks;
    }

    private static List<(int Index, MagebloodFlaskEntry Flask)> KeptFlasks(
        JArray list,
        List<MagebloodConfigProblem> problems
    )
    {
        var kept = new List<(int Index, MagebloodFlaskEntry Flask)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < list.Count; i++)
        {
            string path = MagebloodConfigKeys.Flasks + "[" + i + "]";
            if (!TryReadFlask(list[i], path, names, problems, out MagebloodFlaskEntry flask))
            {
                continue;
            }
            names.Add(flask.Name);
            kept.Add((i, flask));
        }
        return kept;
    }

    private static bool TryReadFlask(
        JToken token,
        string path,
        HashSet<string> names,
        List<MagebloodConfigProblem> problems,
        out MagebloodFlaskEntry flask
    )
    {
        flask = null;
        if (token is not JObject obj)
        {
            Report(problems, MagebloodConfigProblemCode.NotObject, path, "Must be an object.");
            return false;
        }
        if (!TryReadName(obj, path, names, problems, out string name))
        {
            return false;
        }
        if (!TryReadRows(obj, path, problems, out List<MagebloodStatEntry> rows))
        {
            return false;
        }

        flask = new MagebloodFlaskEntry
        {
            Name = name,
            Icon = ReadIcon(obj, path, problems),
            Stats = rows,
        };
        return true;
    }

    private static bool TryReadName(
        JObject flask,
        string path,
        HashSet<string> names,
        List<MagebloodConfigProblem> problems,
        out string name
    )
    {
        name = null;
        string namePath = path + "." + MagebloodConfigKeys.Name;
        JToken token = flask[MagebloodConfigKeys.Name];
        if (token == null)
        {
            Report(problems, MagebloodConfigProblemCode.MissingName, path, "Missing name.");
            return false;
        }
        if (!ConfigJson.TryReadText(token, out name))
        {
            Report(
                problems,
                MagebloodConfigProblemCode.EmptyOrNotText,
                namePath,
                "Must be non-empty text."
            );
            return false;
        }
        if (names.Contains(name))
        {
            Report(
                problems,
                MagebloodConfigProblemCode.DuplicateFlask,
                namePath,
                "Same name as an earlier flask: " + name
            );
            return false;
        }
        return true;
    }

    private static string ReadIcon(
        JObject flask,
        string path,
        List<MagebloodConfigProblem> problems
    )
    {
        JToken token = flask[MagebloodConfigKeys.Icon];
        if (token == null)
        {
            return null;
        }
        if (ConfigJson.TryReadText(token, out string icon))
        {
            return icon;
        }
        Report(
            problems,
            MagebloodConfigProblemCode.EmptyOrNotText,
            path + "." + MagebloodConfigKeys.Icon,
            "Must be non-empty text."
        );
        return null;
    }

    private static bool TryReadRows(
        JObject flask,
        string path,
        List<MagebloodConfigProblem> problems,
        out List<MagebloodStatEntry> rows
    )
    {
        rows = null;
        JToken token = flask[MagebloodConfigKeys.Stats];
        if (token == null)
        {
            Report(problems, MagebloodConfigProblemCode.NoStats, path, "Flask has no stats.");
            return false;
        }
        string statsPath = path + "." + MagebloodConfigKeys.Stats;
        if (token is not JArray list)
        {
            Report(problems, MagebloodConfigProblemCode.NotList, statsPath, "Must be a list.");
            return false;
        }

        rows = ReadRows(list, statsPath, problems);
        if (rows.Count > 0)
        {
            return true;
        }
        Report(problems, MagebloodConfigProblemCode.NoStats, statsPath, "No usable stat row.");
        return false;
    }

    private static List<MagebloodStatEntry> ReadRows(
        JArray list,
        string statsPath,
        List<MagebloodConfigProblem> problems
    )
    {
        var rows = new List<MagebloodStatEntry>();
        foreach ((int _, MagebloodStatEntry row) in KeptRows(list, statsPath, problems))
        {
            rows.Add(row);
        }
        return rows;
    }

    private static List<(int Index, MagebloodStatEntry Row)> KeptRows(
        JArray list,
        string statsPath,
        List<MagebloodConfigProblem> problems
    )
    {
        var kept = new List<(int Index, MagebloodStatEntry Row)>();
        var seen = new HashSet<(string Stat, string Tag)>();
        for (int i = 0; i < list.Count; i++)
        {
            string path = statsPath + "[" + i + "]";
            if (!TryReadRow(list[i], path, problems, out MagebloodStatEntry row))
            {
                continue;
            }
            if (!seen.Add((row.Stat, row.Tag)))
            {
                Report(
                    problems,
                    MagebloodConfigProblemCode.DuplicateStat,
                    path + "." + MagebloodConfigKeys.Stat,
                    "Same stat and tag as an earlier row: " + row.RowText
                );
                continue;
            }
            kept.Add((i, row));
        }
        return kept;
    }

    private static bool TryReadRow(
        JToken token,
        string path,
        List<MagebloodConfigProblem> problems,
        out MagebloodStatEntry row
    )
    {
        row = default;
        if (token is not JObject obj)
        {
            Report(problems, MagebloodConfigProblemCode.NotObject, path, "Must be an object.");
            return false;
        }
        if (!TryReadStat(obj, path, problems, out string stat, out string tag))
        {
            return false;
        }
        if (!TryReadValue(obj, MagebloodConfigKeys.Added, path, problems, out float added))
        {
            return false;
        }
        if (!TryReadValue(obj, MagebloodConfigKeys.Increased, path, problems, out float increased))
        {
            return false;
        }
        if (!TryReadValue(obj, MagebloodConfigKeys.More, path, problems, out float more))
        {
            return false;
        }
        if (added == 0f && increased == 0f && more == 0f)
        {
            Report(
                problems,
                MagebloodConfigProblemCode.NoValue,
                path,
                "Needs added, increased or more."
            );
            return false;
        }

        row = new MagebloodStatEntry(stat, added, increased, more, tag);
        return true;
    }

    private static bool TryReadStat(
        JObject row,
        string path,
        List<MagebloodConfigProblem> problems,
        out string stat,
        out string tag
    )
    {
        stat = null;
        tag = null;
        JToken statToken = row[MagebloodConfigKeys.Stat];
        if (statToken == null)
        {
            Report(problems, MagebloodConfigProblemCode.MissingStat, path, "Missing stat.");
            return false;
        }
        if (!ConfigJson.TryReadText(statToken, out stat))
        {
            ReportNotText(problems, path + "." + MagebloodConfigKeys.Stat);
            return false;
        }

        JToken tagToken = row[MagebloodConfigKeys.Tag];
        if (tagToken == null || ConfigJson.TryReadText(tagToken, out tag))
        {
            return true;
        }
        ReportNotText(problems, path + "." + MagebloodConfigKeys.Tag);
        return false;
    }

    private static void ReportNotText(List<MagebloodConfigProblem> problems, string path)
    {
        Report(
            problems,
            MagebloodConfigProblemCode.EmptyOrNotText,
            path,
            "Must be non-empty text."
        );
    }

    private static bool TryReadValue(
        JObject row,
        string key,
        string path,
        List<MagebloodConfigProblem> problems,
        out float value
    )
    {
        value = 0f;
        JToken token = row[key];
        if (token == null || ConfigJson.TryReadNumber(token, out value))
        {
            return true;
        }
        Report(
            problems,
            MagebloodConfigProblemCode.NotFiniteNumber,
            path + "." + key,
            "Must be a finite number."
        );
        return false;
    }
}

using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>Reads the bar section. Bad values fall back per key and are reported.</summary>
internal static class HeadhunterBarSettingsParser
{
    public static HeadhunterBarSettings Read(JObject root, List<HeadhunterConfigProblem> problems)
    {
        HeadhunterBarSettings defaults = HeadhunterConfigDefaults.Bar;
        JToken token = root[HeadhunterConfigKeys.Bar];
        if (token == null)
        {
            return defaults;
        }
        if (token is not JObject section)
        {
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.NotObject,
                HeadhunterConfigKeys.Bar,
                "Must be an object."
            );
            return defaults;
        }

        return new HeadhunterBarSettings(
            ReadOffset(section, HeadhunterConfigKeys.OffsetX, defaults.OffsetX, problems),
            ReadOffset(section, HeadhunterConfigKeys.OffsetY, defaults.OffsetY, problems),
            ReadIconSize(section, defaults.IconSize, problems),
            ReadPerRow(section, defaults.PerRow, problems)
        );
    }

    private static float ReadOffset(
        JObject section,
        string key,
        float fallback,
        List<HeadhunterConfigProblem> problems
    )
    {
        JToken token = section[key];
        if (token == null)
        {
            return fallback;
        }
        if (HeadhunterConfigParser.TryReadNumber(token, out float value))
        {
            return value;
        }

        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotFiniteNumber,
            HeadhunterConfigKeys.Bar + "." + key,
            "Must be a finite number."
        );
        return fallback;
    }

    private static float ReadIconSize(
        JObject section,
        float fallback,
        List<HeadhunterConfigProblem> problems
    )
    {
        JToken token = section[HeadhunterConfigKeys.IconSize];
        if (token == null)
        {
            return fallback;
        }
        if (HeadhunterConfigParser.TryReadNumber(token, out float value) && value > 0f)
        {
            return value;
        }

        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotPositiveNumber,
            HeadhunterConfigKeys.Bar + "." + HeadhunterConfigKeys.IconSize,
            "Must be a number above 0."
        );
        return fallback;
    }

    private static int ReadPerRow(
        JObject section,
        int fallback,
        List<HeadhunterConfigProblem> problems
    )
    {
        JToken token = section[HeadhunterConfigKeys.PerRow];
        if (token == null)
        {
            return fallback;
        }
        if (HeadhunterConfigParser.TryGetInt(token, out int value) && value >= 1)
        {
            return value;
        }

        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotPositiveWholeNumber,
            HeadhunterConfigKeys.Bar + "." + HeadhunterConfigKeys.PerRow,
            "Must be a whole number of at least 1."
        );
        return fallback;
    }
}

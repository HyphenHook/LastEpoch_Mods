using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>Reads the modelSize section. Bad values fall back and are reported.</summary>
internal static class HeadhunterModelSizeParser
{
    private const float MaxPercent = 100f;

    public static HeadhunterSizeCurve Read(JObject root, List<HeadhunterConfigProblem> problems)
    {
        HeadhunterSizeCurve defaults = HeadhunterConfigDefaults.ModelSize;
        JToken token = root[HeadhunterConfigKeys.ModelSize];
        if (token == null)
        {
            return defaults;
        }
        if (token is not JObject section)
        {
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.NotObject,
                HeadhunterConfigKeys.ModelSize,
                "Must be an object."
            );
            return defaults;
        }

        return new HeadhunterSizeCurve(
            ReadPercent(section, HeadhunterConfigKeys.PerBuff, defaults.PerBuffPercent, problems),
            ReadPercent(section, HeadhunterConfigKeys.Cap, defaults.CapPercent, problems)
        );
    }

    private static float ReadPercent(
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
        if (HeadhunterConfigParser.TryReadNumber(token, out float value) && IsPercent(value))
        {
            return value;
        }

        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotPercent,
            HeadhunterConfigKeys.ModelSize + "." + key,
            "Must be a number from 0 to 100."
        );
        return fallback;
    }

    private static bool IsPercent(float value)
    {
        return value >= 0f && value <= MaxPercent;
    }
}

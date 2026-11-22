using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>Reads the aura section. Bad values fall back per key and are reported.</summary>
internal static class HeadhunterAuraParser
{
    private const float MaxStrength = 3f;

    public static HeadhunterAuraCurve Read(JObject root, List<HeadhunterConfigProblem> problems)
    {
        HeadhunterAuraCurve defaults = HeadhunterConfigDefaults.Aura;
        JToken token = root[HeadhunterConfigKeys.Aura];
        if (token == null)
        {
            return defaults;
        }
        if (token is not JObject section)
        {
            HeadhunterConfigParser.Report(
                problems,
                HeadhunterConfigProblemCode.NotObject,
                HeadhunterConfigKeys.Aura,
                "Must be an object."
            );
            return defaults;
        }

        return new HeadhunterAuraCurve(
            ReadEnabled(section, defaults.Enabled, problems),
            ReadStrength(section, HeadhunterConfigKeys.PerBuff, defaults.PerBuff, problems),
            ReadStrength(section, HeadhunterConfigKeys.Cap, defaults.Cap, problems)
        );
    }

    private static bool ReadEnabled(
        JObject section,
        bool fallback,
        List<HeadhunterConfigProblem> problems
    )
    {
        JToken token = section[HeadhunterConfigKeys.Enabled];
        if (token == null)
        {
            return fallback;
        }
        if (HeadhunterConfigParser.TryReadBool(token, out bool value))
        {
            return value;
        }

        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotBool,
            HeadhunterConfigKeys.Aura + "." + HeadhunterConfigKeys.Enabled,
            "Must be true or false."
        );
        return fallback;
    }

    private static float ReadStrength(
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
        if (HeadhunterConfigParser.TryReadNumber(token, out float value) && IsStrength(value))
        {
            return value;
        }

        HeadhunterConfigParser.Report(
            problems,
            HeadhunterConfigProblemCode.NotAuraStrength,
            HeadhunterConfigKeys.Aura + "." + key,
            "Must be a number from 0 to 3."
        );
        return fallback;
    }

    private static bool IsStrength(float value)
    {
        return value >= 0f && value <= MaxStrength;
    }
}

using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mjolner;

/// <summary>Mjolner description text from its settings.</summary>
public static class MjolnerDescription
{
    public static string LightningProc(
        IReadOnlyDictionary<string, string> texts,
        int strRequirement,
        int intRequirement,
        float minTriggerChance,
        float maxTriggerChance
    )
    {
        string template = LocaleText.Get(texts, CustomItemLocaleKeys.MjolnerDescriptionProc);
        return TextTemplate.Fill(
            template,
            strRequirement,
            intRequirement,
            MjolnerTriggerChance.Percent(minTriggerChance),
            MjolnerTriggerChance.Percent(maxTriggerChance)
        );
    }

    public static string SocketedSkills(
        IReadOnlyDictionary<string, string> texts,
        int strRequirement,
        int intRequirement,
        double socketedCooldownMs,
        string skill0,
        string skill1,
        string skill2
    )
    {
        string template = LocaleText.Get(texts, CustomItemLocaleKeys.MjolnerDescriptionSocketed);
        return TextTemplate.Fill(
            template,
            strRequirement,
            intRequirement,
            skill0,
            skill1,
            skill2,
            socketedCooldownMs / 1000
        );
    }
}

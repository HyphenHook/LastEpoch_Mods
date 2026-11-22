using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mjolner;
using MelonLoader;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mjolner;

internal static class MjolnerLocales
{
    internal static string CurrentDescription(IReadOnlyDictionary<string, string> texts)
    {
        if (Save_Manager.instance.IsNullOrDestroyed() || !Save_Manager.instance.initialized)
        {
            return null;
        }

        Save_Manager.Data.Mjolner mjolner = Save_Manager.instance.data.Items.Mjolner;
        if (mjolner.ProcAnyLightningSpell)
        {
            return MjolnerDescription.LightningProc(
                texts,
                mjolner.StrRequirement,
                mjolner.IntRequirement,
                mjolner.MinTriggerChance,
                mjolner.MaxTriggerChance
            );
        }

        return MjolnerDescription.SocketedSkills(
            texts,
            mjolner.StrRequirement,
            mjolner.IntRequirement,
            mjolner.SocketedCooldown,
            mjolner.SockectedSkill_0,
            mjolner.SockectedSkill_1,
            mjolner.SockectedSkill_2
        );
    }
}

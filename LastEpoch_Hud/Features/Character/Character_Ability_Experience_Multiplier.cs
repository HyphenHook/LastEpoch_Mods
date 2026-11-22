using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Mods.Character;

public class Character_Ability_Experience_Multiplier
{
    [HarmonyPatch(typeof(Il2Cpp.ExperienceTracker), "GainExp")]
    public class ExperienceTracker_GainExp
    {
        [HarmonyPrefix]
        static void Prefix(ref long __1)
        {
            if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
            {
                if (Save_Manager.instance.data.Character.Cheats.Enable_AbilityMultiplier)
                {
                    __1 *= (long)Save_Manager.instance.data.Character.Cheats.AbilityMultiplier;
                }
            }
        }
    }
}

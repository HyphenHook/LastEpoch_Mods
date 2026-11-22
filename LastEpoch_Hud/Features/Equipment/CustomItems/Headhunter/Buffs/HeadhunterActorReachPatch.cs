using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(Actor), nameof(Actor.GetWeaponRange))]
public class HeadhunterActorReachPatch
{
    [HarmonyPostfix]
    private static void Postfix(Actor __instance, ref float __result)
    {
        HeadhunterReach.ForActor(__instance, ref __result);
    }
}

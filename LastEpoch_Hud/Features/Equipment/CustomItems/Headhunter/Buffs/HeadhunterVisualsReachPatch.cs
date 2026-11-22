using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(ActorVisuals), nameof(ActorVisuals.GetWeaponRange))]
public class HeadhunterVisualsReachPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActorVisuals __instance, ref float __result)
    {
        HeadhunterReach.ForVisuals(__instance, ref __result);
    }
}

using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Raises the local player's resistance cap while a Mageblood is worn.</summary>
[HarmonyPatch(typeof(ProtectionClass), nameof(ProtectionClass.ApplyResistance))]
public class MagebloodResistancePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        ProtectionClass __instance,
        float __0,
        DamageType __1,
        ref float __result
    )
    {
        MagebloodResistance.Apply(__instance, __0, __1, ref __result);
    }
}

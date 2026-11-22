using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.ModUI;
using ModSaveManager = LastEpoch_Hud.Scripts.ModUI.Settings.SaveManager;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class Items_InfiniteForgingPotential
{
    static bool Enabled() =>
        ModSettings.InfiniteForgingPotential.Enabled.Value
        && !ModSaveManager.instance.IsNullOrDestroyed()
        && ModSaveManager.instance.initialized
        && Scenes.IsGameScene();

    // Preserve potential at the actual cost methods, including non-affix crafts.
    // Run outside the existing >63 potential compatibility patches.
    static void Before(ItemData item, out int state)
    {
        state = Enabled() && !item.IsNullOrDestroyed() ? item.forgingPotential : -1;
    }

    static void After(ItemData item, int state)
    {
        if (state < 0 || item.IsNullOrDestroyed())
            return;
        Items_Drop_ForginPotencial.Keep(item, (byte)state);
    }

    [HarmonyPatch(typeof(ItemData), "applyForgingPotentialCost")]
    internal static class Cost
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        static void Prefix(ItemData __instance, out int __state)
        {
            Before(__instance, out __state);
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(ItemData __instance, int __state)
        {
            After(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(ItemData), "applyForgingPotentialCostFromShard")]
    internal static class ShardCost
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        static void Prefix(ItemData __instance, out int __state)
        {
            Before(__instance, out __state);
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(ItemData __instance, int __state)
        {
            After(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(ItemData), "applyForgingPotentialCostForNonAffix")]
    internal static class NonAffixCost
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        static void Prefix(ItemData __instance, out int __state)
        {
            Before(__instance, out __state);
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(ItemData __instance, int __state)
        {
            After(__instance, __state);
        }
    }
}

using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

[HarmonyPatch(typeof(TooltipItemManager), "GetUniqueDescription")]
public class MagebloodDescriptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        ItemDataUnpacked __0,
        int __3,
        ref Il2CppSystem.Collections.Generic.List<string> __result
    )
    {
        if (__3 != CustomUniqueSpecs.Mageblood.UniqueId || __result == null)
        {
            return;
        }

        if (!MagebloodSlots.TryRead(__0, out int slots))
        {
            return;
        }

        // A new list: the game may cache the one it returned.
        var withSlots = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (string line in __result)
        {
            withSlots.Add(MagebloodDescription.WithSlots(line, slots));
        }

        __result = withSlots;
    }
}

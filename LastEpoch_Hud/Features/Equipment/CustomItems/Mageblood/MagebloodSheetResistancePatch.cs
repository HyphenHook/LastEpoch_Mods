using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Rebuilds the sheet's resistance text while a Mageblood is worn.</summary>
[HarmonyPatch(typeof(CharacterSheet), nameof(CharacterSheet.resistanceText))]
public class MagebloodSheetResistancePatch
{
    [HarmonyPostfix]
    private static void Postfix(float __0, ref string __result)
    {
        MagebloodSheetResistance.Apply(__0, ref __result);
    }
}

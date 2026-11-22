using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

[HarmonyPatch(typeof(Il2Cpp.Localization), "TryGetText")]
public class CustomItemTryGetTextPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result, string __0, ref string __1)
    {
        string text = CustomItemLocalization.Resolve(__0);
        if (text == null)
        {
            return true;
        }

        __1 = text;
        __result = true;
        return false;
    }
}

using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

[HarmonyPatch(typeof(Il2Cpp.Localization), "GetText")]
public class CustomItemGetTextPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref string __result, string __0)
    {
        string text = CustomItemLocalization.Resolve(__0);
        if (text == null)
        {
            return true;
        }

        __result = text;
        return false;
    }
}

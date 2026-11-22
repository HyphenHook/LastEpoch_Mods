using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Chat;

public class Chat_Remove
{
    [HarmonyPatch(typeof(UIBase), "ChatKeyDown")]
    public class UIBase_ChatKeyDown
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return false;
        }
    }
}

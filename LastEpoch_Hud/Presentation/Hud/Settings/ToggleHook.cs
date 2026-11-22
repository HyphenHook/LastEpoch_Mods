using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

// Toggle events via Harmony. Managed UnityEvent listeners are not reliable for
// runtime-created IL2CPP controls, especially while the HUD mirrors save data.
public static class ToggleHook
{
    private static readonly Dictionary<int, Action<bool>> handlers = new();

    public static void Register(Toggle toggle, Action<bool> handler)
    {
        if (toggle == null || handler == null)
            return;
        handlers[toggle.GetInstanceID()] = handler;
    }

    public static void Unregister(Toggle toggle)
    {
        if (toggle == null)
            return;
        handlers.Remove(toggle.GetInstanceID());
    }

    public static void Clear() => handlers.Clear();

    [HarmonyPatch(typeof(Toggle), "OnPointerClick")]
    public class TogglePointerClickPatch
    {
        [HarmonyPostfix]
        static void Postfix(Toggle __instance)
        {
            if (SaveManager.instance == null)
                return;
            if (handlers.TryGetValue(__instance.GetInstanceID(), out var handler))
                handler(__instance.isOn);
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

// Reliable click dispatch for runtime-created IL2CPP buttons. Components using
// this hook must not also bind the same action through Button.onClick.
public static class ButtonHook
{
    private static readonly Dictionary<int, Action> handlers = new();

    public static void Register(Button button, Action handler)
    {
        if (button == null || handler == null)
            return;
        handlers[button.GetInstanceID()] = handler;
    }

    public static void Unregister(Button button)
    {
        if (button == null)
            return;
        handlers.Remove(button.GetInstanceID());
    }

    public static void Clear() => handlers.Clear();

    [HarmonyPatch(typeof(Button), "Press")]
    public class ButtonPressPatch
    {
        [HarmonyPostfix]
        static void Postfix(Button __instance)
        {
            if (handlers.TryGetValue(__instance.GetInstanceID(), out var handler))
                handler();
        }
    }
}

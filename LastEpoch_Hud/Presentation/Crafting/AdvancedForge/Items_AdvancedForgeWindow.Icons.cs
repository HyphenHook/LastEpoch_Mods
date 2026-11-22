using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_AdvancedForgeWindow
{
    private static ItemIconSource icon;
    private static long iconFor;

    private static ItemIconSource IconFor(ItemData data)
    {
        long key = (long)data.Pointer;
        if (icon != null && iconFor == key)
        {
            return icon;
        }
        icon?.Dispose();
        icon = BuildIcon(data);
        iconFor = key;
        return icon;
    }

    private static ItemIconSource BuildIcon(ItemData data)
        => ItemIconSource.Create(data, root.transform, "AdvancedForgeBackgroundHolder", "Advanced Forge");

    private static void TickIcon()
    {
        if (icon != null && (icon.Content == null || icon.Back == null))
        {
            bool had = icon.Content != null;
            icon.Poll();
            if (!had && icon.Content != null)
            {
                dirty = true;
            }
        }
    }
}

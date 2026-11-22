using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
    private static readonly ItemIconSource[] icons = new ItemIconSource[2]; // 0: item A, 1: item B
    private static readonly long[] iconFor = new long[2];

    private static ItemIconSource IconFor(ItemData item, int id)
    {
        long key = (long)item.Pointer;
        if (icons[id] != null && iconFor[id] == key)
        {
            return icons[id];
        }
        icons[id]?.Dispose();
        icons[id] = BuildIcon(item);
        iconFor[id] = key;
        return icons[id];
    }

    private static ItemIconSource BuildIcon(ItemData item)
        => ItemIconSource.Create(item, root.transform, "ReflectorBackgroundHolder", "Memory Reflector");

    private static void TickIcons()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            ItemIconSource icon = icons[i];
            if (icon != null && (icon.Content == null || icon.Back == null) && icon.Born > 0)
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
}

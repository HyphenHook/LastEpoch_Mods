using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Character;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_RecombinatorWindow
{
    private static readonly ItemIconSource[] icons = new ItemIconSource[3]; // 0 and 1: the input slots, 2: the result
    private static readonly long[] iconFor = new long[3];

    /// <summary>
    /// Reuses a source for this slot until its item changes. The source retains the native background
    /// widget and sprite load until the window releases it.
    /// </summary>
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
        => ItemIconSource.Create(item, root.transform, "RecombinatorBackgroundHolder", "Recombinator", requireSpriteReference: true);

    /// <summary>Re-draws the window when an icon that was still loading has its sprite.</summary>
    private static void TickIcons()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            ItemIconSource icon = icons[i];
            if (icon != null && icon.Content == null && icon.Poll())
            {
                dirty = true; // the sprite has arrived: draw it
            }
        }
        // if the result's own icon never loads, it takes the look of the item it was made from
        ItemIconSource res = icons[2];
        if (res != null && resultRevealed && res.Content == null && Time.unscaledTime - res.Born > 3f)
        {
            ItemIconSource from = icons[0]?.Content != null ? icons[0] : icons[1]?.Content != null ? icons[1] : null;
            if (from != null)
            {
                res.Content = from.Content;
                dirty = true;
            }
        }
    }
}

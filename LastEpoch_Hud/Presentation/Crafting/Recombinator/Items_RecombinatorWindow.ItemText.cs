using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    // ---------------------------------------------------------------- text helpers

    private static string ItemTitle(ItemData item)
    {
        string name = item.TryCast<ItemDataUnpacked>()?.FullName;
        return string.IsNullOrEmpty(name) ? "Item" : name;
    }

    private static string ItemSubtitle(ItemData item)
    {
        int ordinary = item.affixes.ToArray().Count(x => x != null && !x.IsSealed);
        return ordinary + " affix" + (ordinary == 1 ? "" : "es") + (item.affixes.ToArray().Any(x => x != null && x.IsSealed) ? ", sealed" : "");
    }

    private static readonly Regex HexColour = new("#?([0-9A-Fa-f]{6})", RegexOptions.Compiled);

    /// <summary>The item's rarity colour, taken from the game's own name colouring.</summary>
    internal static Color ItemColor(ItemData item)
    {
        try
        {
            ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
            if (data != null)
            {
                Match m = HexColour.Match(TooltipItemManager.SetNameColor(data) ?? "");
                if (m.Success)
                {
                    int v = Convert.ToInt32(m.Groups[1].Value, 16);
                    return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
                }
            }
        }
        catch (Exception)
        {
        }
        return ItemColorFallback(item);
    }

    private static Color ItemColorFallback(ItemData item)
    {
        bool exalted = item.affixes.ToArray().Any(x => x != null && x.affixTier >= 5);
        if (exalted)
        {
            return new Color(0.82f, 0.55f, 1f);
        }
        return item.rarity <= 1 ? new Color(0.5f, 0.65f, 1f) : Gold;
    }

    private static string AffixText(RecombAffix a)
    {
        AffixList.Affix def = AffixList.get()?.GetAffix(a.AffixId);
        string name = def == null ? "Affix " + a.AffixId : NativeItemNames.AffixName(def);
        return name + "   T" + a.DisplayedTier;
    }
}

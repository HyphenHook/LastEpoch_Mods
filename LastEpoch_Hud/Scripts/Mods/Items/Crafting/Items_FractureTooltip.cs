using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Tints the fractured affix's line light blue in an item's tooltip. The text of the line is changed where the game builds
/// it (see <see cref="Items_FractureGuard" />); this looks at the finished tooltip too and tints the line there if it was
/// missed, so the tint does not depend on how the game happens to build the tooltip.
/// </summary>
internal static class Items_FractureTooltip
{
    private const string Blue = "8fd8ff";
    private static readonly Regex Tags = new("</?color[^>]*>", RegexOptions.Compiled);
    private static readonly Regex AllTags = new("<[^>]+>", RegexOptions.Compiled);

    private sealed class Watch
    {
        public UITooltipItem Ui;
        public ItemData Item;
        public float Next;
    }

    private static readonly List<Watch> watched = new();

    [HarmonyPatch(typeof(UITooltipItem), "SetItemImage",
        new[] { typeof(ItemDataUnpacked), typeof(UITooltipItem.ItemTooltipInfo), typeof(bool) })]
    private static class BindPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance, ItemDataUnpacked __0, bool __2)
        {
            try
            {
                if (__instance.IsNullOrDestroyed() || __2)
                {
                    return;
                }
                watched.RemoveAll(w => w.Ui == __instance || w.Ui.IsNullOrDestroyed());
                ItemData item = __0?.TryCast<ItemData>();
                if (Items_Fracture.FracturedAffix(item) >= 0)
                {
                    watched.Add(new Watch { Ui = __instance, Item = item });
                    Main.logger_instance?.Msg("Fracture tooltip: watching a tooltip of a fractured item.");
                }
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), "OnUpdateTick")]
    private static class TickPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance)
        {
            try
            {
                foreach (Watch w in watched)
                {
                    if (w.Ui == __instance && Time.unscaledTime >= w.Next)
                    {
                        w.Next = Time.unscaledTime + 0.2f;
                        Tint(w);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private static void Tint(Watch w)
    {
        if (w.Ui.IsNullOrDestroyed() || !w.Ui.gameObject.activeInHierarchy)
        {
            return;
        }
        ItemAffix affix = Items_Fracture.FracturedOf(w.Item);
        if (affix == null)
        {
            return;
        }
        var wanted = new List<string>();
        if (!string.IsNullOrEmpty(affix.affixName))
        {
            wanted.Add(affix.affixName.ToLowerInvariant());
        }
        foreach (TextMeshProUGUI t in w.Ui.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
            if (t == null || string.IsNullOrEmpty(t.text) || t.text.Contains(Blue))
            {
                continue;
            }
            string plain = AllTags.Replace(t.text, "").Trim();
            // one affix is a line or two; a block of several affixes is left alone
            if (plain.Length == 0 || plain.Length > 160 || plain.Split('\n').Length > 3)
            {
                continue;
            }
            string lower = plain.ToLowerInvariant();
            if (wanted.Any(lower.Contains))
            {
                t.text = "<color=#" + Blue + ">◆ " + Tags.Replace(t.text, "") + "</color>";
                Main.logger_instance?.Msg("Fracture tooltip: tinted \"" + plain + "\".");
            }
        }
    }
}

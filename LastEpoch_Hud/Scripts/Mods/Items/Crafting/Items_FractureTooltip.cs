using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Marks the fractured affix in an item's tooltip: its line is tinted light blue and a small blue diamond sits just before
/// it. The tooltip is the game's own; each affix in it is a UITooltipItemAffix whose text is changed after the game has
/// written it. The diamond is an image, because the game's font has no diamond character.
/// </summary>
internal static class Items_FractureTooltip
{
    private const string Blue = "8fd8ff";
    private const string DiamondName = "FractureDiamond";
    private static readonly Regex Tags = new("</?color[^>]*>", RegexOptions.Compiled);
    private static readonly Regex AllTags = new("<[^>]+>", RegexOptions.Compiled);

    private sealed class Watch
    {
        public UITooltipItem Ui;
        public ItemData Item;
        public float Next;
    }

    private static readonly List<Watch> watched = new();

    /// <summary>The text of every affix in the tooltip.</summary>
    private static List<TextMeshProUGUI> AffixTexts(UITooltipItem ui)
    {
        var lines = new List<TextMeshProUGUI>();
        void Collect(Il2CppSystem.Collections.Generic.List<UITooltipItemAffix> list)
        {
            if (list == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].IsNullOrDestroyed() && !list[i]._text.IsNullOrDestroyed())
                {
                    lines.Add(list[i]._text);
                }
            }
        }
        Collect(ui.prefixesText);
        Collect(ui.suffixesText);
        Collect(ui.uniqueAffixesText);
        return lines;
    }

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
                foreach (TextMeshProUGUI t in AffixTexts(__instance))
                {
                    RemoveDiamond(t); // the rows are reused from item to item
                }
                ItemData item = __0?.TryCast<ItemData>();
                if (Items_Fracture.FracturedAffix(item) >= 0)
                {
                    watched.Add(new Watch { Ui = __instance, Item = item });
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

    private static void RemoveDiamond(TextMeshProUGUI text)
    {
        Transform old = text.transform.Find(DiamondName);
        if (old != null)
        {
            UnityEngine.Object.Destroy(old.gameObject);
        }
    }

    /// <summary>A small blue diamond just left of the line of text.</summary>
    private static void AddDiamond(TextMeshProUGUI text)
    {
        if (text.transform.Find(DiamondName) != null)
        {
            return;
        }
        Sprite sprite = OrbArt.Diamond();
        if (sprite == null)
        {
            return;
        }
        Image image = R.MakeImage(text.transform, DiamondName, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform r = image.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(1, 0.5f);
        r.anchoredPosition = new Vector2(-3, -text.fontSize * 0.65f);
        r.sizeDelta = new Vector2(text.fontSize * 0.95f, text.fontSize * 0.95f);
    }

    private static void Tint(Watch w)
    {
        if (w.Ui.IsNullOrDestroyed() || !w.Ui.gameObject.activeInHierarchy)
        {
            return;
        }
        ItemAffix affix = Items_Fracture.FracturedOf(w.Item);
        if (affix == null || string.IsNullOrEmpty(affix.affixName))
        {
            return;
        }
        string wanted = affix.affixName.ToLowerInvariant();
        foreach (TextMeshProUGUI t in AffixTexts(w.Ui))
        {
            if (t == null || string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (t.text.Contains(Blue))
            {
                AddDiamond(t);
                continue;
            }
            string plain = AllTags.Replace(t.text, "").Trim();
            if (plain.Length > 0 && plain.ToLowerInvariant().Contains(wanted))
            {
                t.text = "<color=#" + Blue + ">" + Tags.Replace(t.text, "") + "</color>";
                AddDiamond(t);
                Main.logger_instance?.Msg("Fracture tooltip: marked \"" + plain + "\".");
            }
        }
    }
}

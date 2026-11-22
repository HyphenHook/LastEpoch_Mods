using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using Il2CppLE.UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Marks the encapsulated affix in an item's tooltip: its line is tinted light blue and a small blue diamond sits just before
/// it. The tooltip is the game's own; each affix in it is a UITooltipItemAffix whose text is changed after the game has
/// written it. The diamond is an image, because the game's font has no diamond character.
/// </summary>
internal static class Items_EncapsulateTooltip
{
    private const string Blue = "8fd8ff";
    internal const string RemindedColour = "D0A7FF";
    internal const string EncapsulatedColour = Blue;
    private const string DiamondName = "EncapsulateDiamond";
    private const string RemindedDiamondName = "RemindedDiamond";
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
                    RemoveDiamond(t, DiamondName);
                    RemoveDiamond(t, RemindedDiamondName);
                }
                ItemData item = __0?.TryCast<ItemData>();
                if (Items_Encapsulate.EncapsulatedAffix(item) >= 0 || Items_Reminiscence.RemindedOf(item) != null)
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

    private static void RemoveDiamond(TMP_Text text, string name)
    {
        Transform old = text.transform.Find(name);
        if (old != null)
        {
            old.gameObject.SetActive(false);
            old.SetParent(null, false);
            UnityEngine.Object.Destroy(old.gameObject);
        }
    }

    internal static void ClearAffixMarkers(TMP_Text text)
    {
        if (text.IsNullOrDestroyed()) return;
        RemoveDiamond(text, DiamondName);
        RemoveDiamond(text, RemindedDiamondName);
    }

    internal static string AffixColourText(string text, bool encapsulated) =>
        "<color=#" + (encapsulated ? Blue : RemindedColour) + ">" + Tags.Replace(text ?? "", "") + "</color>";

    /// <summary>Shared by hover tooltips and Bazaar result rows. Encapsulation takes precedence.</summary>
    internal static void MarkAffixRow(TMP_Text text, bool encapsulated, bool reminded)
    {
        if (text.IsNullOrDestroyed() || string.IsNullOrEmpty(text.text)) return;
        if (!encapsulated && !reminded) { ClearAffixMarkers(text); return; }
        RemoveDiamond(text, encapsulated ? RemindedDiamondName : DiamondName);
        string colour = encapsulated ? Blue : RemindedColour;
        if (!text.text.StartsWith("<color=#" + colour + ">", StringComparison.Ordinal))
            text.text = AffixColourText(text.text, encapsulated);
        AddDiamond(text, encapsulated ? DiamondName : RemindedDiamondName,
            encapsulated ? new Color(0.56f, 0.85f, 1f) : new Color(0.82f, 0.65f, 1f));
    }

    /// <summary>Places an affix marker just left of the text.</summary>
    private static void AddDiamond(TMP_Text text, string name, Color tint, int index = 0)
    {
        if (text.transform.Find(name) != null)
        {
            return;
        }
        Sprite sprite = OrbArt.Diamond();
        if (sprite == null)
        {
            return;
        }
        Image image = R.MakeImage(text.transform, name, tint);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform r = image.rectTransform;
        // in the middle of the affix's height, so a wording of two lines has it between them
        r.anchorMin = r.anchorMax = new Vector2(0, 0.5f);
        r.pivot = new Vector2(1, 0.5f);
        r.anchoredPosition = new Vector2(-3f - index * (text.fontSize * 0.95f + 2f), 0f);
        r.sizeDelta = new Vector2(text.fontSize * 0.95f, text.fontSize * 0.95f);
    }

    private static void Tint(Watch w)
    {
        if (w.Ui.IsNullOrDestroyed() || !w.Ui.gameObject.activeInHierarchy)
        {
            return;
        }
        TintEncapsulated(w);
        ItemAffix reminded = Items_Reminiscence.RemindedOf(w.Item);
        if (reminded == null)
        {
            return;
        }

        TextMeshProUGUI row = RowOf(w.Ui, w.Item, reminded);
        if (row == null || string.IsNullOrEmpty(row.text))
        {
            return;
        }
        MarkAffixRow(row, Items_Encapsulate.IsEncapsulated(w.Item, reminded), true);
    }

    [HarmonyPatch]
    private static class ObscuredTooltipPatch
    {
        private const string Caption = "<color=#d0a7ff>Obscured Memory</color>";

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, UITooltipItem.ItemTooltipInfo __result)
        {
            if (__result == null) return;
            var memory = Items_Reminiscence.ObscuredOf(__0);
            if (memory == null) return;

            bool prefix = memory.AffixType == (int)AffixList.AffixType.PREFIX;
            var rows = prefix ? __result.prefixesText : __result.suffixesText;
            if (rows == null)
            {
                rows = new Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<string, bool>>();
                if (prefix) __result.prefixesText = rows;
                else __result.suffixesText = rows;
            }

            foreach (var row in rows)
            {
                if (row.Item1 == Caption) return;
            }
            rows.Add(new Il2CppSystem.ValueTuple<string, bool>(Caption, false));
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemTooltipInfo))]
    private static class ModernMemoryContent
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemTooltipInfo __result)
        {
            if (__result == null) return;
            var memory = Items_Reminiscence.ObscuredOf(__0);
            int? placeholderId = memory?.OriginalAffixId ?? memory?.Choices.FirstOrDefault()?.AffixId;
            if (!placeholderId.HasValue) return;
            var original = new ItemAffix((ushort)placeholderId.Value, memory.Tier, 0, __0.itemType, SealedAffixType.None);
            var placeholder = ItemTooltipContentBuilder.GetItemAffixInfo(__0, original, __result.SlotType);
            if (placeholder == null) return;
            bool prefix = memory.AffixType == (int)AffixList.AffixType.PREFIX;
            var rows = prefix ? __result.Prefixes : __result.Suffixes;
            if (rows == null)
            {
                rows = new Il2CppSystem.Collections.Generic.List<ItemTooltipAffixInfo>();
                if (prefix) __result.Prefixes = rows;
                else __result.Suffixes = rows;
            }
            rows.Add(placeholder);
        }
    }

    [HarmonyPatch(typeof(ItemTooltip), nameof(ItemTooltip.SetTooltipInfo))]
    private static class ModernAffixMarkers
    {
        [HarmonyPrefix]
        private static void Prefix(ItemTooltip __instance)
        {
            foreach (var row in __instance.GetComponentsInChildren<ItemTooltipAffix>(true))
            {
                var text = row._modifier1?._modifierText;
                if (text.IsNullOrDestroyed()) continue;
                RemoveDiamond(text, DiamondName);
                RemoveDiamond(text, RemindedDiamondName);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(ItemTooltip __instance, ItemTooltipInfo __0)
        {
            ItemData item = __0?.Item;
            if (item.IsNullOrDestroyed()) return;
            int encapsulated = Items_Encapsulate.EncapsulatedAffix(item);
            int reminded = Items_Reminiscence.RemindedOf(item)?.affixId ?? -1;
            var memory = Items_Reminiscence.ObscuredOf(item);
            int? placeholderId = memory?.OriginalAffixId ?? memory?.Choices.FirstOrDefault()?.AffixId;
            foreach (var row in __instance.GetComponentsInChildren<ItemTooltipAffix>(true))
            {
                var text = row._modifier1?._modifierText;
                var definition = row.Info?.Affix;
                if (text.IsNullOrDestroyed() || definition == null) continue;
                int id = definition.affixId;
                if (placeholderId == id)
                {
                    row._modifier1.SetModifierText("<color=#d0a7ff>Obscured Memory</color>");
                    row._modifier1.SetExplanationText("");
                    row._modifier1.SetRangeText("");
                    if (!row._modifier2.IsNullOrDestroyed()) row._modifier2.gameObject.SetActive(false);
                    if (!row._tierText.IsNullOrDestroyed()) row._tierText.text = "";
                    continue;
                }
                if (id != encapsulated && id != reminded) continue;
                string color = id == encapsulated ? Blue : "D0A7FF";
                foreach (var modifier in new[] { row._modifier1, row._modifier2 })
                {
                    if (modifier.IsNullOrDestroyed() || modifier._modifierText.IsNullOrDestroyed()) continue;
                    modifier.SetModifierText("<color=#" + color + ">" + Tags.Replace(modifier._modifierText.text, "") + "</color>");
                }
                AddDiamond(text, id == encapsulated ? DiamondName : RemindedDiamondName,
                    id == encapsulated ? new Color(0.56f, 0.85f, 1f) : new Color(0.82f, 0.65f, 1f));
            }
        }
    }

    private static void TintEncapsulated(Watch w)
    {
        ItemAffix affix = Items_Encapsulate.EncapsulatedOf(w.Item);
        if (affix == null)
        {
            return;
        }
        // The tooltip lists prefixes and suffixes in the item's own order, so the encapsulated affix is the row at its
        // position; an affix whose displayed wording differs from its name (many do) is still found.
        TextMeshProUGUI row = RowOf(w.Ui, w.Item, affix);
        if (row != null)
        {
            Mark(row);
            return;
        }
        if (string.IsNullOrEmpty(affix.affixName))
        {
            return;
        }
        string wanted = affix.affixName.ToLowerInvariant();
        foreach (TextMeshProUGUI t in AffixTexts(w.Ui))
        {
            if (t != null && !string.IsNullOrEmpty(t.text) && t.gameObject.activeInHierarchy
                && AllTags.Replace(t.text, "").ToLowerInvariant().Contains(wanted))
            {
                Mark(t);
            }
        }
    }

    private static TextMeshProUGUI RowOf(UITooltipItem ui, ItemData item, ItemAffix affix)
    {
        TextMeshProUGUI Find(Il2CppSystem.Collections.Generic.List<ItemAffix> own, Il2CppSystem.Collections.Generic.List<UITooltipItemAffix> rows)
        {
            if (own == null || rows == null)
            {
                return null;
            }
            for (int i = 0; i < own.Count && i < rows.Count; i++)
            {
                if (own[i] != null && own[i].affixId == affix.affixId)
                {
                    UITooltipItemAffix row = rows[i];
                    return row.IsNullOrDestroyed() || row._text.IsNullOrDestroyed() || !row._text.gameObject.activeInHierarchy ? null : row._text;
                }
            }
            return null;
        }
        return Find(item.GetNonSealedPrefixes(), ui.prefixesText) ?? Find(item.GetNonSealedSuffixes(), ui.suffixesText);
    }

    private static void Mark(TextMeshProUGUI t)
    {
        if (string.IsNullOrEmpty(t.text))
        {
            return;
        }
        MarkAffixRow(t, true, false);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Services.Bazaar;
using Il2CppLE.UI.Bazaar;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>Native Bazaar cards have their own affix formatter and never use the hover tooltip's marker binding.</summary>
internal static class BazaarAffixIndicators
{
    private const string MemoryCaption = "<color=#D0A7FF>Obscured Memory</color>";
    private static readonly Dictionary<IntPtr, List<TextMeshProUGUI>> extraRows = new();

    private static TextMeshProUGUI ExtraRow(ListingUI ui, string text)
    {
        var row = ui.affixesText.FirstOrDefault(t => !t.IsNullOrDestroyed() && string.IsNullOrEmpty(t.text));
        if (row == null) return null;
        row.text = text;
        row.gameObject.SetActive(true);
        if (!extraRows.TryGetValue(ui.Pointer, out var rows)) extraRows[ui.Pointer] = rows = new();
        rows.Add(row);
        return row;
    }

    private static string Mark(string text, ItemAffix affix, int encapsulated, int reminded)
    {
        if (affix == null || string.IsNullOrEmpty(text)) return text;
        bool blue = affix.affixId == encapsulated;
        return blue || affix.affixId == reminded ? Items_EncapsulateTooltip.AffixColourText(text, blue) : text;
    }

    [HarmonyPatch(typeof(ListingInfoFormatter), nameof(ListingInfoFormatter.BakeListingInfo))]
    private static class FormatCraftedAffixes
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ListingInfoFormatter.ListingInfo __result)
        {
            if (__0.IsNullOrDestroyed() || __result == null) return;
            try
            {
                int encapsulated = Items_Encapsulate.EncapsulatedAffix(__0);
                int reminded = Items_Reminiscence.RemindedOf(__0)?.affixId ?? -1;
                var prefixes = __0.GetNonSealedPrefixes().ToArray();
                var suffixes = __0.GetNonSealedSuffixes().ToArray();
                __result.prefix1Text = Mark(__result.prefix1Text, prefixes.ElementAtOrDefault(0), encapsulated, reminded);
                __result.prefix2Text = Mark(__result.prefix2Text, prefixes.ElementAtOrDefault(1), encapsulated, reminded);
                __result.suffix1Text = Mark(__result.suffix1Text, suffixes.ElementAtOrDefault(0), encapsulated, reminded);
                __result.suffix2Text = Mark(__result.suffix2Text, suffixes.ElementAtOrDefault(1), encapsulated, reminded);
                var memory = Items_Reminiscence.ObscuredOf(__0);
                if (memory == null) return;
                // The dormant affix occupies its own prefix/suffix row, rather than the item's name.
                if (memory.AffixType == (int)AffixList.AffixType.PREFIX)
                {
                    if (string.IsNullOrEmpty(__result.prefix1Text)) __result.prefix1Text = MemoryCaption;
                    else if (string.IsNullOrEmpty(__result.prefix2Text)) __result.prefix2Text = MemoryCaption;
                }
                else
                {
                    if (string.IsNullOrEmpty(__result.suffix1Text)) __result.suffix1Text = MemoryCaption;
                    else if (string.IsNullOrEmpty(__result.suffix2Text)) __result.suffix2Text = MemoryCaption;
                }
            }
            catch (Exception ex) { ErrorLog.Report(ex, "Bazaar affix text"); }
        }
    }

    [HarmonyPatch(typeof(ListingUI), nameof(ListingUI.DisplayItem))]
    private static class DisplayCraftedAffixes
    {
        [HarmonyPrefix]
        private static void Prefix(ListingUI __instance)
        {
            if (__instance.IsNullOrDestroyed() || __instance.affixesText == null) return;
            foreach (var key in extraRows.Where(p => p.Value.All(t => t.IsNullOrDestroyed())).Select(p => p.Key).ToArray())
                extraRows.Remove(key);
            if (extraRows.Remove(__instance.Pointer, out var extras))
                foreach (var row in extras)
                    if (!row.IsNullOrDestroyed()) { row.text = ""; row.gameObject.SetActive(false); }
            // Search pages reuse cards. Remove markers before native text is assigned for the next item.
            foreach (var row in __instance.affixesText) Items_EncapsulateTooltip.ClearAffixMarkers(row);
        }

        [HarmonyPostfix]
        private static void Postfix(ListingUI __instance, ItemListingData __0)
        {
            if (__instance.IsNullOrDestroyed() || __instance.affixesText == null) return;
            try
            {
                var item = __instance.itemData;
                if (item.IsNullOrDestroyed()) return;
                int encapsulated = Items_Encapsulate.EncapsulatedAffix(item);
                int reminded = Items_Reminiscence.RemindedOf(item)?.affixId ?? -1;
                var memory = Items_Reminiscence.ObscuredOf(item);
                if (encapsulated < 0 && reminded < 0 && memory == null) return;
                var info = ListingInfoFormatter.BakeListingInfo(item, __0);
                if (info == null) return;
                var marked = new Dictionary<string, Queue<(int Id, bool Blue)>>(StringComparer.Ordinal);
                var displayed = new HashSet<int>();
                void Track(string text, ItemAffix affix)
                {
                    if (affix == null || string.IsNullOrEmpty(text)) return;
                    bool blue = affix.affixId == encapsulated;
                    if (!blue && affix.affixId != reminded) return;
                    if (!marked.TryGetValue(text, out var occurrences)) marked[text] = occurrences = new();
                    occurrences.Enqueue((affix.affixId, blue));
                }
                var prefixes = item.GetNonSealedPrefixes().ToArray();
                var suffixes = item.GetNonSealedSuffixes().ToArray();
                Track(info.prefix1Text, prefixes.ElementAtOrDefault(0));
                Track(info.prefix2Text, prefixes.ElementAtOrDefault(1));
                Track(info.suffix1Text, suffixes.ElementAtOrDefault(0));
                Track(info.suffix2Text, suffixes.ElementAtOrDefault(1));
                bool memoryShown = false;
                foreach (var row in __instance.affixesText)
                {
                    if (row.IsNullOrDestroyed()) continue;
                    memoryShown |= row.text == MemoryCaption;
                    if (marked.TryGetValue(row.text ?? "", out var occurrences) && occurrences.Count > 0)
                    {
                        var marker = occurrences.Dequeue();
                        displayed.Add(marker.Id);
                        Items_EncapsulateTooltip.MarkAffixRow(row, marker.Blue, !marker.Blue);
                    }
                }
                // Native cards have only two ordinary text fields per side. A marked third
                // prefix/suffix on crafted gear still needs its own visible, marked entry.
                foreach (int id in new[] { encapsulated, reminded }.Where(id => id >= 0).Distinct())
                {
                    if (displayed.Contains(id)) continue;
                    var affix = item.affixes.ToArray().FirstOrDefault(a => a != null && a.affixId == id);
                    if (affix == null) continue;
                    string text = TooltipItemManager.FormatAffix(item, affix, TooltipMode.DefaultInfo,
                        TooltipItemManager.SlotType.CRAFTING, false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                    var row = ExtraRow(__instance, text);
                    if (row != null) Items_EncapsulateTooltip.MarkAffixRow(row, id == encapsulated, id == reminded);
                }
                // Five/six-affix gear can already fill both native rows on the memory's side.
                // Use an otherwise unused affix text row so that the dormant entry remains visible.
                if (memory != null && !memoryShown)
                {
                    ExtraRow(__instance, MemoryCaption);
                }
            }
            catch (Exception ex) { ErrorLog.Report(ex, "Bazaar affix indicators"); }
        }
    }
}

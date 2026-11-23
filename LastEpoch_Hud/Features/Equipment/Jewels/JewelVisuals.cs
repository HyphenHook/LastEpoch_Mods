using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Jewels;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Services.Models.Items;
using Il2CppLE.Services.Visuals;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

internal static class JewelVisuals
{
    private static readonly string[] Assets = { "jewel_cobalt", "jewel_ruby", "jewel_emerald", "jewel_prismatic" };
    private static readonly HashSet<Sprite> Owned = new();
    internal static Sprite IconFor(ItemData item)
    {
        if (!JewelBaseType.IsJewel(item)) return null;
        var sprite = OrbArt.Named(HeorotCore.IsCore(item) ? "heorot_core" : VolcanicCore.IsCore(item) ? "volcanic_core" : Assets[item.subType]);
        if (sprite != null) Owned.Add(sprite);
        return sprite;
    }
    // Diagnostic for missing Jewel implicit lines: once per tooltip kind and subtype, the item's resolved implicits
    // against what the game's tooltip builder produced. -1 means the list was null.
    private static readonly HashSet<string> loggedImplicits = new();
    private static void LogImplicits(string kind, int subtype, int itemImplicits, int tooltipImplicits)
    {
        if (!loggedImplicits.Add(kind + subtype)) return;
        Main.logger_instance?.Msg("Jewel tooltip (" + kind + ") subtype " + subtype + ": item implicits " + itemImplicits
            + ", tooltip implicits " + tooltipImplicits);
    }

    /// <summary>
    /// The Jewel's name from its affix titles: the highest tier prefix title, the subtype, then the highest tier suffix
    /// title ("Mighty Cobalt Jewel of the Gale"). The titles come from Data/JewelAffixes.csv.
    /// </summary>
    internal static string NameFor(ItemData item)
    {
        if (HeorotCore.IsCore(item)) return Core.Heorot.HeorotCoreRules.Name;
        if (VolcanicCore.IsCore(item)) return Core.Volcanic.VolcanicCoreRules.Name;
        string baseName = JewelBaseType.Names[item.subType];
        string prefix = null, suffix = null;
        int prefixTier = -1, suffixTier = -1;
        if (item.affixes != null)
        {
            foreach (var affix in item.affixes)
            {
                if (affix == null || affix.IsSealed || affix.specialAffixType != AffixList.SpecialAffixType.Standard) continue;
                var definition = JewelAffixes.Get(affix.affixId);
                string title = definition?.affixTitle?.Trim();
                if (string.IsNullOrEmpty(title)) continue;
                if (definition.type == AffixList.AffixType.SUFFIX)
                {
                    if (affix.affixTier <= suffixTier) continue;
                    suffixTier = affix.affixTier;
                    suffix = title.StartsWith("of ", System.StringComparison.OrdinalIgnoreCase) ? title : "of " + title;
                }
                else
                {
                    if (affix.affixTier <= prefixTier) continue;
                    prefixTier = affix.affixTier;
                    prefix = title;
                }
            }
        }
        return (prefix == null ? "" : prefix + " ") + baseName + (suffix == null ? "" : " " + suffix);
    }

    private static readonly HashSet<string> loggedAffixes = new();

    // For checking the numbers: each affix's table range, the effect the game applies to the item, and the attunement factor.
    private static void LogAffixes(ItemData item)
    {
        try
        {
            if (item.affixes == null) return;
            float effect = ItemList.get().getAffixEffectModifier(item);
            double attunement = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_AttunementStats.ImplicitFactor(item);
            foreach (var affix in item.affixes)
            {
                var definition = affix == null ? null : JewelAffixes.Get(affix.affixId);
                if (definition?.tiers == null || affix.affixTier >= definition.tiers.Count) continue;
                if (!loggedAffixes.Add(item.individualID + ":" + affix.affixId + ":" + affix.affixTier + ":" + affix.affixRoll)) continue;
                var range = definition.tiers[affix.affixTier];
                Main.logger_instance?.Msg("Jewel affix " + affix.affixId + " " + definition.affixTitle + " T" + (affix.affixTier + 1) + " roll " + affix.affixRoll
                    + ": table range " + range.minRoll + " to " + range.maxRoll + ", item effect bonus " + effect + ", implicit attunement x" + attunement
                    + ", rolled " + JewelAffixRules.RollStepped(range.minRoll, range.maxRoll, affix.affixRoll, JewelAffixes.StepOf(affix.affixId)));
                if (affix.affixId == 2830)
                {
                    var multi = definition.TryCast<AffixList.MultiAffix>();
                    if (multi != null)
                        Main.logger_instance?.Msg("Jewel stun properties: " + string.Join(" | ", multi.affixProperties.ToArray()
                            .Select(p => p.property + " " + p.modifierType + " " + p.modDisplayName))
                            + "; primary " + range.minRoll + " to " + range.maxRoll
                            + "; extra " + string.Join(" | ", range.extraRolls.ToArray().Select(r => r.minRoll + " to " + r.maxRoll)));
                }
            }
        }
        catch (System.Exception ex) { Main.logger_instance?.Warning("Jewel affix log: " + ex.Message); }
    }

    private static bool ShowsRanges(TooltipMode mode) => mode == TooltipMode.DefaultModRanges || mode == TooltipMode.CompareModRanges;

    // The game ends each line with a line break, which opened a gap between implicits.
    private static string Clean(string text) => text?.Trim('\r', '\n');

    private static string Format(ItemDataUnpacked item, ItemList.EquipmentImplicit definition, int index, TooltipMode mode,
        TooltipItemManager.SlotType slot, float value, ValueRangeFormat format) =>
        Clean(TooltipItemManager.ImplicitFormatter(item, definition.property, definition.type, value, definition.tags,
            definition.specialTag, definition.extraTag, mode, slot, index, false, format));

    private static readonly HashSet<TooltipMode> loggedModes = new();

    /// <summary>
    /// The implicit through the game's own formatter. In a range mode the game already puts its usual blue "Range" line
    /// under the implicit (and the Explanation mode adds its explanation line), so nothing is added here.
    /// </summary>
    private static string NativeImplicit(ItemDataUnpacked item, ItemList.EquipmentImplicit definition,
        int index, TooltipMode mode, TooltipItemManager.SlotType slot, float value)
    {
        try
        {
            string text = Format(item, definition, index, mode, slot, value, ValueRangeFormat.None);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (loggedModes.Add(mode))
                Main.logger_instance?.Msg("Jewel implicit from the game's formatter (" + mode + "): \"" + text.Replace("\n", "<LF>") + "\"");
            return text;
        }
        catch (System.Exception ex)
        {
            if (loggedModes.Add(TooltipMode.None))
                Main.logger_instance?.Warning("Jewel implicit: the game's formatter failed, using the mod's own text (" + ex.Message + ")");
            return null;
        }
    }

    // The blue "Range" line under an implicit, for when the game gives no line of its own.
    private static string OwnRangeLine(ItemList.EquipmentImplicit definition)
    {
        var info = PropertyList.get().GetPropertyInfo(definition.property, definition.tags, definition.specialTag);
        if (info.IsNullOrDestroyed()) return "";
        var modifier = new ItemTooltipPropertyModifierInfo(info.getPropertyName(definition.specialTag), info,
            definition.property, definition.tags, definition.specialTag, 0, definition.type, definition.implicitValue);
        return "<size=85%><color=#6C8FD6>Range: " + modifier.ConvertValueToString(definition.implicitValue)
            + " to " + modifier.ConvertValueToString(definition.implicitMaxValue) + "</color></size>";
    }

    // The Jewel's implicit lines, written with the game's own stat wording from the rolled values.
    internal static string ImplicitText(ItemDataUnpacked item, TooltipItemManager manager, TooltipMode mode, TooltipItemManager.SlotType slot)
    {
        var lines = new List<string>();
        // Ctrl + Alt: the game's own key for the roll ranges on a tooltip.
        bool ranges = ShowsRanges(mode);
        try
        {
            var definitions = item.Implicits;
            var rolls = item.implicitRolls;
            if (definitions == null) return "";
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null) continue;
                byte roll = rolls != null && i < rolls.Length ? rolls[i] : (byte)0;
                float value = definition.implicitValue + (definition.implicitMaxValue - definition.implicitValue) * roll / 255f;
                // The game's formatter first: same colours, sizes and blue range line as every other implicit.
                float attuned = JewelAffixRules.Roll(definition.implicitValue, definition.implicitMaxValue, roll)
                    * (float)LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_AttunementStats.ImplicitFactor(item);
                string native = NativeImplicit(item, definition, i, mode, slot, attuned);
                if (!string.IsNullOrWhiteSpace(native))
                {
                    lines.Add(native);
                    // The game's own range line is already part of the text; only add one if it is somehow missing.
                    if (ranges && native.IndexOf("Range", System.StringComparison.OrdinalIgnoreCase) < 0) lines.Add(OwnRangeLine(definition));
                    continue;
                }
                var info = PropertyList.get().GetPropertyInfo(definition.property, definition.tags, definition.specialTag);
                if (info.IsNullOrDestroyed()) continue;
                var modifier = new ItemTooltipPropertyModifierInfo(info.getPropertyName(definition.specialTag), info,
                    definition.property, definition.tags, definition.specialTag, 0, definition.type, value);
                string text = modifier.GetModifierString(false);
                if (string.IsNullOrWhiteSpace(text)) continue;
                lines.Add(Clean(text));
                if (ranges) lines.Add(OwnRangeLine(definition));
            }
        }
        catch (System.Exception ex) { ErrorLog.Report(ex, "Jewel implicit text"); }
        return string.Join("\n", lines);
    }

    private static void Apply(Image image, Sprite sprite)
    {
        if (image.IsNullOrDestroyed()) return;
        if (sprite != null)
        {
            image.overrideSprite = sprite;
            image.color = Color.white;
        }
        else if (image.overrideSprite != null && Owned.Contains(image.overrideSprite)) image.overrideSprite = null;
    }

    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItemSprite))]
    private static class InventoryIcon
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance, ItemData __0) => Apply(__instance.contentImage, IconFor(__0));
    }

    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetImageSpritesAndColours))]
    private static class InventoryRefresh
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance) => Apply(__instance.contentImage,
            IconFor(__instance.EntryRef.IsNullOrDestroyed() ? null : __instance.EntryRef.data));
    }

    [HarmonyPatch(typeof(ClientVisualsService), "GetItemVisual")]
    private static class WorldVisual
    {
        [HarmonyPrefix]
        private static void Prefix(ref ItemVisualKey __0)
        {
            if (!JewelBaseType.Registered || (int)__0.EquipmentType != JewelBaseType.Id) return;
            __0.EquipmentType = EquipmentType.RING;
            __0.SubType = 0;
            __0.UniqueID = 0;
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemTooltipInfo))]
    private static class ModernContent
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemTooltipInfo __result)
        {
            if (__result == null || !JewelBaseType.IsJewel(__0)) return;
            __result.Name = NameFor(__0);
            __result.PrimaryType = "Jewel";
            __result.SecondaryType = "";
            LogImplicits("modern", __0.subType, __0.Implicits?.Count ?? -1, __result.Implicits?.Count ?? -1);
        }
    }

    [HarmonyPatch(typeof(ItemTooltip), nameof(ItemTooltip.SetTooltipInfo))]
    private static class ModernIcon
    {
        [HarmonyPrefix]
        private static void Prefix(ItemTooltip __instance, ItemTooltipInfo __0) => Apply(__instance._icon, IconFor(__0?.Item));
        [HarmonyPostfix]
        private static void Postfix(ItemTooltip __instance, ItemTooltipInfo __0) => Apply(__instance._icon, IconFor(__0?.Item));
    }

    [HarmonyPatch]
    private static class LegacyContent
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }
        [HarmonyPostfix]
        private static void Postfix(TooltipItemManager __instance, ItemDataUnpacked __0, TooltipMode __1,
            TooltipItemManager.SlotType __2, UITooltipItem.ItemTooltipInfo __result)
        {
            if (__result == null || !JewelBaseType.IsJewel(__0)) return;
            __result.nameText = NameFor(__0);
            __result.typeText = "Jewel";
            LogAffixes(__0);
            // The game's own builder leaves the implicit lines empty for base type 44, so they are written here
            // with the game's own implicit formatter (which also draws the blue roll range in the range mode).
            if (string.IsNullOrEmpty(__result.implicitText)) __result.implicitText = ImplicitText(__0, __instance, __1, __2);
            LogImplicits("legacy", __0.subType, __0.Implicits?.Count ?? -1, string.IsNullOrEmpty(__result.implicitText) ? 0 : 1);
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), nameof(UITooltipItem.SetAsItemTooltip))]
    private static class LegacyIcons
    {
        [HarmonyPrefix]
        private static void Prefix(UITooltipItem __instance, ItemDataUnpacked __3) => ApplyLegacyIcons(__instance, __3);
    }

    // The game fills the item picture again in UITooltipItem.SetItemImage, after the prefix above has run,
    // which left a plain white block for a Jewel. OrbItemVisuals' existing SetItemImage patch calls this too.
    internal static void ApplyLegacyIcons(UITooltipItem ui, ItemDataUnpacked item)
    {
        if (ui.IsNullOrDestroyed()) return;
        var sprite = IconFor(item);
        Apply(ui.itemImage, sprite);
        Apply(ui.smallItemImage, sprite);
        Apply(ui.mediumItemImage, sprite);
        Apply(ui.tallMediumItemImage, sprite);
        Apply(ui.largeItemImage, sprite);
        Apply(ui.spearItemImage, sprite);
        Apply(ui.wideItemImage, sprite);
    }
}

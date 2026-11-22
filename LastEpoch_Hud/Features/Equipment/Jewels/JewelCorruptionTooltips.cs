using System;
using System.IO;
using System.Linq;
using System.Text;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.IO;
using LastEpoch_Hud.Scripts.Core.Jewels;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

internal static class JewelCorruptionTooltips
{
    private static bool exported;

    internal static void Export()
    {
        if (exported || !JewelAffixes.Registered) return;
        try
        {
            var definitions = JewelAffixes.Definitions.Where(affix => JewelAffixes.IsCorruptionCopy(affix.affixId))
                .OrderBy(affix => affix.affixId).ToList();
            if (definitions.Count == 0) return;
            var output = new StringBuilder("JewelId,NativeId,Enabled,NativeWeight,InternalName,DisplayName,Tooltip\r\n");
            foreach (var definition in definitions)
            {
                // These objects are only passed to the native text formatter. They are
                // never inserted into a container, dropped, equipped, or saved.
                var item = new ItemDataUnpacked
                {
                    itemType = JewelBaseType.Id, subType = 0, rarity = 4,
                    classReq = ItemList.ClassRequirement.Any,
                    affixes = new Il2CppSystem.Collections.Generic.List<ItemAffix>(),
                };
                var affix = new ItemAffix((ushort)definition.affixId, 0, 255, JewelBaseType.Id, SealedAffixType.None);
                item.affixes.Add(affix);
                string tooltip = TooltipItemManager.FormatAffix(item, affix, TooltipMode.DefaultModRanges,
                    TooltipItemManager.SlotType.CRAFTING, false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                if (string.IsNullOrWhiteSpace(tooltip))
                    throw new InvalidOperationException("No native tooltip returned for jewel affix " + definition.affixId);
                output.Append(definition.affixId).Append(',')
                    .Append(definition.affixId - JewelAffixRules.InheritedIdOffset).Append(',')
                    .Append(definition.weighting > 0).Append(',')
                    .Append(definition.weighting.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvText.Quote(definition.affixName ?? "")).Append(',')
                    .Append(CsvText.Quote(definition.affixDisplayName ?? "")).Append(',')
                    .Append(CsvText.Quote(tooltip)).Append("\r\n");
            }
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "JewelAffixes.corruption-tooltips.csv");
            File.WriteAllText(path, output.ToString());
            exported = true;
            Main.logger_instance?.Msg("Verified " + definitions.Count + " jewel corruption tooltip strings through the native formatter: " + path);
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Jewel corruption tooltip verification: " + ex.Message);
        }
    }
}

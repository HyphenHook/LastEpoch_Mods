using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;
using MelonLoader;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Removes the Headhunter buffs as soon as Headhunter leaves the player.</summary>
internal static class HeadhunterUnequipHandler
{
    public static void OnItemRemoved(ItemContainerEntryHandler removed)
    {
        if (!CustomItemRemoval.IsUnique(removed, CustomUniqueSpecs.Headhunter.UniqueId))
        {
            return;
        }

        HeadhunterBuffClearer.ClearAll(HeadhunterConfigLoader.Resolved);
    }

    public static void OnEquipmentChanged()
    {
        if (HeadhunterKillSource.IsHeadhunterWorn())
        {
            return;
        }

        HeadhunterBuffClearer.ClearAll(HeadhunterConfigLoader.Resolved);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>Uses an Orb of Encapsulation to encapsulate one random affix of the item in the forge.</summary>
internal static class Items_Encapsulation
{
    private static readonly Random Rng = new();

    /// <summary>The affix the last orb encapsulated.</summary>
    public static int LastEncapsulated = -1;

    /// <summary>The item in the forge's main slot, or null.</summary>
    public static ItemData ForgeItem()
    {
        try
        {
            ItemData data = Refs_Manager.craft_slot_manager?.GetMain()?.content?.data;
            return data.IsNullOrDestroyed() ? null : data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<ItemAffix> Ordinary(ItemData item) =>
        item.affixes.ToArray().Where(a => a != null && !CraftProtection.IsAttached(item, a)).ToList();

    /// <summary>Why the orb cannot be used on the item, or an empty string.</summary>
    public static string WhyNot(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return "Put an item in the slot first.";
        }
        string blocked = CraftProtection.Blocked(item);
        if (blocked.Length != 0) return blocked;
        return EncapsulationMath.WhyNot(Ordinary(item).Count, Items_Encapsulate.EncapsulatedAffix(item) >= 0);
    }

    /// <summary>Uses one orb on the item. Returns whether it worked, with a message for the player.</summary>
    public static bool UseOn(ItemData item, out string message)
    {
        message = WhyNot(item);
        if (message.Length > 0)
        {
            return false;
        }
        if (AdvancedMaterials.Count(AdvancedMaterials.EncapsulationOrb) < 1)
        {
            message = "You have no Orbs of Encapsulation.";
            return false;
        }
        List<ItemAffix> affixes = Ordinary(item);
        ItemAffix chosen = affixes[EncapsulationMath.Pick(affixes.Count, Rng)];
        if (!AdvancedMaterials.TryTake(AdvancedMaterials.EncapsulationOrb))
        {
            message = "The Orb of Encapsulation could not be spent.";
            return false;
        }
        Items_Encapsulate.Registry.Set(item.individualID, chosen.affixId);
        Items_Encapsulate.Save();
        message = "Encapsulated: " + (string.IsNullOrEmpty(chosen.affixName) ? "an affix" : chosen.affixName);
        LastEncapsulated = chosen.affixId;
        Main.logger_instance?.Msg("Orb of Encapsulation: encapsulated affix " + chosen.affixId + " (" + chosen.affixName + ")");
        return true;
    }
}

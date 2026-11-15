using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>Using an Encapsulation Orb: fracture one random affix of the item in the forge.</summary>
internal static class Items_Encapsulation
{
    private static readonly Random Rng = new();

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
        item.affixes.ToArray().Where(a => a != null && !a.IsSealed).ToList();

    /// <summary>Why the orb cannot be used on the item, or an empty string.</summary>
    public static string WhyNot(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return "Put an item in the forge first.";
        }
        if (item.rarity == 7 || item.rarity == 8)
        {
            return "Unique and set items cannot be fractured.";
        }
        return EncapsulationMath.WhyNot(Ordinary(item).Count, Items_Fracture.FracturedAffix(item) >= 0);
    }

    /// <summary>Uses one orb on the forge item. Returns whether it worked, with a message for the player.</summary>
    public static bool Use(out string message)
    {
        ItemData item = ForgeItem();
        message = WhyNot(item);
        if (message.Length > 0)
        {
            return false;
        }
        if (AdvancedMaterials.Count(AdvancedMaterials.EncapsulationOrb) < 1)
        {
            message = "You have no Encapsulation Orbs.";
            return false;
        }
        List<ItemAffix> affixes = Ordinary(item);
        ItemAffix chosen = affixes[EncapsulationMath.Pick(affixes.Count, Rng)];
        AdvancedMaterials.TryTake(AdvancedMaterials.EncapsulationOrb);
        Items_Fracture.Registry.Set(item.individualID, chosen.affixId);
        Items_Fracture.Save();
        message = "Fractured: " + (string.IsNullOrEmpty(chosen.affixName) ? "an affix" : chosen.affixName);
        Main.logger_instance?.Msg("Encapsulation Orb: fractured affix " + chosen.affixId + " (" + chosen.affixName + ")");
        return true;
    }
}

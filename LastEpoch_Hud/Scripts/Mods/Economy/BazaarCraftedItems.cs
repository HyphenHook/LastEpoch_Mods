using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Economy;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>Listing-owned crafting metadata, with permanent markers applied when an item is delivered.</summary>
internal static class BazaarCraftedItems
{
    private static readonly Dictionary<uint, (Guid ListingId, BazaarCraftingState State)> previews = new();

    internal static BazaarCraftingState PreviewState(ItemData item) =>
        !item.IsNullOrDestroyed() && previews.TryGetValue(item.individualID, out var preview) ? preview.State : null;

    internal static void BindPreview(OfflineListing listing)
    {
        if (!listing.Mine && listing.Crafting != null && listing.IndividualId != 0)
            previews[listing.IndividualId] = (listing.Id, listing.Crafting);
    }

    internal static void ForgetPreview(OfflineListing listing)
    {
        if (previews.TryGetValue(listing.IndividualId, out var preview) && preview.ListingId == listing.Id)
            previews.Remove(listing.IndividualId);
    }

    internal static BazaarCraftingState Capture(ItemData item)
    {
        int encapsulated = Items_Encapsulate.EncapsulatedAffix(item);
        var reminded = Items_Reminiscence.RemindedOf(item);
        var memory = Items_Reminiscence.ObscuredOf(item);
        var attunement = Items_Attunement.StateOf(item);
        if (encapsulated < 0 && reminded == null && memory == null && attunement == null) return null;
        return new BazaarCraftingState
        {
            EncapsulatedAffixId = encapsulated >= 0 ? encapsulated : null,
            RemindedAffixId = reminded == null ? null : reminded.affixId,
            Memory = memory?.Copy(),
            Attunement = attunement?.Copy(),
        };
    }

    internal static BazaarCraftingState Craft(ItemDataUnpacked item, BazaarItemFactory.Kind kind, Random random)
    {
        if (item.IsNullOrDestroyed() || !ItemList.isEquipment(item.itemType) || item.corrupted || item.isUniqueSetOrLegendary())
            return null;
        bool crafter = kind == BazaarItemFactory.Kind.Crafted || kind == BazaarItemFactory.Kind.FinalProduct
            || kind == BazaarItemFactory.Kind.Creation || kind == BazaarItemFactory.Kind.SixAffix
            || kind == BazaarItemFactory.Kind.SealedGear || kind == BazaarItemFactory.Kind.PrimordialSix;
        if (!crafter) return null;

        var state = new BazaarCraftingState();
        // Some sellers finished a memory craft, while others sell the item before unveiling it.
        double memoryRoll = random.NextDouble();
        if (memoryRoll < 0.28)
        {
            var memory = Items_Reminiscence.PlanMemory(item, random, out var original);
            if (memory != null)
            {
                int index = item.affixes.IndexOf(original);
                if (memoryRoll < 0.10)
                {
                    item.affixes.RemoveAt(index);
                    state.Memory = memory;
                }
                else
                {
                    var choice = memory.Choices[random.Next(memory.Choices.Count)];
                    var replacement = Items_Reminiscence.Preview(item, memory, choice);
                    item.affixes[index] = replacement;
                    state.RemindedAffixId = replacement.affixId;
                }
            }
        }

        var ordinary = item.affixes.ToArray().Where(a => a != null && !CraftProtection.IsAttached(item, a)).ToList();
        if (state.Memory == null && ordinary.Count >= 4 && random.NextDouble() < 0.16)
            state.EncapsulatedAffixId = ordinary[random.Next(ordinary.Count)].affixId;
        if (state.Memory == null && !state.RemindedAffixId.HasValue && !state.EncapsulatedAffixId.HasValue) return null;

        item.RefreshIDAndValues();
        return state;
    }

    internal static bool PrepareDelivery(OfflineListing listing, ItemData item)
    {
        // Each purchased copy gets an identity separate from its persistent market preview.
        if (!listing.Mine)
        {
            item.RollIndividualID();
            item.RefreshIDAndValues();
        }
        var state = listing.Crafting;
        if (state == null) return true;
        if (!Items_Attunement.Restore(item, state.Attunement)) return false;
        if (state.EncapsulatedAffixId.HasValue && !Items_Encapsulate.RestoreAffix(item, state.EncapsulatedAffixId.Value))
            return false;
        if (state.RemindedAffixId.HasValue && !Items_Reminiscence.SetRemindedAffix(item, state.RemindedAffixId.Value))
            return false;
        return state.Memory == null || Items_Reminiscence.RestoreMemory(item, state.Memory);
    }
}

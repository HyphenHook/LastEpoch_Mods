using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Shatters a dropped item into affix shards; exceptions reach the caller.</summary>
public static class AutoShatter
{
    private static readonly SystemShatterRandom _random = new();

    public static bool TryShatter(ItemDataUnpacked item)
    {
        Save_Manager.Data.Pickup pickup = Save_Manager.instance.data.Items.Pickup;
        if (!ShatterRoll.Triggers(pickup.AutoShatter_Chance, _random))
        {
            return false;
        }

        ItemContainersManager manager = ItemContainersManager.Instance;
        if (manager.IsNullOrDestroyed())
        {
            return false;
        }
        if (pickup.Enable_AutoShatter_UseRune && !HasRune(manager))
        {
            return false;
        }
        if (
            !GrantShards(
                manager,
                item,
                pickup.AutoShatter_AffixChance,
                pickup.AutoShatter_QuantityChance
            )
        )
        {
            return false;
        }
        if (pickup.Enable_AutoShatter_UseRune)
        {
            ConsumeRune(manager);
        }
        return true;
    }

    private static bool HasRune(ItemContainersManager manager)
    {
        if (
            manager.materials.IsNullOrDestroyed()
            || manager.materials.shattering.IsNullOrDestroyed()
        )
        {
            return false;
        }
        return manager.materials.shattering.GetQuantity() > 0;
    }

    private static void ConsumeRune(ItemContainersManager manager)
    {
        SingleSubTypeContainer container = manager.materials.shattering;
        if (
            (container.IsNullOrDestroyed())
            || (!container.TryGetContent(out ItemContainerEntry entry))
            || (entry.IsNullOrDestroyed())
        )
        {
            return;
        }
        container.TryRemoveItem(entry, 1, Context.SILENT);
    }

    private static bool GrantShards(
        ItemContainersManager manager,
        ItemDataUnpacked item,
        int affixChance,
        int quantityChance
    )
    {
        if (manager.shardStorage == null)
        {
            return false;
        }
        Il2CppSystem.Collections.Generic.List<ItemAffix> affixes = item.affixes;
        if (affixes.IsNullOrDestroyed())
        {
            return true;
        }
        for (int i = 0; i < affixes.Count; i++)
        {
            ItemAffix affix = affixes[i];
            if (affix.IsNullOrDestroyed())
            {
                continue;
            }
            int shards = ShatterRoll.ShardsFor(affix.affixId, affixChance, quantityChance, _random);
            if (shards == 0)
            {
                continue;
            }
            manager.shardStorage.AddShard(affix.affixId, shards, true);
        }
        return true;
    }
}
